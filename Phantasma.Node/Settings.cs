using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Phantasma.Business.Blockchain;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;
using Phantasma.Infrastructure.API;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Phantasma.Node
{
    public static class ConfigurationBinder
    {
        public static T GetValueEx<T>(this IConfiguration configuration, string key, T defaultValue = default(T)) where T : struct, IConvertible
        {
            if (typeof(T) == typeof(Int32))
            {
                // If command line arguments are initialized,
                // we try to get value from there, as it has a priority.
                if (CliArgumets.Default != null && CliArgumets.Default.HasValue(key))
                {
                    return (T)(object)CliArgumets.Default.GetInt(key);
                }

                // Otherwise we proceed with configuration's data.
                return (T)(object)configuration.GetValue<T>(key);
            }

            if (typeof(T) == typeof(UInt32))
            {
                // If command line arguments are initialized,
                // we try to get value from there, as it has a priority.
                if (CliArgumets.Default != null && CliArgumets.Default.HasValue(key))
                {
                    return (T)(object)CliArgumets.Default.GetUInt(key);
                }

                // Otherwise we proceed with configuration's data.
                return (T)(object)configuration.GetValue<T>(key);
            }

            if (typeof(T) == typeof(bool))
            {
                // If command line arguments are initialized,
                // we try to get value from there, as it has a priority.
                if (CliArgumets.Default != null && CliArgumets.Default.HasValue(key))
                {
                    return (T)(object)CliArgumets.Default.GetBool(key);
                }

                // Otherwise we proceed with configuration's data.
                return (T)(object)configuration.GetValue<T>(key);
            }

            if (typeof(T).IsEnum)
            {
                // If command line arguments are initialized,
                // we try to get value from there, as it has a priority.
                if (CliArgumets.Default != null && CliArgumets.Default.HasValue(key))
                {
                    var stringValue = CliArgumets.Default.GetString(key);
                    Enum.Parse<T>(stringValue);
                    return (T)(object)CliArgumets.Default.GetEnum<T>(key);
                }

                // Otherwise we proceed with configuration's data.
                return (T)(object)configuration.GetValue<T>(key);
            }

            throw new Exception($"Type {typeof(T)} is not supported");
        }
        public static string GetString(this IConfiguration configuration, string key, string defaultValue = null)
        {
            // If command line arguments are initialized,
            // we try to get value from there, as it has a priority.
            if (CliArgumets.Default != null && CliArgumets.Default.HasValue(key))
            {
                return CliArgumets.Default.GetString(key);
            }

            // Otherwise we proceed with configuration's data.
            return configuration.GetValue<string>(key, defaultValue);
        }
    }

    internal class CliArgumets
    {
        public static Arguments Default { get; set; }

        public CliArgumets(string[] args)
        {
            Default = new Arguments(args);
        }
    }

    internal class Settings
    {
        public RPCSettings RPC { get; }
        public NodeSettings Node { get; }
        public AppSettings App { get; }
        public List<ValidatorSettings> Validators { get; }
        public LogSettings Log { get; }
        public WebhookSettings WebhookSetting { get; }
        public OracleSettings Oracle { get; }
        public SimulatorSettings Simulator { get; }
        public PerformanceMetricsSettings PerformanceMetrics { get; }

        public string _configFile;

        public static Settings Instance { get; private set; }

        private Settings(string[] args, IConfigurationSection section)
        {
            new CliArgumets(args);

            var levelSwitchConsole = new LoggingLevelSwitch
            {
                MinimumLevel = LogEventLevel.Verbose
            };

            var logConfig = new LoggerConfiguration()
                .WriteTo.Console(outputTemplate: "{Timestamp:u} {Timestamp:ffff} [{Level:u3}] <{ThreadId}> {Message:lj}{NewLine}{Exception}",
                    levelSwitch: levelSwitchConsole);

            Serilog.Log.Logger = logConfig.CreateLogger();

            var defaultConfigFile = "config.json";

            this._configFile = defaultConfigFile;

            if (!File.Exists(_configFile))
            {
                Serilog.Log.Error($"Expected configuration file to exist: {this._configFile}");

                if (this._configFile == defaultConfigFile)
                {
                    Serilog.Log.Warning($"Copy either config_mainnet.json or config_testnet.json and rename it to {this._configFile}");
                }

                Environment.Exit(-1);
            }

            try
            {
                this.Node = new NodeSettings(args, section.GetSection("Node"));
                this.Simulator = new SimulatorSettings(section.GetSection("Simulator"));
                this.Oracle = new OracleSettings(section.GetSection("Oracle"));
                this.App = new AppSettings(section.GetSection("App"));
                this.Log = new LogSettings(section.GetSection("Log"));
                this.RPC = new RPCSettings(section.GetSection("RPC"));
                this.Validators = SetupValidatorsList(section.GetSection("Validators"));
                this.WebhookSetting = new WebhookSettings(section.GetSection("Webhook"));
                this.PerformanceMetrics = section.GetSection("PerformanceMetrics").Get<PerformanceMetricsSettings>();
            }
            catch (Exception e)
            {
                Serilog.Log.Error(e, $"There were issues loading settings from {this._configFile}, aborting...");
                Environment.Exit(-1);
            }
        }

        private static List<ValidatorSettings> SetupValidatorsList(IConfigurationSection section)
        {
            var validators = new List<ValidatorSettings>();

            var validatorsArray = section.GetChildren();
            //var validatorConfig = .Get<ValidatorConfig[]>().ToList();

            foreach (var validator in validatorsArray)
            {
                var validatorSettings = new ValidatorConfig(validator);
                validators.Add(new ValidatorSettings(validatorSettings.Address, validatorSettings.Name, validatorSettings.Host, validatorSettings.Port));
            }

            return validators;
        }

        public static void Load(string[] args, IConfigurationSection section)
        {
            Instance = new Settings(args, section);
        }

        public string GetInteropWif(PhantasmaKeys nodeKeys, string platformName)
        {
            var nexus = NexusAPI.GetNexus();

            var genesisHash = nexus.GetGenesisHash(nexus.RootChain.StorageFactory.MainStorage);
            var interopKeys = InteropUtils.GenerateInteropKeys(nodeKeys, genesisHash, platformName);
            var defaultWif = interopKeys.ToWIF();

            string customWIF = null;

            switch (platformName)
            {
                case "neo":
                    customWIF = this.Oracle.NeoWif;
                    break;


                case "ethereum":
                    customWIF = this.Oracle.EthWif;
                    break;
            }

            var result = !string.IsNullOrEmpty(customWIF) ? customWIF: defaultWif;

            if (result != null && result.Length == 64)
            {
                var temp = new PhantasmaKeys(Base16.Decode(result));
                result = temp.ToWIF();
            }

            return result;
        }
    }


    public enum StorageBackendType
    {
        CSV,
        RocksDB,
    }

    public class NodeSettings
    {
        public string NexusName { get; }
        public string ProfilerPath { get; }
        public string StoragePath { get; }
        public string OraclePath { get; }
        public StorageBackendType StorageBackend;

        public bool StorageConversion { get; }
        public string VerifyStoragePath { get; }

        public bool RandomSwapData { get; } = false;

        public bool HasSync { get; }
        public bool HasMempool { get; }
        public bool MempoolLog { get; }
        public bool HasEvents { get; }
        public bool HasRelay { get; }
        public bool HasArchive { get; }
        public List<Address> SeedValidators { get; }
        public string APIURL { get; } = "http://localhost:5101";

        public bool NexusBootstrap { get; }
        public bool ApiCache { get; }
        public bool ApiLog { get; }

        public string SenderHost { get; } = "localhost";
        public uint SenderThreads { get; } = 8;
        public uint SenderAddressCount { get; } = 100;

        public int BlockTime { get; } = 0;
        public int MinimumFee { get; } = 100000;
        public int MinimumPow { get; } = 0;
        public int MaxGas { get; } = 10000;

        public bool WebLogs { get; }

        public string TendermintPath { get; }
        public string TendermintHome { get; }
        public string TendermintGenesis { get; }
        public string TendermintChainID { get; }
        public string TendermintPeers { get; }

        public string TendermintProxyHost { get; }
        public int TendermintProxyPort { get; }

        public string TendermintRPCHost { get; }
        public int TendermintRPCPort { get; }
        public string TendermintKey { get; }

        public NodeSettings(string[] args, IConfigurationSection section)
        {
            this.WebLogs = section.GetValueEx<bool>("web.logs");

            this.TendermintPath = section.GetString("tendermint.path", "");
            this.TendermintHome = section.GetString("tendermint.home", "");
            this.TendermintGenesis = section.GetString("tendermint.genesis", "");
            this.TendermintChainID = section.GetString("tendermint.chainid", "");
            this.TendermintPeers = section.GetString("tendermint.peers", "");

            this.TendermintRPCPort = section.GetValueEx<Int32>("tendermint.rpc.port");
            this.TendermintRPCHost = section.GetString("tendermint.rpc.host");

            this.TendermintProxyPort = section.GetValueEx<Int32>("tendermint.proxy.port");
            this.TendermintProxyHost = section.GetString("tendermint.proxy.host");

            this.TendermintKey = section.GetString("tendermint.key");

            var keyTag = "--tendermint.key=";
            var keyOverride = args.FirstOrDefault(x => x.StartsWith(keyTag));
            if (!string.IsNullOrEmpty(keyOverride))
            {
                this.TendermintKey = keyOverride.Substring(keyTag.Length);
            }

            this.BlockTime = section.GetValueEx<Int32>("block.time");
            this.MinimumFee = section.GetValueEx<Int32>("minimum.fee");
            this.MinimumPow = section.GetValueEx<Int32>("minimum.pow");
            this.MaxGas = section.GetValueEx<Int32>("max.gas");

            int maxPow = 5; // should be a constant like MinimumBlockTime
            if (this.MinimumPow < 0 || this.MinimumPow > maxPow)
            {
                throw new Exception("Proof-Of-Work difficulty has to be between 1 and 5");
            }

            this.SeedValidators = section.GetSection("seed.validators").AsEnumerable()
                .Where(p => p.Value != null)
                .Select(p => Address.FromText(p.Value))
                .ToList();
            

            if (this.SeedValidators.Count < 3)
            {
                throw new Exception("Seed.validators list not set or too small");
            }

            this.NexusName = section.GetString("nexus.name");
            this.StorageConversion = section.GetValueEx<bool>("convert.storage");
            this.ApiLog = section.GetValueEx<bool>("api.log");

            this.ProfilerPath = section.GetString("profiler.path");
            if (string.IsNullOrEmpty(this.ProfilerPath)) this.ProfilerPath = null;

            this.HasSync = section.GetValueEx<bool>("has.sync");
            this.HasMempool = section.GetValueEx<bool>("has.mempool");
            this.MempoolLog = section.GetValueEx<bool>("mempool.log");
            this.HasEvents = section.GetValueEx<bool>("has.events");
            this.HasRelay = section.GetValueEx<bool>("has.relay");
            this.HasArchive = section.GetValueEx<bool>("has.archive");

            this.APIURL = section.GetString("api.url");

            this.NexusBootstrap = section.GetValueEx<bool>("nexus.bootstrap");
            this.ApiCache = section.GetValueEx<bool>("api.cache");

            this.SenderHost = section.GetString("sender.host");
            this.SenderThreads = section.GetValueEx<UInt32>("sender.threads");
            this.SenderAddressCount = section.GetValueEx<UInt32>("sender.address.count");

            var defaultStoragePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/Storage/";
            var defaultOraclePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/Oracle/";

            this.StoragePath = section.GetString("storage.path");
            if (string.IsNullOrEmpty(this.StoragePath))
            {
                this.StoragePath = defaultStoragePath;
            }

            if (!StoragePath.EndsWith("" + Path.DirectorySeparatorChar))
            {
                StoragePath += Path.DirectorySeparatorChar;
            }

            StoragePath = Path.GetFullPath(StoragePath);

            this.VerifyStoragePath = section.GetString("verify.storage.path");
            if (string.IsNullOrEmpty(this.VerifyStoragePath))
            {
                this.VerifyStoragePath = defaultStoragePath;
            }

            this.OraclePath = section.GetString("oracle.path");
            if (string.IsNullOrEmpty(this.OraclePath))
            {
                this.OraclePath = defaultOraclePath;
            }

            var backend = section.GetString("storage.backend");

            if (!Enum.TryParse<StorageBackendType>(backend, true, out this.StorageBackend))
            {
                throw new Exception("Unknown storage backend: " + backend);
            }

            if (this.StorageConversion)
            {
                this.RandomSwapData = section.GetValueEx<bool>("random.Swap.data");
            }
        }
    }

    public class Contract
    {
        public string symbol { get; set; }
        public string hash { get; set; }
    }

    public class FeeUrl
    {
        public string url { get; set; }
        public string feeHeight { get; set; }
        public uint feeIncrease { get; set; }
    }

    public class PricerSupportedToken
    {
        public string ticker { get; set; }
        public string coingeckoId { get; set; }
        public string cryptocompareId { get; set; }
    }

    public class OracleSettings
    {
        public string NeoscanUrl { get; }
        public List<string> NeoRpcNodes { get; }
        public List<string> EthRpcNodes { get; }
        public List<FeeUrl> EthFeeURLs { get; }
        public bool PricerCoinGeckoEnabled { get; } = true;
        public List<PricerSupportedToken> PricerSupportedTokens { get; }
        public string CryptoCompareAPIKey { get; }
        public string Swaps { get; }
        public string SwapColdStorageNeo { get; }
        public string PhantasmaInteropHeight { get; } = "0";
        public string NeoInteropHeight { get; } = "4261049";
        public string EthInteropHeight { get; }
        public string NeoWif { get; }
        public string EthWif { get; }
        public uint EthConfirmations { get; }
        public uint EthGasLimit { get; }
        public bool NeoQuickSync { get; } = true;

        public OracleSettings(IConfigurationSection section)
        {
            this.NeoscanUrl = section.GetString("neoscan.api");

            this.NeoRpcNodes = section.GetSection("neo.rpc.specific.nodes").AsEnumerable().Where(x => x.Value != null).Select(x => x.Value).ToList();

            if (this.NeoRpcNodes.Count() == 0)
            {
                this.NeoRpcNodes = section.GetSection("neo.rpc.nodes").AsEnumerable().Where(x => x.Value != null).Select(x => x.Value).ToList();
                this.NeoQuickSync = false;
            }

            this.EthRpcNodes = section.GetSection("eth.rpc.nodes").AsEnumerable().Where(x => x.Value != null).Select(x => x.Value).ToList();

            this.EthFeeURLs = section.GetSection("eth.fee.urls").Get<FeeUrl[]>().ToList();

            this.PricerCoinGeckoEnabled = section.GetValueEx<bool>("pricer.coingecko.enabled");
            this.PricerSupportedTokens = section.GetSection("pricer.supportedtokens").Get<PricerSupportedToken[]>().ToList();

            this.EthConfirmations = section.GetValueEx<UInt32>("eth.block.confirmations");
            this.EthGasLimit = section.GetValueEx<UInt32>("eth.gas.limit");
            this.CryptoCompareAPIKey = section.GetString("crypto.compare.key");
            this.Swaps = section.GetString("swap.platforms");
            this.SwapColdStorageNeo = section.GetString("swap.coldStorage.neo");
            this.PhantasmaInteropHeight = section.GetString("phantasma.interop.height");
            this.NeoInteropHeight = section.GetString("neo.interop.height");
            this.EthInteropHeight = section.GetString("eth.interop.height");
            this.NeoWif = section.GetString("neo.wif");
            if (string.IsNullOrEmpty(this.NeoWif))
            {
                this.NeoWif = null;
            }
            this.EthWif = section.GetString("eth.wif");
            if (string.IsNullOrEmpty(this.EthWif))
            {
                this.EthWif = null;
            }
        }
    }

    public class SimulatorSettings
    {
        public bool Enabled { get; }
        public bool Blocks { get; }

        public SimulatorSettings(IConfigurationSection section)
        {
            this.Enabled = section.GetValueEx<bool>("simulator.enabled");
            this.Blocks = section.GetValueEx<bool>("simulator.generate.blocks");
        }
    }

    public class LogSettings
    {
        public string LogName { get; }
        public string LogPath { get; }
        public LogEventLevel FileLevel { get; }
        public LogEventLevel ShellLevel { get; }

        public LogSettings(IConfigurationSection section)
        {
            this.LogName = section.GetString("file.name", "spook.log");
            this.LogPath = section.GetString("file.path", Path.GetTempPath());
            this.FileLevel = section.GetValueEx<LogEventLevel>("file.level", LogEventLevel.Verbose);
            this.ShellLevel = section.GetValueEx<LogEventLevel>("shell.level", LogEventLevel.Information);
        }
    }

    public class AppSettings
    {
        public bool UseShell { get; }
        public string AppName { get; }
        public bool NodeStart { get; }
        public string History { get; }
        public string Config { get; }
        public string Prompt { get; }

        public AppSettings(IConfigurationSection section)
        {
            this.UseShell = section.GetValueEx<bool>("shell.enabled");
            this.AppName = section.GetString("app.name");
            this.NodeStart = section.GetValueEx<bool>("node.start");
            this.History = section.GetString("history");
            this.Config = section.GetString("config");
            this.Prompt = section.GetString("prompt");
        }
    }

    public class WebhookSettings
    {
        public string Token { get; }
        public string Channel { get; }
        public string Prefix { get; }
        
        public WebhookSettings(IConfigurationSection section)
        {
            this.Token = section.GetString("webhook.token");
            this.Channel = section.GetString("webhook.channel");
            this.Prefix = section.GetString("webhook.prefix");
            
            Webhook.Token = Token; 
            Webhook.Channel = Channel; 
            Webhook.Prefix = Prefix; 
            Log.Logger.Information($"Webhook settings loaded");
            Log.Logger.Information($"Webhook Token {Webhook.Token}");
            Log.Logger.Information($"Webhook Channel {Webhook.Channel}");
            Log.Logger.Information($"Webhook Prefix {Webhook.Prefix}");
        }
    }

    public class ValidatorConfig
    {
        public Address Address { get; }
        public string Name { get; }
        public string Host { get; }
        public uint Port { get; }
        public string URL { get; }
        
        public ValidatorConfig(IConfigurationSection section)
        {
            this.Address = Address.FromText(section.GetString("validator.address"));
            this.Name = section.GetString("validator.name");
            this.Host = section.GetString("validator.api.host");
            this.Port = section.GetValueEx<uint>("validator.api.port");
            this.URL = Host + ":" + Port;
        }
    }

    public class RPCSettings
    {
        public string Address { get; }
        public uint Port { get; }

        public RPCSettings(IConfigurationSection section)
        {
            this.Address = section.GetString("rpc.address");
            this.Port = section.GetValueEx<UInt32>("rpc.port");
        }
    }

    public class PerformanceMetricsSettings
    {
        public bool CountsEnabled { get; set; }
        public bool AveragesEnabled { get; set; }
        public int LongRunningRequestThreshold { get; set; } = 500;
    }
}
