using System.Threading;
using System.Threading.Tasks;

namespace Phantasma.Node.Events;

public interface IEventBus
{
    Task Run(CancellationToken cancellationToken);
}
