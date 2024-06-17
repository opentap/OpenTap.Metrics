using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap.Metrics;

/// <summary> Information about a given metric, </summary>
public class MetricInfo
{
    /// <summary> Whether this metric can be polled or will be published out of band. </summary>
    public MetricKind Kind { get; }

    /// <summary> The type of this metric. </summary>
    public MetricType Type { get; }

    /// <summary> The metric member object. </summary>
    IMemberData Member { get; }

    /// <summary> The attributes of the metric. </summary>
    public IEnumerable<object> Attributes { get; }

    /// <summary> The name of the metric group. </summary>
    public string GroupName { get; }

    /// <summary> Gets the full name of the metric. </summary>
    public string MetricFullName => $"{GroupName} / {Name}";

    /// <summary> The name of the metric. </summary>
    public string Name { get; }

    /// <summary> Creates a new metric info based on a member name. </summary>
    /// <param name="mem">The metric member object.</param>
    /// <param name="groupName">The name of the metric group.</param>
    public MetricInfo(IMemberData mem, string groupName)
    {
        Member = mem;
        GroupName = groupName;
        Attributes = Member.Attributes.ToArray();
        var metricAttr = Attributes.OfType<MetricAttribute>().FirstOrDefault();
        Kind = metricAttr?.Kind ?? MetricKind.Poll;

        if (mem.TypeDescriptor.IsNumeric())
        {
            Type = MetricType.Double;
        }
        else if (mem.TypeDescriptor.DescendsTo(typeof(string)))
        {
            Type = MetricType.String;
        }
        else if (mem.TypeDescriptor.DescendsTo(typeof(bool)))
        {
            Type = MetricType.Boolean;
        }

        Name = metricAttr?.Name ?? Member.GetDisplayAttribute()?.Name;
    }

    /// <summary> Creates a new metric info based on custom data. </summary>
    /// <param name="name">The name of the metric.</param>
    /// <param name="groupName">The name of the metric group.</param>
    /// <param name="attributes">The attributes of the metric.</param>
    ///  <param name="kind">The push / poll semantics of the metric. </param>
    public MetricInfo(string name, string groupName, IEnumerable<object> attributes, MetricKind kind)
    {
        Name = name;
        Member = null;
        GroupName = groupName;
        Attributes = attributes;
        Kind = kind;
    }

    /// <summary>
    /// Provides name for the metric.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => $"Metric: {MetricFullName}";

    /// <summary>
    /// Implements equality for metric info.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object obj)
    {
        if (obj is MetricInfo o)
            return string.Equals(GroupName, o.GroupName, StringComparison.Ordinal) &&
                   string.Equals(Name, o.Name, StringComparison.Ordinal) &&
                   Equals(Member, o.Member);

        return false;
    }

    /// <summary>
    /// Hash code for metrics.
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(Name.GetHashCode(), GroupName?.GetHashCode() ?? 0, Member?.GetHashCode(), 5639212);
    }

    /// <summary> Gets the value of the metric. </summary>
    public object GetValue(object metricSource)
    {
        return Member?.GetValue(metricSource);
    }
}
