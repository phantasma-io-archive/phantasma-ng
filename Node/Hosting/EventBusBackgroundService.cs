using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Phantasma.Spook.Events;

namespace Phantasma.Spook.Hosting;

public class EventBusBackgroundService : BackgroundService
{
    private readonly IEventBus _bus;

    public EventBusBackgroundService(IEventBus bus)
    {
        _bus = bus;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _bus.Run(stoppingToken);
    }
}
