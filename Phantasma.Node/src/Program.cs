using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Phantasma.Node.Configuration;
using Serilog;
using Serilog.Events;

namespace Phantasma.Node;

public class Program
{
    public static IConfiguration Configuration { get; private set; } = new ConfigurationBuilder()
        .AddJsonFile(Settings.DefaultConfigFile, true, true)
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
            // Load config File
            LoadConfigFile(args);
            
            var culture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;

            Settings.Load(args, Configuration.GetSection("ApplicationConfiguration"));

            var API_URL = Settings.Instance.Node.APIURL;

            if (string.IsNullOrEmpty(API_URL) || !API_URL.Contains("http")) {
                throw new Exception("Invalid or missing api.url setting in config.json");   
            }

            Log.Information("Starting API: " + API_URL);
            await CreateHostBuilder(API_URL, args).Build().RunAsync().ConfigureAwait(false);

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

    public static IHostBuilder CreateHostBuilder(string url,
        string[] args
    )
    {
        return Host.CreateDefaultBuilder(args)
            .UseSerilog((
                _,
                _,
                configuration
            ) => configuration.ReadFrom.Configuration(Configuration.GetSection("ApplicationConfiguration")), true)
            .ConfigureWebHostDefaults(webBuilder => webBuilder.UseConfiguration(Configuration).UseStartup<Startup>()
            .UseUrls(new string[] { url}));
    }
    
    private static string GetDefaultConfigFile(string[] args)
    {
        if (args.Length < 1)
        {
            return Settings.DefaultConfigFile;
        }

        foreach (var arg in args)
        {
            Log.Information(arg);
            if (arg.StartsWith("--config="))
            {
                return arg.Split("=")[1];
            }
        }

        return Settings.DefaultConfigFile;
    }

    private static void LoadConfigFile(string[] args)
    {
        var configFile = GetDefaultConfigFile(args);
        
        if (!File.Exists(configFile))
        {
            Log.Error($"Expected configuration file to exist: {configFile}");

            if (configFile == Settings.DefaultConfigFile)
            {
                Log.Warning($"Copy either config_mainnet.json or config_testnet.json and rename it to {configFile}");
            }

            Environment.Exit(-1);
        }
        
        Configuration = new ConfigurationBuilder()
            .AddJsonFile(configFile, true, true)
            .AddEnvironmentVariables()
            .Build();
    }
}
