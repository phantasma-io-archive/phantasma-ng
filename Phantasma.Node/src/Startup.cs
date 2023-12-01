using System;
using System.Linq;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Foundatio.Caching;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Messaging;
using Foundatio.Serializer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Phantasma.Infrastructure.API;
using Phantasma.Infrastructure.API.Controllers;
using Phantasma.Infrastructure.API.Interfaces;
using Phantasma.Node.Authentication;
using Phantasma.Node.Caching;
using Phantasma.Node.Converters;
using Phantasma.Node.Events;
using Phantasma.Node.Hosting;
using Phantasma.Node.Metrics;
using Phantasma.Node.Middleware;
using Phantasma.Node.Swagger;
using Serilog;
using StackExchange.Redis;

namespace Phantasma.Node;

public class Startup
{
    public Startup(
        IConfiguration configuration
    )
    {
        Log.Information("Startup...");

        Configuration = configuration;

        Thread nodeThread = new Thread(() =>
        {
            Log.Information("Initialising Node");
            var node = new Node();
            Log.Information("Starting node");
            node.Start();
        });
        nodeThread.Start();
        Log.Information("Startup finished");
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(
        IServiceCollection services
    )
    {
        Log.Information("Starting services configuration...");

        services.AddStartupActionToWaitForHealthChecks("Critical");
        services.AddHealthChecks()
            .AddCheckForStartupActions();
        services.AddAuthorization();
        services.AddAuthentication().AddBasicAuthentication();
        services.AddHttpContextAccessor();
        services.AddTransient<IPrincipal>(sp => sp.GetRequiredService<IHttpContextAccessor>().HttpContext?.User);
        services.AddEndpointsApiExplorer();
        services.AddHealthChecks();
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policyBuilder => { policyBuilder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod(); });
        });
        services.AddMvc()
            .AddJsonOptions(options =>
            {
                // Ensure settings here match GetDefaultSerializerOptions()
                options.JsonSerializerOptions.IncludeFields = true;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.Converters.Add(new EnumerableJsonConverterFactory());
            });

        string redis = Configuration.GetValue<string>("Redis");
        if (!string.IsNullOrEmpty(redis))
        {
            Log.Information("Using Redis cache");
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(ConfigurationOptions.Parse(redis)));
            services.AddSingleton<ICacheClient>(sp => new RedisCacheClient(optionsBuilder =>
                optionsBuilder.ConnectionMultiplexer(sp.GetRequiredService<IConnectionMultiplexer>())
                    .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                    .Serializer(new SystemTextJsonSerializer(GetDefaultSerializerOptions()))));
        }
        else
        {
            Log.Information("Using in-memory cache");
            services.AddSingleton<ICacheClient>(sp => new InMemoryCacheClient(optionsBuilder =>
                optionsBuilder.CloneValues(true)
                    .MaxItems(10000)
                    .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                    .Serializer(new SystemTextJsonSerializer(GetDefaultSerializerOptions()))));
        }

        services.AddSingleton<IMessageBus>(sp => new InMemoryMessageBus(optionsBuilder =>
            optionsBuilder.LoggerFactory(sp.GetRequiredService<ILoggerFactory>())));
        services.AddSingleton<IMessagePublisher>(sp => sp.GetRequiredService<IMessageBus>());
        services.AddSingleton<IMessageSubscriber>(sp => sp.GetRequiredService<IMessageBus>());
        services.AddScoped<IEndpointCacheManager, EndpointCacheManager>();
        services.AddSingleton<IEndpointMetrics, EndpointMetrics>();
        services.AddSingleton<IEventBus, EventBus>();
        services.AddHostedService<EventBusBackgroundService>();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1",
                new OpenApiInfo
                {
                    Title = "Phantasma API",
                    Description = "",
                    Version = "v1",
                    Contact = new OpenApiContact { Name = "Phantasma", Url = new Uri("https://phantasma.io") }
                });
            c.SwaggerDoc("v1-internal",
                new OpenApiInfo
                {
                    Title = "Phantasma API (Internal)",
                    Description = "",
                    Version = "v1-internal",
                    Contact = new OpenApiContact { Name = "Phantasma", Url = new Uri("https://phantasma.io") }
                });
            c.DocumentFilter<InternalDocumentFilter>();
        });

        Log.Information("Loading controllers...");
        var assembly = System.Reflection.Assembly.Load("Phantasma.Infrastructure");

        var controllers = assembly.GetTypes()
                .Where(type => typeof(BaseControllerV1).IsAssignableFrom(type));
        Log.Information($"Found {controllers.Count()} controllers");

        services.AddMvc().AddApplicationPart(assembly).AddControllersAsServices();

        services.AddScoped<IAPIService, APIChainService>();
        services.AddScoped<IAPIService, APIExplorerService>();

        Log.Information("Finished services configuration");
    }

    public void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env
    )
    {
        Log.Information("Starting app configuration...");

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseWaitForStartupActionsBeforeServingRequests();
        app.UseSerilogRequestLogging();
        app.UseCors();
        //app.UseExceptionHandler();
        app.UseMiddleware<ErrorLoggingMiddleware>();
        app.UseMiddleware<PerformanceMiddleware>();
        app.UseMiddleware<CacheMiddleware>();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<SwaggerAuthorizationMiddleware>();
        app.UseSwagger();
        app.UseHttpsRedirection();
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "swagger";
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
        });
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "swagger-internal";
            options.SwaggerEndpoint("/swagger/v1-internal/swagger.json", "API v1 (Internal)");
        });
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapHealthChecks("/health");
            endpoints.MapControllers();
        });

        app.UseMiddleware<APIServiceMiddleware>();
        
        Log.Information("Finished app configuration");
    }

    private static JsonSerializerOptions GetDefaultSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            IncludeFields = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new EnumerableJsonConverterFactory() }
        };
    }
}
