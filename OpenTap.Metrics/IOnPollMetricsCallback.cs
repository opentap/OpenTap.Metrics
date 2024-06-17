using System.Collections.Generic;

namespace OpenTap.Metrics;

/// <summary> Defines a class which can update metrics. </summary>
public interface IOnPollMetricsCallback
{
    /// <summary> Called right before the metric manager reads PollMetric properties. </summary>   
    ///  <param name="metrics">List of metrics from this class that are about to be polled.</param>
    void OnPollMetrics(IEnumerable<MetricInfo> metrics);
}
