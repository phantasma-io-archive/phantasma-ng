using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Foundatio.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phantasma.Node.Caching;

namespace Phantasma.Node.Events;

public class EventBus : IEventBus
{
    private readonly ILogger<EventBus> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IMessageSubscriber _subscriber;

    public EventBus(ILogger<EventBus> logger, IMessageSubscriber subscriber, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _subscriber = subscriber;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task Run(CancellationToken cancellationToken)
    {
        try
        {
            await RunInternal(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "Event bus encountered an unexpected exception");
        }
    }

    private Task RunInternal(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting global event subscriptions");

        var tasks = new List<Task>
        {
            _subscriber.SubscribeAsync<InvalidateEndpointCacheEvent>(InvalidateEndpointCacheEventHandler,
                cancellationToken)
        };

        return Task.WhenAll(tasks.ToArray());
    }

    private async Task InvalidateEndpointCacheEventHandler(InvalidateEndpointCacheEvent evt)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var cacheManager = scope.ServiceProvider.GetRequiredService<IEndpointCacheManager>();
        await cacheManager.Invalidate(evt.Tag);
    }
}
