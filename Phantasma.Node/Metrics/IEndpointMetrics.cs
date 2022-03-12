using System.Collections.Generic;
using System.Threading.Tasks;

namespace Phantasma.Node.Metrics;

public interface IEndpointMetrics
{
    Task Count(
        string path
    );

    Task<KeyValuePair<string, long>[]> GetCounts();

    Task Average(
        string path,
        long duration
    );

    Task<KeyValuePair<string, long>[]> GetAverages();
}
