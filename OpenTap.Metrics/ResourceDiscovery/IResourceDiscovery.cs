using System.Collections.Generic;

namespace OpenTap.Metrics.ResourceDiscovery;

/// <summary>
/// Plugin interface for resource discovery.
/// Implementations of this interface can be used to discover resources such as instruments or DUTs connected to the system.
/// </summary>
public interface IResourceDiscovery : OpenTap.ITapPlugin
{
    /// <summary>
    /// Sets the priority of this provider. In case two implementations return the same 
    /// discovered resource (same Identifier), the one from the higher priority provider is used
    /// </summary>
    double Priority { get; }
    IEnumerable<DiscoveredResource> DiscoverResources();
}

/// <summary>
/// Represents a discovered resource.
/// IResourceDiscovery implementations can specialize this class to provide additional information.
/// </summary>
public class DiscoveredResource
{
    /// <summary>
    /// The type of the resource. E.g. "N9020A".
    /// This can be used to determine a suitable driver for the resource.
    /// </summary>
    public string Model { get; set; }
    /// <summary>
    /// A unique identifier for the resource. This is used to identify the resource in the system.
    /// E.g. for an Instrument, this could be the serial number.
    /// </summary>
    public string Identifier { get; set; }
    /// <summary>
    /// The firmware version of the resource.
    /// </summary>
    public string FirmwareVersion { get; set; }

}


