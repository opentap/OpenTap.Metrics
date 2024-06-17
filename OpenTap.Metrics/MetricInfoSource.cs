namespace OpenTap.Metrics;

public class MetricInfoData 
{
    public MetricInfo Metric { get; }
    public object Source { get; }

    public MetricInfoData(MetricInfo metric, object source)
    {
        Metric = metric;
        Source = source;
    }
}

