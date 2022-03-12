using System.Threading;
using System.Threading.Tasks;
using Foundatio.Extensions.Hosting.Startup;
using Phantasma.Node.Events;
using Microsoft.Extensions.Hosting;

namespace Phantasma.Node.Hosting;

public class EventBusBackgroundService : BackgroundService
{
    private readonly IEventBus _bus;
    private readonly StartupActionsContext _startupContext;

    public EventBusBackgroundService(
        IEventBus bus,
        StartupActionsContext startupContext
    )
    {
        _bus = bus;
        _startupContext = startupContext;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken
    )
    {
        if (_startupContext != null)
        {
            await _startupContext.WaitForStartupAsync(stoppingToken);
        }

        await _bus.Run(stoppingToken);
    }
}
