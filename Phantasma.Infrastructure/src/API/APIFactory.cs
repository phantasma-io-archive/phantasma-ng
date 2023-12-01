using System;
using Microsoft.Extensions.DependencyInjection;
using Phantasma.Infrastructure.API.Interfaces;

namespace Phantasma.Infrastructure.API;

public class APIFactory
{
    private readonly IServiceProvider _serviceProvider;

    public APIFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IAPIService Create(string name)
    {
        return name switch
        {
            "Chain" => _serviceProvider.GetRequiredService<APIChainService>(),
            "Explorer" => _serviceProvider.GetRequiredService<APIExplorerService>(),
            _ => throw new ArgumentException($"Unknown service name: {name}")
        };
    }
}

