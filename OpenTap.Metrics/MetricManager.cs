//            Copyright Keysight Technologies 2012-2024
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap.Metrics;

/// <summary>
///  Class for managing metrics.
/// </summary>
public static class MetricManager
{
    /// <summary>
    /// NOTE: This method only exists to clear between unit tests.
    /// This should never be used
    /// </summary>
    internal static void Reset()
    {
        _interestLookup.Clear();
        _metricProducers.Clear();
    }

    private static readonly ConcurrentDictionary<IMetricListener, HashSet<MetricInfo>> _interestLookup =
        new ConcurrentDictionary<IMetricListener, HashSet<MetricInfo>>();

    /// <summary>
    /// Subscribe to the given set of push metrics. When called again, this overwrites the interest set of the listener.
    /// </summary>
    /// <param name="listener">The listener to subscribe.</param>
    /// <param name="interest">The set of metric infos of interest.</param>
    public static void Subscribe(IMetricListener listener, IEnumerable<MetricInfo> interest)
    {
        _interestLookup[listener] = interest.ToHashSet();
    }

    /// <summary> Unsubscribe from push metrics. </summary>
    /// <param name="listener">The lsitener to unsubscribe.</param>
    public static void Unsubscribe(IMetricListener listener)
    {
        _interestLookup.TryRemove(listener, out _);
    }

    /// <summary> Returns true if a metric has interest. </summary>
    public static bool HasInterest(MetricInfo metric) => _interestLookup.Values.Any(x => x.Contains(metric));

    /// <summary> Get information about the metrics available to query. </summary>
    /// <returns></returns>
    public static IEnumerable<MetricInfo> GetMetricInfos()
    {
        var types = TypeData.GetDerivedTypes<IMetricSource>().Where(x => x.CanCreateInstance);
        List<object> producers = new List<object>();
        foreach (var type in types)
        {
            // DUT and Instrument settings will explicitly added later if they are configured on the bench,
            // regardless of whether or not they are IMetricSources.
            if (type.DescendsTo(typeof(IDut)) || type.DescendsTo(typeof(IInstrument)))
                continue;
            if (type.DescendsTo(typeof(ComponentSettings)))
            {
                if (ComponentSettings.GetCurrent(type) is IMetricSource producer)
                    producers.Add(producer);
            }
            else
            {
                if (_metricProducers.GetOrAdd(type, t => (IMetricSource)t.CreateInstance()) is IMetricSource m)
                    producers.Add(m);
            }
        }

        foreach (var metricSource in producers.Concat(InstrumentSettings.Current).Concat(DutSettings.Current))
        {

            var type1 = TypeData.GetTypeData(metricSource);

            string sourceName = (metricSource as IResource)?.Name ?? type1.GetDisplayAttribute().Name;
            var memberGrp = type1.GetMembers()
                .Where(member => member.HasAttribute<MetricAttribute>() && TypeIsSupported(member.TypeDescriptor))
                .ToLookup(type2 => type2.GetAttribute<MetricAttribute>().Group ?? sourceName);
            foreach (var member in memberGrp)
            {
                foreach (var mem in member)
                    yield return new MetricInfo(mem, member.Key, metricSource);
            }
            if (metricSource is IAdditionalMetricSources source2)
            {
                foreach (var metric in source2.AdditionalMetrics)
                    yield return metric;
            }
        }
    }

    /// <summary> For now only string, double, int, and bool type are supported. </summary>
    /// <param name="td"></param>
    /// <returns></returns>
    static bool TypeIsSupported(ITypeData td)
    {
        var type = td.AsTypeData().Type;

        var isSupportedDoubleType = type == typeof(double) || type == typeof(double?);
        var isSupportedBoolType = type == typeof(bool) || type == typeof(bool?);
        var isSupportedIntType = type == typeof(int) || type == typeof(int?);

        return isSupportedDoubleType || isSupportedBoolType || isSupportedIntType || type == typeof(string);
    }

    private static readonly ConcurrentDictionary<ITypeData, IMetricSource> _metricProducers =
        new ConcurrentDictionary<ITypeData, IMetricSource>();

    /// <summary> Push a double metric. </summary>
    public static void PushMetric(MetricInfo metric, double value)
    {
        PushMetric(new DoubleMetric(metric, value));
    }

    /// <summary> Push a boolean metric. </summary>
    public static void PushMetric(MetricInfo metric, bool value)
    {
        PushMetric(new BooleanMetric(metric, value));
    }
    /// <summary> Push a string metric. </summary>
    public static void PushMetric(MetricInfo metric, string value)
    {
        PushMetric(new StringMetric(metric, value));
    }

    /// <summary> Push an empty metric. </summary>
    public static void PushMetric(MetricInfo metric)
    {
        PushMetric(new EmptyMetric(metric));
    }

