using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Phantasma.Node;

public class Program
{
    public static IConfiguration Configuration { get; } = new ConfigurationBuilder()
        .AddJsonFile("config.json", true, true)
        .AddEnvironmentVariables()
        .Build();

    public static async Task<int> Main(
        string[] args
    )
    {
        Log.Logger = new LoggerConfiguration().MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            var culture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;

            Settings.Load(args, Configuration.GetSection("ApplicationConfiguration"));

            Log.Information("Starting host");
            await CreateHostBuilder(args).Build().RunAsync().ConfigureAwait(false);

            return 0;
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Host terminated unexpectedly");

            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static IHostBuilder CreateHostBuilder(
        string[] args
    )
    {
        return Host.CreateDefaultBuilder(args)
            .UseSerilog((
                _,
                _,
                configuration
            ) => configuration.ReadFrom.Configuration(Configuration.GetSection("ApplicationConfiguration")), true)
            .ConfigureWebHostDefaults(webBuilder => webBuilder.UseConfiguration(Configuration).UseStartup<Startup>());
    }
}
