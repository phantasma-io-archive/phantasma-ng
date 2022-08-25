using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Phantasma.Business;
using Phantasma.Shared.Types;
using Phantasma.Shared.Utils;
using Phantasma.Core;
using Serilog.Events;
using Serilog;
using Serilog.Core;
using Microsoft.Extensions.Configuration;
using Phantasma.Infrastructure;

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
        public LogSettings Log { get; }
        public OracleSettings Oracle { get; }
        public SimulatorSettings Simulator { get; }
        public PerformanceMetricsSettings PerformanceMetrics { get; }

        public string _configFile;

        public static Settings Default { get; private set; }

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
                this.Node = new NodeSettings(section.GetSection("Node"));
                this.Simulator = new SimulatorSettings(section.GetSection("Simulator"));
                this.Oracle = new OracleSettings(section.GetSection("Oracle"));
                this.App = new AppSettings(section.GetSection("App"));
                this.Log = new LogSettings(section.GetSection("Log"));
                this.RPC = new RPCSettings(section.GetSection("RPC"));
                this.PerformanceMetrics = section.GetSection("PerformanceMetrics").Get<PerformanceMetricsSettings>();

                var usedPorts = new HashSet<int>();
                int expected = 0;
                usedPorts.Add(this.Node.NodePort); expected++;
                usedPorts.Add(this.Node.RestPort); expected++;
                usedPorts.Add(this.Node.RpcPort); expected++;

                if (usedPorts.Count != expected)
                {
                    throw new Exception("One or more ports are being re-used for different services, check the config");
                }
            }
            catch (Exception e)
            {
                Serilog.Log.Error(e, $"There were issues loading settings from {this._configFile}, aborting...");
                Environment.Exit(-1);
            }
        }

        public static void Load(string[] args, IConfigurationSection section)
        {
            Default = new Settings(args, section);
        }

        public string GetInteropWif(PhantasmaKeys nodeKeys, string platformName)
        {
            var nexus = NexusAPI.GetNexus();

            var genesisHash = nexus.GetGenesisHash(nexus.RootStorage);
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

    public enum NodeMode
    {
        Invalid,
        Normal,
        Proxy,
        Validator,
    }

    public class NodeSettings
    {
        public string ApiProxyUrl { get; }
        public string NexusName { get; }
        public string ProfilerPath { get; }
        public NodeMode Mode { get; }
        public string NodeWif { get; }

        public string StoragePath { get; }
        public string OraclePath { get; }
        public StorageBackendType StorageBackend;

        public bool StorageConversion { get; }
        public string VerifyStoragePath { get; }

        public bool RandomSwapData { get; } = false;

        public int NodePort { get; }
        public string NodeHost { get; }

        public bool IsValidator => Mode == NodeMode.Validator;

        public bool HasSync { get; }
        public bool HasMempool { get; }
        public bool MempoolLog { get; }
        public bool HasEvents { get; }
        public bool HasRelay { get; }
        public bool HasArchive { get; }
        public bool HasRpc { get; }
        public int RpcPort { get; } = 7077;
        public List<Address> SeedValidators { get; }

        public bool HasRest { get; }
        public int RestPort { get; } = 7078;

        public bool NexusBootstrap { get; }
        public uint GenesisTimestampUint { get; }
        public Timestamp GenesisTimestamp { get; }
        public bool ApiCache { get; }
        public bool ApiLog { get; }
        public bool Readonly { get; }

        public string SenderHost { get; } = "localhost";
        public uint SenderThreads { get; } = 8;
        public uint SenderAddressCount { get; } = 100;

        public int BlockTime { get; } = 0;
        public int MinimumFee { get; } = 100000;
        public int MinimumPow { get; } = 0;
        public int MaxGas { get; } = 10000;

        public bool WebLogs { get; }

        public string TendermintProxyHost { get; }
        public int TendermintProxyPort { get; }

        public string TendermintRPCHost { get; }
        public int TendermintRPCPort { get; }

        public string TendermintKey { get; }

        public NodeSettings(IConfigurationSection section)
        {
            this.WebLogs = section.GetValueEx<bool>("web.logs");

            this.TendermintRPCPort = section.GetValueEx<Int32>("tendermint.rpc.port");
            this.TendermintRPCHost = section.GetString("tendermint.rpc.host");

            this.TendermintProxyPort = section.GetValueEx<Int32>("tendermint.proxy.port");
            this.TendermintProxyHost = section.GetString("tendermint.proxy.host");

            this.TendermintKey = section.GetString("tendermint.key");

            this.BlockTime = section.GetValueEx<Int32>("block.time");
            this.MinimumFee = section.GetValueEx<Int32>("minimum.fee");
            this.MinimumPow = section.GetValueEx<Int32>("minimum.pow");
            this.MaxGas = section.GetValueEx<Int32>("max.gas");

            int maxPow = 5; // should be a constant like MinimumBlockTime
            if (this.MinimumPow < 0 || this.MinimumPow > maxPow)
            {
                throw new Exception("Proof-Of-Work difficulty has to be between 1 and 5");
            }

            this.ApiProxyUrl = section.GetString("api.proxy.url");

            if (string.IsNullOrEmpty(this.ApiProxyUrl))
            {
                this.ApiProxyUrl = null;
            }

            this.SeedValidators = section.GetSection("seed.validators").AsEnumerable()
                .Where(p => p.Value != null)
                .Select(p => Address.FromText(p.Value))
                .ToList();

            this.Mode = section.GetValueEx<NodeMode>("node.mode", NodeMode.Invalid);
            if (this.Mode == NodeMode.Invalid)
            {
                throw new Exception("Unknown node mode specified");
            }

            this.NexusName = section.GetString("nexus.name");
            this.NodeWif = section.GetString("node.wif");
            this.StorageConversion = section.GetValueEx<bool>("convert.storage");
            this.ApiLog = section.GetValueEx<bool>("api.log");

            this.NodePort = section.GetValueEx<Int32>("node.port");
            this.NodeHost = section.GetString("node.host", "localhost");

            this.ProfilerPath = section.GetString("profiler.path");
            if (string.IsNullOrEmpty(this.ProfilerPath)) this.ProfilerPath = null;

            this.HasSync = section.GetValueEx<bool>("has.sync");
            this.HasMempool = section.GetValueEx<bool>("has.mempool");
            this.MempoolLog = section.GetValueEx<bool>("mempool.log");
            this.HasEvents = section.GetValueEx<bool>("has.events");
            this.HasRelay = section.GetValueEx<bool>("has.relay");
            this.HasArchive = section.GetValueEx<bool>("has.archive");

            this.HasRpc = section.GetValueEx<bool>("has.rpc");
            this.RpcPort = section.GetValueEx<Int32>("rpc.port");
            this.HasRest = section.GetValueEx<bool>("has.rest");
            this.RestPort = section.GetValueEx<Int32>("rest.port");

            this.NexusBootstrap = section.GetValueEx<bool>("nexus.bootstrap");
            this.GenesisTimestampUint = section.GetValueEx<UInt32>("genesis.timestamp");
            this.GenesisTimestamp = new Timestamp((this.GenesisTimestampUint == 0) ? Timestamp.Now.Value : this.GenesisTimestampUint);
            this.ApiCache = section.GetValueEx<bool>("api.cache");
            this.Readonly = section.GetValueEx<bool>("readonly");

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