    /// <summary>
    /// Push a non-specific metric. This method is private to avoid pushing any kind of metric.
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    static void PushMetric(IMetric metric)
    {
        foreach (var consumer in _interestLookup.Keys.ToList())
        {
            if (_interestLookup.TryGetValue(consumer, out var interest) && interest.Contains(metric.Info))
                consumer.OnPushMetric(metric);
        }
    }

    static readonly TraceSource log = Log.CreateSource(nameof(MetricManager));

    /// <summary> Poll metrics. </summary>
    public static IEnumerable<IMetric> PollMetrics(IEnumerable<MetricInfo> interestSet)
    {
        var interest = interestSet.Where(i => i.Kind.HasFlag(MetricKind.Poll)).ToHashSet();
        
        foreach (var source in interest.GroupBy(i => i.Source))
        {
            if (source.Key is IOnPollMetricsCallback producer)
            {
                try
                {
                    producer.OnPollMetrics(source);
                }
                catch (Exception ex)
                {
                    log.Warning($"Unhandled exception in OnPollMetrics in '{producer}': '{ex.Message}'");
                }
            }
        }

        foreach (var metric in interest)
        {
            var metricValue = metric.GetValue(metric.Source);
            switch (metricValue)
            {
                case bool v:
                    yield return new BooleanMetric(metric, v);
                    break;
                case double v:
                    yield return new DoubleMetric(metric, v);
                    break;
                case int v:
                    yield return new DoubleMetric(metric, v);
                    break;
                case string v:
                    yield return new StringMetric(metric, v);
                    break;
                case null when metric.Type.HasFlag(MetricType.Nullable & MetricType.String):
                    // String metrics does also support null values, but does not use the nullable flag.
                    yield return new EmptyMetric(metric);
                    break;
                default:
                    log.ErrorOnce(metric, "Metric value is not a supported type: {0} of type {1}", metric.Name, metricValue?.GetType().Name ?? "null");
                    break;
            }
        }
    }

    /// <summary> Get metric information from the system. </summary>
    public static MetricInfo GetMetricInfo(object source, string member)
    {
        var type = TypeData.GetTypeData(source);
        var mem = type.GetMember(member);
        if (mem?.GetAttribute<MetricAttribute>() is MetricAttribute metric)
        {
            if (TypeIsSupported(mem.TypeDescriptor))
            {
                return new MetricInfo(mem,
                    metric.Group ?? (source as IResource)?.Name ?? type.GetDisplayAttribute()?.Name, source);
            }
        }
        return null;
    }

    public delegate void MetricCreatedEventHandler(MetricCreatedEventArgs args);
    public static event MetricCreatedEventHandler OnMetricCreated;

    private static MetricInfo CreateMetric<T>(IAdditionalMetricSources owner, string name, string groupName, MetricKind kind, Func<T> pollFunction = null)
    {
        var declaring = TypeData.GetTypeData(owner);
        var descriptor = TypeData.FromType(typeof(T));
        var metric = new MetricAttribute(name, group: groupName, kind: kind);
        var mem = new MetricMemberData(declaring, descriptor, metric, () => pollFunction());
        var mi = new MetricInfo(mem, groupName, owner);
        if (mi.Type.Equals(MetricType.Unknown))
            throw new InvalidOperationException($"Unsupported metric type '{typeof(T)}'.");
        // Notify listeners that a new metric has been created so they can subscribe to it.
        OnMetricCreated?.Invoke(new MetricCreatedEventArgs(mi));
        return mi;
    }

    public static MetricInfo CreatePollMetric<T>(IAdditionalMetricSources owner, Func<T> pollFunction, string name, string groupName) where T : IConvertible
    {
        if (pollFunction == null)
            throw new ArgumentNullException(nameof(pollFunction));
        return CreateMetric<T>(owner, name, groupName, MetricKind.Poll, pollFunction);
    }

    public static MetricInfo CreateNullablePollMetric<T>(IAdditionalMetricSources owner, Func<T> pollFunction, string name, string groupName)
    {
        if (pollFunction is null)
            throw new ArgumentNullException(nameof(pollFunction));

        EnsureNullableMetricIsValid<T>();
        return CreateMetric(owner, name, groupName, MetricKind.Poll, pollFunction);
    }

    public static MetricInfo CreatePushMetric<T>(IAdditionalMetricSources owner, string name, string groupName) where T : IConvertible
    {
        return CreateMetric<T>(owner, name, groupName, MetricKind.Push, null);
    }

    public static MetricInfo CreateNullablePushMetric<T>(IAdditionalMetricSources owner, string name, string groupName)
    {
        EnsureNullableMetricIsValid<T>();
        return CreateMetric<T>(owner, name, groupName, MetricKind.Push, null);
    }

    private static void EnsureNullableMetricIsValid<T>()
    {
        var underlyingType = Nullable.GetUnderlyingType(typeof(T));
        if (underlyingType.GetInterface(nameof(IConvertible)) is null)
        {
            throw new InvalidOperationException($"Unsupported type '{typeof(T)}' is not a 'System.IConvertible' type");
        }
    }
}
