//            Copyright Keysight Technologies 2012-2024
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

namespace OpenTap.Metrics;

/// <summary> Indicates that a class can consume metrics. </summary>
public interface IMetricListener
{
    /// <summary>  Event occuring when a metric is pushed. </summary>
    void OnPushMetric(IMetric table);
}
