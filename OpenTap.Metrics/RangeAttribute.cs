//            Copyright Keysight Technologies 2012-2024
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;

namespace OpenTap.Metrics;

/// <summary> Specifies an optional min and max value for a property. </summary>
[AttributeUsage(AttributeTargets.Property)]
public class RangeAttribute : Attribute
{
    /// <summary>  The minimum value for the property. </summary>
    public double? Minimum { get; }
    /// <summary> The maximum value for the property. </summary>
    public double? Maximum { get; }

    /// <summary> Creates a new instance of the RangeAttribute. </summary>
    public RangeAttribute(double minimum = double.NaN, double maximum = double.NaN)
    {
        if (!double.IsNaN(minimum))
            Minimum = minimum;
        if(!double.IsNaN(maximum))
            Maximum = maximum;
    }
}
