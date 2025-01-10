namespace OpenTap.Metrics;

/// <summary>
/// This is a hint to the poller indicating whether or not this metric ought to be enabled by default. A UI is free to ignore this hint.
/// </summary>
public enum MetricSuggestedInitialState
{ 
    /// <summary>
    /// Indicate that the metric is indifferent to whether or not it is enabled
    /// </summary>
    Indifferent,
    /// <summary>
    /// Indicate that the metric ought to be disabled by default
    /// </summary>
    Disabled,
    /// <summary>
    /// Indicate that the metric ought to be enabled by default
    /// </summary>
    Enabled,
}