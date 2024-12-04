using System.Collections.Generic;
using System.Linq;

namespace OpenTap.Metrics.ResourceDiscovery;

public static class ResourceDiscoveryManager
{
    /// <summary>
    /// Returns all discovered resources from all available providers.
    /// </summary>
    public static IEnumerable<DiscoveredResource> DiscoverAllResources()
    {
        foreach (var provider in GetResourceDiscoveryProviders())
        {
            foreach (var res in provider.DiscoverResources())
            {
                yield return res;
            }
        }
    }

    private static IEnumerable<IResourceDiscovery> _resourceDiscoveryProviders;
    private static IEnumerable<IResourceDiscovery> GetResourceDiscoveryProviders()
    {
        if (_resourceDiscoveryProviders == null)
        {
            _resourceDiscoveryProviders = TypeData.GetDerivedTypes<IResourceDiscovery>().Where(x => x.CanCreateInstance)
                .Select(x => x.CreateInstance() as IResourceDiscovery)
                .OrderBy(x => x.Priority);
        }
        return _resourceDiscoveryProviders;
    }

    // public static void PushDiscoveredResource(DiscoveredResource res)
    // {
    //
    // }
}
