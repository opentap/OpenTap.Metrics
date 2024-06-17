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
    [Metric] [Unit("I")] public double X { get; private set; }

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
    public class IdleResultTestInstrument : Instrument, IMetricUpdateCallback
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

        public void UpdateMetrics()
        {
            Voltage = Math.Sin(sw.Elapsed.TotalSeconds * 100.0) + 2.5;
            Current = Math.Cos(sw.Elapsed.TotalSeconds * 100.0) * 0.1 + 1.5;
            Id = Guid.NewGuid().ToString();
        }


        [Metric]
        [Unit("cm")]
        [Range(minimum: 0.0)]
        public int Test { get; private set; }

        public readonly int Count = 10;

        public void PushRangeValues()
        {
            var iMetric = MetricManager.GetMetricInfo(this, nameof(Test));
            if (MetricManager.HasInterest(iMetric) == false)
                return;
            var lst = new List<int>();
            for (int i = 0; i < Count; i++)
            {
                lst.Add(i);
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

        public List<IMetric> MetricValues = new List<IMetric>();

        public void OnPushMetric(IMetric table)
        {
            MetricValues.Add(table);
        }

        public HashSet<MetricInfo> MetricFilter { get; set; } = new HashSet<MetricInfo>();

        public IEnumerable<MetricInfo> GetInterest(IEnumerable<MetricInfo> allMetrics) =>
            allMetrics.Where(MetricFilter.Contains);
    }

    [Test]
    public void TestMetricNames()
    {
        using (Session.Create())
        {
            InstrumentSettings.Current.Clear();
            var instrTest = new IdleResultTestInstrument();

            InstrumentSettings.Current.Add(instrTest);
            var metrics = MetricManager.GetMetricInfos().Select(x => x.Item1).ToArray();

            var testMetric = metrics.FirstOrDefault(m => m.MetricFullName == "INST / Test");
            Assert.IsNotNull(testMetric);
            var range = testMetric.Attributes.OfType<RangeAttribute>().FirstOrDefault();
            Assert.IsNotNull(range);
            Assert.IsTrue(range.Minimum == 0.0);

            Assert.IsTrue(metrics.Any(m => m.MetricFullName == "INST / v"));

            Assert.Contains("Test Metric Producer / Y", metrics.Select(m => m.MetricFullName).ToArray());
            InstrumentSettings.Current.Remove(instrTest);
            metrics = MetricManager.GetMetricInfos().Select(x => x.Item1).ToArray();

            Assert.IsFalse(metrics.Any(m => m.MetricFullName == "INST / v"));
        }
    }

    public class HasInterestTestListener : IMetricListener
    {
        public List<MetricInfo> interestMetrics { get; }
        public HasInterestTestListener()
        {
            interestMetrics = MetricManager.GetMetricInfos().Select(m => m.metric).OrderBy(m => m.GetHashCode()).ToList();
        }
        public void OnPushMetric(IMetric table)
        {
            
        }

        public MetricInfo[] PolledMetrics = Array.Empty<MetricInfo>();
        public IEnumerable<MetricInfo> GetInterest(IEnumerable<MetricInfo> allMetrics)
        {
            PolledMetrics = allMetrics.ToArray();
            return interestMetrics;
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
    public void TestHasInterest()
    { 
        CompareMetricLists(MetricManager.GetMetricInfos().Select(m => m.metric),
            MetricManager.GetMetricInfos().Select(m => m.metric));

        var listener = new HasInterestTestListener();
        MetricManager.RegisterListener(listener);

        for (int i = 0; i < 10; i++)
        {
            MetricManager.PollMetrics(); 
            CompareMetricLists(listener.interestMetrics, listener.PolledMetrics);
        }

        // Verify that all initial metrics are currently of interest
        foreach (var m in listener.interestMetrics)
        {
            Assert.IsTrue(MetricManager.HasInterest(m));
        }
        // Verify that all polled metrics are currently of interest
        foreach (var m in listener.PolledMetrics)
        {
            Assert.IsTrue(MetricManager.HasInterest(m));
        }


        using (Session.Create())
        {
            InstrumentSettings.Current.Clear();
            var instrTest = new IdleResultTestInstrument();
            InstrumentSettings.Current.Add(instrTest);
            
            // Verify that the metric returned by GetMetricInfo is equal to the metrics created by MetricManager
            var currentMetricInfo = MetricManager.GetMetricInfo(instrTest, nameof(instrTest.Current));
            var managerInfo = MetricManager.GetMetricInfos().Where(m =>
                currentMetricInfo.GetHashCode().Equals(m.metric.GetHashCode()) &&
                currentMetricInfo.Equals(m.metric)).ToArray();
            Assert.AreEqual(1, managerInfo.Length);
        }
    }

    [Test]
    public void TestGetMetrics()
    {
        using (Session.Create())
        {
            InstrumentSettings.Current.Clear();
            var listener = new TestMetricsListener();
            var instrTest = new IdleResultTestInstrument();

            InstrumentSettings.Current.Add(instrTest);
            var metricInfos = MetricManager.GetMetricInfos();
            foreach (var metric in metricInfos)
            {
                listener.MetricFilter.Add(metric.Item1);
            }

            MetricManager.RegisterListener(listener);
            MetricManager.PollMetrics();


            instrTest.PushRangeValues();

            var results0 = listener.MetricValues.ToArray();
            Assert.AreEqual(16, results0.Length);

            listener.Clear();
            listener.MetricFilter.Remove(listener.MetricFilter.FirstOrDefault(x => x.Name == "Test"));
            MetricManager.PollMetrics();
            instrTest.PushRangeValues();
            var results2 = listener.MetricValues.ToArray();
            Assert.AreEqual(5, results2.Length);
        }
    }
}
