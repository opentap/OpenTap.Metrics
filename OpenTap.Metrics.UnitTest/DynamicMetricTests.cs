//            Copyright Keysight Technologies 2012-2024
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace OpenTap.Metrics.UnitTest;

[TestFixture]
public class DynamicMetricTests
{

    class MetricMemberData : IMemberData
    {
        public MetricMemberData(ITypeData declaringType, ITypeData typeDescriptor, MetricAttribute attr, Func<object> getter, params object[] additionalAttributes)
        {
            Name = attr.Name;
            DeclaringType = declaringType;
            TypeDescriptor = typeDescriptor;
            Attributes = new[] { attr }.Concat(additionalAttributes).ToArray();
            Getter = getter;
        }
        public ITypeData DeclaringType { get; }

        public ITypeData TypeDescriptor { get; }

        public bool Writable => false;

        public bool Readable => true;

        public IEnumerable<object> Attributes { get; }

        public string Name { get; }

        private readonly Func<object> Getter = null;
        public object GetValue(object owner)
        {
            return Getter?.Invoke() ?? null;
        }

        public void SetValue(object owner, object value)
        {
        }

    }

    static MetricInfo CreateDynamicMetric<T>(IMetricSource owner, MetricKind kind, Func<T> getter, string name, string groupName) where T : IConvertible
    {
        if (kind == MetricKind.Poll && getter == null)
            throw new ArgumentNullException(nameof(getter));
        var declaring = TypeData.GetTypeData(owner);
        var descriptor = TypeData.FromType(typeof(T));
        var metric = new MetricAttribute(name, group: groupName, kind: kind);
        var mem = new MetricMemberData(declaring, descriptor, metric, () => getter());
        var mi = new MetricInfo(mem, groupName, owner);
        if (mi.Type == MetricType.Unknown)
            throw new InvalidOperationException($"Unsupported metric type '{typeof(T)}'.");
        return mi;
    }


    public class DynamicMetricProvider : Instrument, IAdditionalMetricSources, IOnPollMetricsCallback
    {
        const string Group = "Dynamic Metric Test";
        IEnumerable<MetricInfo> IAdditionalMetricSources.AdditionalMetrics => new[] { PollMetric, PushMetric };
        public MetricInfo PollMetric { get; }
        public MetricInfo PushMetric { get; }

        public double Counter { get; private set; } = 0;

        public DynamicMetricProvider()
        {
            PollMetric = CreateDynamicMetric(this, MetricKind.Poll, () => Counter, "Counter", Group);
            PushMetric = CreateDynamicMetric<double>(this, MetricKind.Push, null, "Pusher", Group);
        }

        public void PushDouble(double value)
        {
            MetricManager.PushMetric(PushMetric, value);
        }

        public void OnPollMetrics(IEnumerable<MetricInfo> metrics)
        {
            if (metrics.Contains(PollMetric))
                Counter++;
        }
    }

    public class DynamicMetricListener : IMetricListener
    {
        public object LastMetric { get; private set; }
        public void OnPushMetric(IMetric table)
        {
            if (table.Info.MetricFullName == "Dynamic Metric Test / Pusher")
            {
                LastMetric = table.Value;
            }
        }
    }

    [Test]
    public void TestDynamicMetrics()
    {
        MetricManager.Reset();
        using var session = Session.Create(SessionOptions.OverlayComponentSettings);
        var dyn = new DynamicMetricProvider();
        InstrumentSettings.Current.Add(dyn);
        var metrics = MetricManager.GetMetricInfos();
        var listener = new DynamicMetricListener();
        MetricManager.Subscribe(listener, new[] { dyn.PushMetric });
        Assert.AreEqual(null, listener.LastMetric);

        for (double i = 0; i < 10; i++)
        {
            dyn.PushDouble(i);
            Assert.AreEqual(i, listener.LastMetric);
        }

        for (double i = 1; i < 10; i++)
        {
            // DynamicMetricProvider is incrementing Counter whenever it is polled
            IMetric m = MetricManager.PollMetrics(new[] { dyn.PollMetric }).First();
            Assert.AreEqual(i, m.Value);
            Assert.AreEqual(i, dyn.Counter);
        }
    }
}

