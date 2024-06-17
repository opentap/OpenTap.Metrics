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
    static readonly HashSet<IMetricListener> _consumers =
        new HashSet<IMetricListener>();

    /// <summary> Register a metric consumer. </summary>
    /// <param name="listener"></param>
    public static void RegisterListener(IMetricListener listener)
    {
        _consumers.Add(listener);
        foreach (var i in listener.GetInterest(GetMetricInfos()))
        {
            // Interest is normally populated during PollMetrics,
            // but if only Push metrics are used, Poll may never be called.
            _interest.Add(i);
        }
    }

    /// <summary> Register a metric consumer. </summary>
    /// <param name="listener"></param>
    public static void UnregisterListener(IMetricListener listener)
    {
        _consumers.Remove(listener);
    }

    /// <summary> Returns true if a metric has interest. </summary>
    public static bool HasInterest(MetricInfo metric) => _interest.Contains(metric);
        
    /// <summary> Get information about the metrics available to query. </summary>
    /// <returns></returns>
    public static IEnumerable<MetricInfo> GetMetricInfos()
    {
        var types = TypeData.GetDerivedTypes<IMetricSource>().Where(x => x.CanCreateInstance);
        List<IMetricSource> producers = new List<IMetricSource>();
        foreach (var type in types)
        {
            if (type.DescendsTo(typeof(ComponentSettings)))
            {
                if(ComponentSettings.GetCurrent(type) is IMetricSource producer)
                    producers.Add(producer);
            }
            else
            {
                if (_metricProducers.GetOrAdd(type, t => (IMetricSource)t.CreateInstance()) is IMetricSource m)
                    producers.Add(m);
            }
        }

        foreach (var metricSource in InstrumentSettings.Current.Cast<object>().Concat(DutSettings.Current)
                     .Concat(producers))
        {

            var type1 = TypeData.GetTypeData(metricSource);
                
            string sourceName = (metricSource as IResource)?.Name ?? type1.GetDisplayAttribute().Name;
            var memberGrp = type1.GetMembers()
                .Where(member => member.HasAttribute<MetricAttribute>() && TypeIsSupported(member.TypeDescriptor))
                .ToLookup(type2 => type2.GetAttribute<MetricAttribute>().Group ?? sourceName);
            foreach (var member in memberGrp)
            {
                foreach(var mem in member)
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
        return type == typeof(double) || type == typeof(bool) || type == typeof(int) || type == typeof(string);
    }

    private static readonly ConcurrentDictionary<ITypeData, IMetricSource> _metricProducers =
        new ConcurrentDictionary<ITypeData, IMetricSource>();

    private static HashSet<MetricInfo> _interest = new HashSet<MetricInfo>();

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
        
    /// <summary>
    /// Push a non-specific metric. This method is private to avoid pushing any kind of metric.
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    static void PushMetric(IMetric metric)
    {
        var metrics = new[]
        {
            metric.Info
        };
        foreach (var consumer in _consumers)
        {
            var thisInterest = consumer.GetInterest(metrics);
            if (thisInterest.Any(m => m == metric.Info))
                consumer.OnPushMetric(metric);
        }
    }
        
    static readonly TraceSource log = Log.CreateSource("Metric");

    /// <summary> Poll metrics. </summary>
    public static void PollMetrics()
    {
        var allMetrics = GetMetricInfos().Where(m => m.Kind.HasFlag(MetricKind.Poll)).ToArray();
        Dictionary<IMetricListener, MetricInfo[]> interestLookup = new Dictionary<IMetricListener, MetricInfo[]>();
        HashSet<MetricInfo> InterestMetrics = new HashSet<MetricInfo>();
        foreach (var consumer in _consumers)
        {
            interestLookup[consumer] = consumer.GetInterest(allMetrics).ToArray();
            InterestMetrics.UnionWith(interestLookup[consumer]);
        }

        var interest2 = interestLookup.Values.SelectMany(x => x).Distinct().ToHashSet();
        _interest = interest2;
        foreach (var producer in allMetrics.Where(x => InterestMetrics.Contains(x)).Select(x => x.Source).OfType<IOnPollMetricsCallback>().Distinct())
        {
            var polled = allMetrics.Where(m => ReferenceEquals(m.Source, producer)).Where(m => InterestMetrics.Contains(m));
            try
            {
                producer.OnPollMetrics(polled);
            }
            catch (Exception ex)
            {
                log.Warning($"Unhandled exception in OnPollMetrics on '{producer}': '{ex.Message}'");
            }
        }
            
        Dictionary<MetricInfo, IMetric> metricValues = new Dictionary<MetricInfo, IMetric>();
        foreach (var metric in allMetrics)
        {
            if (interest2.Contains(metric) == false)
                continue;
            IMetric metricObject = null;
            var metricValue = metric.GetValue(metric.Source);
            switch (metricValue)
            {
                case bool v:
                    metricObject = new BooleanMetric(metric, v);
                    break;
                case double v:
                    metricObject = new DoubleMetric(metric, v);
                    break;
                case int v:
                    metricObject = new DoubleMetric(metric, v);
                    break;
                case string v:
                    metricObject = new StringMetric(metric, v);
                    break;
                default:
                    log.ErrorOnce(metric, "Metric value is not a supported type: {0} of type {1}", metric.Name, metricValue?.GetType().Name ?? "null");
                    break;
            }
            if (metricObject != null)
            {
                metricValues[metric] = metricObject;
            }
        }

        foreach (var consumerInterest in interestLookup)
        {
            var consumer = consumerInterest.Key;
            foreach (var metric in consumerInterest.Value)
            {
                if (metricValues.TryGetValue(metric, out var metricValue))
                {
                    try
                    {
                        consumer.OnPushMetric(metricValue);
                    }
                    catch (Exception ex)
                    {
                        log.Warning($"Unhandled exception in OnPushMetric on '{consumer}': '{ex.Message}'");
                    }
                }
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
}
