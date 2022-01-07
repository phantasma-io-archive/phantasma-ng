using System.Threading;
using System.Threading.Tasks;

namespace Phantasma.Spook.Events;

public interface IEventBus
{
    Task Run(CancellationToken cancellationToken);
}
