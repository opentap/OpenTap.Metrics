//            Copyright Keysight Technologies 2012-2024
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap.Metrics;

/// <summary> Defines a property as a metric. </summary>
public class MetricAttribute : Attribute
{
    /// <summary> Optionally give the metric a name. </summary>
    public string Name { get; }

    /// <summary> Optionally give the metric a group. </summary>
    public string Group { get; }

    /// <summary> Whether this metric can be polled or will be published out of band. </summary>
    public MetricKind Kind { get; }
    
    /// <summary> The suggested poll rate of the metric, in seconds. </summary>
    public int SuggestedPollRate { get; set; }
    
    /// <summary> The suggested initial state of the metric. </summary>
    public MetricSuggestedInitialState SuggestedInitialState { get; set; } 
    
    /// <summary> Creates a new instance of the metric attribute </summary>
    ///  <param name="name">Optionally, the name of the metric.</param>
    ///  <param name="group">The group of the metric.</param>
    ///  <param name="kind"> The push / poll semantics of the metric. </param>
    public MetricAttribute(string name = null, string group = null, MetricKind kind = MetricKind.Poll)
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
