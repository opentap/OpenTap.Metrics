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
    public class DynamicMetricProvider : Instrument, IAdditionalMetricSources, IOnPollMetricsCallback
    {
        const string Group = "Dynamic Metric Test";
        IEnumerable<MetricInfo> IAdditionalMetricSources.AdditionalMetrics => new[] { PollMetric, PushMetric };
        public MetricInfo PollMetric { get; }
        public MetricInfo PushMetric { get; }

        public double Counter { get; private set; } = 0;

        public DynamicMetricProvider()
        {
            PollMetric = MetricManager.CreatePollMetric(this, () => Counter, "Counter", Group);
            PushMetric = MetricManager.CreatePushMetric<double>(this, "Pusher", Group);
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

        public void OnSubscriptionsChanged(MetricInfo metric, int subscribers)
        {
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
    public void TestNewMetric()
    {
        MetricManager.Reset();
        using var session = Session.Create(SessionOptions.OverlayComponentSettings);
        MetricInfo result = null;
        void onNewMetric(MetricCreatedEventArgs args)
        {
            MetricManager.OnMetricCreated -= onNewMetric;
            result = args.Metric;
        }
        var provider = new DynamicMetricProvider();
        MetricManager.OnMetricCreated += onNewMetric;
        var created = MetricManager.CreatePushMetric<double>(provider, "test", "group");
        Assert.IsNotNull(created);
        Assert.AreEqual(created, result);
    }

    [Test]
    public void TestDynamicMetrics()
    {
        MetricManager.Reset();
        using var session = Session.Create(SessionOptions.OverlayComponentSettings);
        var dyn = new DynamicMetricProvider();
        InstrumentSettings.Current.Add(dyn);
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

