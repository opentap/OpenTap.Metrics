using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;

namespace OpenTap.Metrics.UnitTest;

[Display("Test Metric Producer")]
public class TestMetricSource : IMetricSource
{
    [Metric][Unit("I")] public double X { get; private set; }

    [Metric]
    [Unit("V")]
    public double Y { get; private set; }

    private int _offset = 0;
    public void PushMetric()
    {
        var xMetric = MetricManager.GetMetricInfo(this, nameof(X));
        var yMetric = MetricManager.GetMetricInfo(this, nameof(Y));
        if (!MetricManager.HasInterest(xMetric)) return;
        for (int i = 0; i < 100; i++)
        {
            _offset += 1;
            X = _offset;
            MetricManager.PushMetric(xMetric, X);
            MetricManager.PushMetric(yMetric, Math.Sin(_offset * 0.1));
        }
    }
}

[TestFixture]
public class MetricManagerTest
{
    public class IdleResultTestInstrument : Instrument, IOnPollMetricsCallback
    {
        public IdleResultTestInstrument()
        {

        }

        public string StatusName => $"{Name}: {Voltage,2} V";

        readonly Stopwatch sw = Stopwatch.StartNew();

        [Browsable(true)]
        [Unit("V")]
        [Display("v", Group: "Metrics")]
        [Metric]
        public double Voltage { get; private set; }

        [Browsable(true)]
        [Unit("A")]
        [Display("I", Group: "Metrics")]
        [Metric]
        public double Current { get; private set; }

        [Metric]
        public string Id { get; set; }

        public void OnPollMetrics(IEnumerable<MetricInfo> metrics)
        {
            var metricV = MetricManager.GetMetricInfo(this, nameof(Voltage));
            var currentV = MetricManager.GetMetricInfo(this, nameof(Current));
            var idV = MetricManager.GetMetricInfo(this, nameof(Id));

            var metricMap = metrics.ToHashSet();

            Assert.IsTrue(metricMap.Contains(metricV));
            Assert.IsTrue(metricMap.Contains(currentV));
            Assert.IsFalse(metricMap.Contains(idV));

            Voltage = Math.Sin(sw.Elapsed.TotalSeconds * 100.0) + 2.5;
            Current = Math.Cos(sw.Elapsed.TotalSeconds * 100.0) * 0.1 + 1.5;
            Id = Guid.NewGuid().ToString();
        }


        [Metric(kind: MetricKind.Push)]
        [Unit("cm")]
        [Range(minimum: 0.0)]
        public int Test { get; private set; }

        public readonly int Count = 10;

        public void PushRangeValues()
        {
            var iMetric = MetricManager.GetMetricInfo(this, nameof(Test));
            if (MetricManager.HasInterest(iMetric) == false)
                return;
            for (int i = 0; i < Count; i++)
            {
                Test++;
                MetricManager.PushMetric(iMetric, Test);
            }

        }
    }

    public class TestMetricsListener : IMetricListener
    {

        public void Clear()
        {
            MetricValues.Clear();
        }

        public readonly List<IMetric> MetricValues = new List<IMetric>();

        public void OnPushMetric(IMetric table)
        {
            MetricValues.Add(table);
        }
    }

    [Test]
    public void TestMetricNames()
    {
        using var _ = Session.Create();
        MetricManager.Reset();
        InstrumentSettings.Current.Clear();
        var instrTest = new IdleResultTestInstrument();

        InstrumentSettings.Current.Add(instrTest);
        var metrics = MetricManager.GetMetricInfos().ToArray();

        var testMetric = metrics.FirstOrDefault(m => m.MetricFullName == "INST / Test");
        Assert.IsNotNull(testMetric);
        var range = testMetric.Attributes.OfType<RangeAttribute>().FirstOrDefault();
        Assert.IsNotNull(range);
        Assert.IsTrue(range.Minimum == 0.0);

        Assert.IsTrue(metrics.Any(m => m.MetricFullName == "INST / v"));

        Assert.Contains("Test Metric Producer / Y", metrics.Select(m => m.MetricFullName).ToArray());
        InstrumentSettings.Current.Remove(instrTest);
        metrics = MetricManager.GetMetricInfos().ToArray();

        Assert.IsFalse(metrics.Any(m => m.MetricFullName == "INST / v"));
    } 
    
