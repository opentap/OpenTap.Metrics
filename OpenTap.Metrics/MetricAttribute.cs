using System;

namespace OpenTap.Metrics;

/// <summary> Defines a property as a metric. </summary>
public class MetricAttribute : Attribute
{
    /// <summary> The name of the metric. </summary>
    public string Name { get; }

    /// <summary> Optionally give the metric a group. </summary>
    public string Group { get; }

    /// <summary> Whether this metric can be polled or will be published out of band. </summary>
    public MetricKind Kind { get; }

    /// <summary> Creates a new instance of the metric attribute </summary>
    ///  <param name="name">The name of the metric.</param>
    ///  <param name="group">The group of the metric.</param>
    ///  <param name="kind"> The push / poll semantics of the metric. </param>
    public MetricAttribute(string name, string group = null, MetricKind kind = MetricKind.Poll)
    {
        Name = name;
        Group = group;
        Kind = kind;
    }

    /// <summary> Creates a new instance of the metric attribute.</summary>
    public MetricAttribute() : this(null)
    {
    }
}
