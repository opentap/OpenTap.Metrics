using System;

namespace OpenTap.Metrics;

[Flags]
public enum MetricKind
{
    /// <summary> This metric can be polled. </summary>
    Poll = 1,
    /// <summary> This metric can be pushed out of band. </summary>
    Push = 2,
    /// <summary> This metric can be polled and pushed out of band. </summary>
    PushPoll = Push | Poll,
}
