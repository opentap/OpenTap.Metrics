using System.Collections.Generic;

namespace OpenTap.Metrics;

public interface IAdditionalMetricSources : IMetricSource
{
    IEnumerable<MetricInfo> AdditionalMetrics { get; } 
}