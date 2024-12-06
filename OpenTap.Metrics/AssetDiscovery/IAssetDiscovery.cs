using System.Collections.Generic;

namespace OpenTap.Metrics.AssetDiscovery;

/// <summary>
/// Plugin interface for asset discovery.
/// Implementations of this interface can be used to discover assets such as instruments or DUTs connected to the system.
/// </summary>
public interface IAssetDiscovery : OpenTap.ITapPlugin
{
    /// <summary>
    /// Sets the priority of this provider. In case two implementations return the same 
    /// discovered asset (same Identifier), the one from the higher priority provider is used
    /// </summary>
    double Priority { get; }

    /// <summary>
    /// Discovers assets connected to the system.
    /// </summary>
    DiscoveryResult DiscoverAssets();
}

public class DiscoveryResult
{
    /// <summary>
    /// If false, the discovery cannot be performed because of an error or because the system is busy. 
    /// E.g. a test is running that might be impacted by sending a *IDN? SCPI query.
    /// An empty list in this case does not mean that no assets are available.
    /// </summary>  
    public bool IsValid { get; set; }
    /// <summary>
    /// List of assets discovered by a specific implementation of IAssetDiscovery.
    /// Null or an empty list only means no assets were found if IsValid is true.
    /// </summary>
    public IEnumerable<DiscoveredAsset> Assets { get; set; }
}


/// <summary>
/// Represents a discovered asset.
/// IAssetDiscovery implementations can specialize this class to provide additional information.
/// </summary>
public class DiscoveredAsset
{
    /// <summary>
    /// The type of the asset. E.g. "N9020A".
    /// This can be used to determine a suitable driver for the asset
    /// so it can be used as an Asset in OpenTAP.
    /// </summary>
    public string Model { get; set; }
    /// <summary>
    /// A unique identifier for the asset. This is used to identify the asset in the system.
    /// E.g. for an Instrument, this could be a combination of Manufacturer, Model and serial number.
    /// </summary>
    public string Identifier { get; set; }
    /// <summary>
    /// The manufacturer of the asset. E.g. "Keysight". 
    /// </summary>
    public string Manufacturer { get; set; }
}


