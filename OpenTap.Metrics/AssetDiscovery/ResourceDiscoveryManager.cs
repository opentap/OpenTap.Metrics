using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap.Metrics.AssetDiscovery;

public static class AssetDiscoveryManager
{
    private static TraceSource log = OpenTap.Log.CreateSource("AssetDiscovery");
    /// <summary>
    /// Returns all discovered assets from all available providers.
    /// </summary>
    public static IEnumerable<DiscoveredAsset> DiscoverAllAssets()
    {
        Dictionary<string, DiscoveredAsset> assets = new Dictionary<string, DiscoveredAsset>();
        foreach (var provider in GetAssetDiscoveryProviders())
        {
            try
            {
                foreach (var asset in provider.DiscoverAssets())
                {
                    assets[asset.Identifier] = asset;
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error while discovering resources from {provider.GetType().Name}: {ex.Message}");
            }
        }
        return assets.Values;
    }

    private static IEnumerable<IAssetDiscovery> _assetDiscoveryProviders;
    private static IEnumerable<IAssetDiscovery> GetAssetDiscoveryProviders()
    {
        if (_assetDiscoveryProviders == null)
        {
            _assetDiscoveryProviders = TypeData.GetDerivedTypes<IAssetDiscovery>().Where(x => x.CanCreateInstance)
                .Select(x => x.CreateInstance() as IAssetDiscovery)
                .OrderBy(x => x.Priority)
                .ToList();
        }
        return _assetDiscoveryProviders;
    }

    // public static void PushDiscoveredAssets(DiscoveredAsset asset)
    // {
    //
    // }
}
