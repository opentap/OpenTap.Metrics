using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap.Metrics;

class MetricMemberData : IMemberData
{
    public MetricMemberData(ITypeData declaringType, ITypeData typeDescriptor, MetricAttribute attr, Func<object> getter, params object[] additionalAttributes)
    {
        Name = attr.Name;
        DeclaringType = declaringType;
        TypeDescriptor = typeDescriptor;
        Attributes = new[] { attr }.Concat(additionalAttributes).ToArray();
        _getter = getter ?? (() => null);
    }
    public ITypeData DeclaringType { get; }

    public ITypeData TypeDescriptor { get; }

    public bool Writable => false;

    public bool Readable => true;

    public IEnumerable<object> Attributes { get; }

    public string Name { get; }

    private readonly Func<object> _getter = null;
    public object GetValue(object owner)
    {
        return _getter();
    }

    public void SetValue(object owner, object value)
    {
    }
}
