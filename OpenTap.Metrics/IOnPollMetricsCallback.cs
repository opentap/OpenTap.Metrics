//            Copyright Keysight Technologies 2012-2024
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.Collections.Generic;

namespace OpenTap.Metrics;

/// <summary> Defines a class which can update metrics. </summary>
public interface IOnPollMetricsCallback
{
    /// <summary> Called right before the metric manager reads PollMetric properties. </summary>   
    ///  <param name="metrics">List of metrics from this class that are about to be polled.</param>
    void OnPollMetrics(IEnumerable<MetricInfo> metrics);
}
