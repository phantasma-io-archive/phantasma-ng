using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Phantasma.Spook.Caching;

public interface IEndpointCacheManager
{
    Task<bool> Add(string key, string content, int duration, string tag = null);

    Task<EndpointCacheResult> Get(string route, IEnumerable<KeyValuePair<string, StringValues>> queryParams,
        string tag = null);

    Task<bool> Invalidate(string tag = null);
}
