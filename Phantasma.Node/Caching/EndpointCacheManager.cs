using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Foundatio.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Phantasma.Node.Caching;

public class EndpointCacheManager : IEndpointCacheManager
{
    private const string DefaultTag = "default";
    private readonly ScopedCacheClient _cache;
    private readonly ILogger<EndpointCacheManager> _logger;

    public EndpointCacheManager(ILogger<EndpointCacheManager> logger, ICacheClient cacheClient)
    {
        _logger = logger;
        _cache = new ScopedCacheClient(cacheClient, nameof(EndpointCacheManager));
    }

    public Task<bool> Add(string key, string content, int duration, string tag = null)
    {
        tag = !string.IsNullOrWhiteSpace(tag) ? tag : DefaultTag;
        var tagCache = new ScopedCacheClient(_cache, tag);

        return duration == 0
            ? Task.FromResult(false)
            : tagCache.AddAsync(key, content, duration < 0 ? null : TimeSpan.FromSeconds(duration));
    }

    public async Task<EndpointCacheResult> Get(string route,
        IEnumerable<KeyValuePair<string, StringValues>> queryParams, string tag = null)
    {
        tag = !string.IsNullOrWhiteSpace(tag) ? tag : DefaultTag;
        var tagCache = new ScopedCacheClient(_cache, tag);
        var result = new EndpointCacheResult();

        // Sort keys for consistent cache key
        queryParams = queryParams.OrderBy(pair => pair.Key).ToArray();

        var cacheKey = "";
        foreach (var arg in queryParams)
        {
            // Sort values for consistent cache key
            var values = arg.Value.OrderBy(value => value).ToArray();

            cacheKey += "/";
            cacheKey += $"[{arg.Key}, {string.Join(',', values)}]";
        }

        result.Key = $"{route}{cacheKey}";

        var cached = await tagCache.GetAsync<string>(result.Key, null);
        if (string.IsNullOrEmpty(cached)) return result;

        result.Cached = true;
        result.Content = cached;

        // Logging this call.
        StringBuilder sb = new();
        sb.Append("API request");

        if (result.Cached) sb.Append(" [Cached]");

        sb.Append($": {route}(");

        for (var i = 0; i < queryParams.Count(); i++)
        {
            if (i > 0)
            {
                sb.Append(',');
                sb.Append(' ');
            }

            sb.Append($"{queryParams.ElementAt(i)}");
        }

        sb.Append(')');

        _logger.LogInformation(sb.ToString());

        return result;
    }

    public async Task<bool> Invalidate(string tag = null)
    {
        tag = !string.IsNullOrWhiteSpace(tag) ? tag : DefaultTag;
        var count = await _cache.RemoveByPrefixAsync(tag);
        _logger.LogDebug("Cache cleared; Tag: {Tag}, Affected: {Count}", tag, count);

        return true;
    }
}