    [Test]
    public void TestHasInterest()
    {
        MetricManager.Reset();
        CompareMetricLists(MetricManager.GetMetricInfos(), MetricManager.GetMetricInfos());

        var allMetrics = MetricManager.GetMetricInfos().ToArray();
        var listener = new TestMetricsListener();
        
        MetricManager.Subscribe(listener, allMetrics); 

        var returned = MetricManager.PollMetrics(allMetrics).ToArray();
        Assert.AreEqual(returned.Length, allMetrics.Length); 

        {
            var listener2 = new TestMetricsListener();
            MetricManager.Subscribe(listener2, allMetrics);
            
            // Verify that all metrics are currently of interest
            foreach (var m in allMetrics)
            {
                Assert.IsTrue(MetricManager.HasInterest(m));
            }

            MetricManager.Subscribe(listener, Array.Empty<MetricInfo>());

            // Verify that all metrics are still of interest
            foreach (var m in allMetrics)
            {
                Assert.IsTrue(MetricManager.HasInterest(m));
            }
            
            MetricManager.Subscribe(listener2, Array.Empty<MetricInfo>());
            
            // Verify that no metrics are of interest
            foreach (var m in allMetrics)
            {
                Assert.IsFalse(MetricManager.HasInterest(m));
            }
        }


        using (Session.Create())
        {
            InstrumentSettings.Current.Clear();
            var instrTest = new IdleResultTestInstrument();
            InstrumentSettings.Current.Add(instrTest);

            // Verify that the metric returned by GetMetricInfo is equal to the metrics created by MetricManager
            var currentMetricInfo = MetricManager.GetMetricInfo(instrTest, nameof(instrTest.Current));
            var managerInfo = MetricManager.GetMetricInfos().Where(m =>
                currentMetricInfo.GetHashCode().Equals(m.GetHashCode()) &&
                currentMetricInfo.Equals(m)).ToArray();
            Assert.AreEqual(1, managerInfo.Length);
        }
    }
    
    static void CompareMetricLists(IEnumerable<MetricInfo> left, IEnumerable<MetricInfo> right)
    {
        MetricInfo[] a1 = left.OrderBy(m => m.GetHashCode()).ToArray();
        MetricInfo[] a2 = right.OrderBy(m => m.GetHashCode()).ToArray();

        Assert.AreEqual(a1.Length, a2.Length);

        for (int i = 0; i < a1.Length; i++)
        {
            var m1 = a1[i];
            var m2 = a2[i];

            Assert.AreEqual(m1.GetHashCode(), m2.GetHashCode());
            Assert.AreEqual(m1, m2);
        }
    }

    [Test]
    public void TestGetMetrics()
    {
        MetricManager.Reset();
        using var _ = Session.Create();
        InstrumentSettings.Current.Clear();
        var listener = new TestMetricsListener();
        var instrTest = new IdleResultTestInstrument();
        InstrumentSettings.Current.Add(instrTest);

        var interestSet = MetricManager.GetMetricInfos().ToList();
        interestSet.Remove(MetricManager.GetMetricInfo(instrTest, nameof(instrTest.Id)));

        MetricManager.Subscribe(listener, interestSet);

        var metrics = MetricManager.PollMetrics(interestSet);
        Assert.AreEqual(metrics.Count(), interestSet.Count(m => m.Kind.HasFlag(MetricKind.Poll)));


        instrTest.PushRangeValues();

        var results0 = listener.MetricValues.ToArray();
        Assert.AreEqual(10, results0.Length);

        listener.Clear();
        interestSet.RemoveAll(x => x.Name == "Test");
        MetricManager.Subscribe(listener, interestSet);
        metrics = MetricManager.PollMetrics(interestSet);
        Assert.AreEqual(metrics.Count(), interestSet.Count(m => m.Kind.HasFlag(MetricKind.Poll)));
        instrTest.PushRangeValues();
        var results2 = listener.MetricValues.ToArray();
        Assert.AreEqual(0, results2.Length);
    }
}
