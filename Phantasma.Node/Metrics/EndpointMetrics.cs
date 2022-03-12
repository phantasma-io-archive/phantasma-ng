using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Phantasma.Node.Metrics;

public class EndpointMetrics : IEndpointMetrics
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<long>> _averages = new();
    private readonly ConcurrentDictionary<string, long> _counters = new();

    public Task Count(
        string path
    )
    {
        _counters.AddOrUpdate(path, 1, (
            _,
            value
        ) => value + 1);

        return Task.CompletedTask;
    }

    public Task<KeyValuePair<string, long>[]> GetCounts()
    {
        return Task.FromResult(_counters.ToArray());
    }

    public Task Average(
        string path,
        long duration
    )
    {
        _averages.AddOrUpdate(path, new ConcurrentQueue<long>(new[] { duration }), (
            _,
            value
        ) =>
        {
            lock (value)
            {
                if (value.Count > 99)
                {
                    value.TryDequeue(out long _);
                }

                value.Enqueue(duration);
            }

            return value;
        });

        return Task.CompletedTask;
    }

    public Task<KeyValuePair<string, long>[]> GetAverages()
    {
        return Task.FromResult(_averages.Select(pair => new KeyValuePair<string, long>(pair.Key, (long)pair.Value.Average()))
            .ToArray());
    }
}
