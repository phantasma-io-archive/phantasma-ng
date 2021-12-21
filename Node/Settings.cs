using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Phantasma.Business;
using Phantasma.Shared.Types;
using Phantasma.Shared.Utils;
using Phantasma.Core;
using LunarLabs.Parser;
using LunarLabs.Parser.JSON;
using Serilog.Events;
using Serilog;
using Serilog.Core;

namespace Phantasma.Spook
{
    public class SpookSettings
    {
        public RPCSettings RPC { get; }
        public NodeSettings Node { get; }
        public AppSettings App { get; }
        public LogSettings Log { get; }
        public OracleSettings Oracle { get; }
        public SimulatorSettings Simulator { get; }

        private Arguments _settings { get; }
        public string _configFile;

        public SpookSettings(string[] args)
        {
            _settings = new Arguments(args);

            var levelSwitchConsole = new LoggingLevelSwitch
            {
                MinimumLevel = LogEventLevel.Verbose
            };

            var logConfig = new LoggerConfiguration()
                .WriteTo.Console(outputTemplate: "{Timestamp:u} {Timestamp:ffff} [{Level:u3}] <{ThreadId}> {Message:lj}{NewLine}{Exception}",
                    levelSwitch: levelSwitchConsole);

            var logger = logConfig.CreateLogger();

            var defaultConfigFile = "config.json";

            this._configFile = _settings.GetString("conf", defaultConfigFile);

            if (!File.Exists(_configFile))
            {
                logger.Error($"Expected configuration file to exist: {this._configFile}");

                if (this._configFile == defaultConfigFile)
                {
                    logger.Warning($"Copy either config_mainnet.json or config_testnet.json and rename it to {this._configFile}");
                }

                Environment.Exit(-1);
            }

            try
            {
                var json = File.ReadAllText(_configFile);
                var root = JSONReader.ReadFromString(json);
                root = root["ApplicationConfiguration"];

                this.Node = new NodeSettings(_settings, FindSection(root, "Node", true));
                this.Simulator = new SimulatorSettings(_settings, FindSection(root, "Simulator", false));
                this.Oracle = new OracleSettings(_settings, FindSection(root, "Oracle", true));
                this.App = new AppSettings(_settings, FindSection(root, "App", true));
                this.Log = new LogSettings(_settings, FindSection(root, "Log", false));
                this.RPC = new RPCSettings(_settings, FindSection(root, "RPC", true));

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
                logger.Error(e.Message);
                logger.Warning($"There were issues loading settings from {this._configFile}, aborting...");
                Environment.Exit(-1);
            }
        }

        private DataNode FindSection(DataNode root, string name, bool required)
        {
            var section = root.GetNode(name);
            if (section == null)
            {
                if (required)
                {
                    throw new Exception("Settings is missing section: " + name);
                }
                else
                {
                    section = DataNode.CreateObject(name);
                }
            }

            return section;
        }

        public string GetInteropWif(Nexus nexus, PhantasmaKeys nodeKeys, string platformName)
        {
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
        public List<string> Seeds { get; }

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

        public int BlockTime { get; } = Mempool.MinimumBlockTime;
        public int MinimumFee { get; } = 100000;
        public int MinimumPow { get; } = 0;

        public bool WebLogs { get; }

        public NodeSettings(Arguments settings, DataNode section)
        {
            this.WebLogs = settings.GetBool("web.log", section.GetBool("web.logs"));

            this.BlockTime = settings.GetInt("block.time", section.GetInt32("block.time"));
            this.MinimumFee = settings.GetInt("minimum.fee", section.GetInt32("minimum.fee"));
            this.MinimumPow = settings.GetInt("minimum.pow", section.GetInt32("minimum.pow"));

            int maxPow = 5; // should be a constanct like MinimumBlockTime
            if (this.MinimumPow < 0 || this.MinimumPow > maxPow)
            {
                throw new Exception("Proof-Of-Work difficulty has to be between 1 and 5");
            }

            this.ApiProxyUrl = settings.GetString("api.proxy.url", section.GetString("api.proxy.url"));

            if (string.IsNullOrEmpty(this.ApiProxyUrl))
            {
                this.ApiProxyUrl = null;
            }

            this.Seeds = section.GetNode("seeds").Children.Select(p => p.Value).ToList();

            this.Mode = settings.GetEnum<NodeMode>("node.mode", section.GetEnum<NodeMode>("node.mode", NodeMode.Invalid));
            if (this.Mode == NodeMode.Invalid)
            {
                throw new Exception("Unknown node mode specified");
            }

            this.NexusName = settings.GetString("nexus.name", section.GetString("nexus.name"));
            this.NodeWif = settings.GetString("node.wif", section.GetString("node.wif"));
            this.StorageConversion = settings.GetBool("convert.storage", section.GetBool("convert.storage"));
            this.ApiLog = settings.GetBool("api.log", section.GetBool("api.log"));

            this.NodePort = settings.GetInt("node.port", section.GetInt32("node.port"));
            this.NodeHost = settings.GetString("node.host", section.GetString("node.host", "localhost"));

            this.ProfilerPath = settings.GetString("profiler.path", section.GetString("profiler.path"));
            if (string.IsNullOrEmpty(this.ProfilerPath)) this.ProfilerPath = null;

            this.HasSync = settings.GetBool("has.sync", section.GetBool("has.sync"));
            this.HasMempool = settings.GetBool("has.mempool", section.GetBool("has.mempool"));
            this.MempoolLog = settings.GetBool("mempool.log", section.GetBool("mempool.log"));
            this.HasEvents = settings.GetBool("has.events", section.GetBool("has.events"));
            this.HasRelay = settings.GetBool("has.relay", section.GetBool("has.relay"));
            this.HasArchive = settings.GetBool("has.archive", section.GetBool("has.archive"));

            this.HasRpc = settings.GetBool("has.rpc", section.GetBool("has.rpc"));
            this.RpcPort = settings.GetInt("rpc.port", section.GetInt32("rpc.port"));
            this.HasRest = settings.GetBool("has.rest", section.GetBool("has.rest"));
            this.RestPort = settings.GetInt("rest.port", section.GetInt32("rest.port"));

            this.NexusBootstrap = settings.GetBool("nexus.bootstrap", section.GetBool("nexus.bootstrap"));
            this.GenesisTimestampUint = settings.GetUInt("genesis.timestamp", section.GetUInt32("genesis.timestamp"));
            this.GenesisTimestamp = new Timestamp((this.GenesisTimestampUint == 0) ? Timestamp.Now.Value : this.GenesisTimestampUint);
            this.ApiCache = settings.GetBool("api.cache", section.GetBool("api.cache"));
            this.Readonly = settings.GetBool("readonly", section.GetBool("readonly"));

            this.SenderHost = settings.GetString("sender.host", section.GetString("sender.host"));
            this.SenderThreads = settings.GetUInt("sender.threads", section.GetUInt32("sender.threads"));
            this.SenderAddressCount = settings.GetUInt("sender.address.count", section.GetUInt32("sender.address.count"));

            var defaultStoragePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/Storage/";
            var defaultOraclePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/Oracle/";

            this.StoragePath = settings.GetString("storage.path", section.GetString("storage.path"));
            if (string.IsNullOrEmpty(this.StoragePath))
            {
                this.StoragePath = defaultStoragePath;
            }

            if (!StoragePath.EndsWith("" + Path.DirectorySeparatorChar))
            {
                StoragePath += Path.DirectorySeparatorChar;
            }

            StoragePath = Path.GetFullPath(StoragePath);

            this.VerifyStoragePath = settings.GetString("verify.storage.path", section.GetString("verify.storage.path"));
            if (string.IsNullOrEmpty(this.VerifyStoragePath))
            {
                this.VerifyStoragePath = defaultStoragePath;
            }

            this.OraclePath = settings.GetString("oracle.path", section.GetString("oracle.path"));
            if (string.IsNullOrEmpty(this.OraclePath))
            {
                this.OraclePath = defaultOraclePath;
            }

            var backend = settings.GetString("storage.backend", section.GetString("storage.backend"));
            
            if (!Enum.TryParse<StorageBackendType>(backend, true, out this.StorageBackend))
            {
                throw new Exception("Unknown storage backend: " + backend);
            }

            if (this.StorageConversion)
            {
                this.RandomSwapData = section.GetBool("random.Swap.data");
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
        public FeeUrl(string url, string feeHeight, uint feeIncrease)
        {
            this.url = url;
            this.feeHeight = feeHeight;
            this.feeIncrease = feeIncrease;
        }

        public string url { get; set; }
        public string feeHeight { get; set; }
        public uint feeIncrease { get; set; }

        public static FeeUrl FromNode(DataNode node)
        {
            var url = node.GetString("url");
            var feeHeight = node.GetString("feeHeight");
            var feeIncrease = node.GetUInt32("feeIncrease");
            return new FeeUrl(url, feeHeight, feeIncrease);
        }
    }

    public class PricerSupportedToken
    {
        public PricerSupportedToken(string ticker, string coingeckoId, string cryptocompareId)
        {
            this.ticker = ticker;
            this.coingeckoId = coingeckoId;
            this.cryptocompareId = cryptocompareId;
        }

        public string ticker { get; set; }
        public string coingeckoId { get; set; }
        public string cryptocompareId { get; set; }

        public static PricerSupportedToken FromNode(DataNode node)
        {
            var ticker = node.GetString("ticker");
            var coingeckoId = node.GetString("coingeckoid");
            var cryptocompareId = node.GetString("cryptocompareid");
            return new PricerSupportedToken(ticker, coingeckoId, cryptocompareId);
        }
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
        
        public OracleSettings(Arguments settings, DataNode section)
        {
            this.NeoscanUrl = settings.GetString("neoscan.api", section.GetString("neoscan.api"));
            this.NeoRpcNodes = section.GetNode("neo.rpc.specific.nodes").Children.Select(p => p.Value).ToList();

            if (this.NeoRpcNodes.Count() == 0)
            {
                this.NeoRpcNodes = section.GetNode("neo.rpc.nodes").Children
                            .Select(p => p.Value)
                            .ToList();
                this.NeoQuickSync = false;
            }

            this.EthRpcNodes = section.GetNode("eth.rpc.nodes").Children
                        .Select(p => p.Value)
                        .ToList();

            this.EthFeeURLs = section.GetNode("eth.fee.urls").Children.Select(x => FeeUrl.FromNode(x)).ToList();

            this.PricerCoinGeckoEnabled = settings.GetBool("pricer.coingecko.enabled", section.GetBool("pricer.coingecko.enabled"));
            this.PricerSupportedTokens = section.GetNode("pricer.supportedtokens").Children.Select(x => PricerSupportedToken.FromNode(x)).ToList();

            this.EthConfirmations = settings.GetUInt("eth.block.confirmations", section.GetUInt32("eth.block.confirmations"));
            this.EthGasLimit = settings.GetUInt("eth.gas.limit", section.GetUInt32("eth.gas.limit"));
            this.CryptoCompareAPIKey = settings.GetString("crypto.compare.key", section.GetString("crypto.compare.key"));
            this.Swaps = settings.GetString("swap.platforms", section.GetString("swap.platforms"));
            this.SwapColdStorageNeo = settings.GetString("swap.coldStorage.neo", section.GetString("swap.coldStorage.neo"));
            this.PhantasmaInteropHeight = settings.GetString("phantasma.interop.height", section.GetString("phantasma.interop.height"));
            this.NeoInteropHeight = settings.GetString("neo.interop.height", section.GetString("neo.interop.height"));
            this.EthInteropHeight = settings.GetString("eth.interop.height", section.GetString("eth.interop.height"));
            this.NeoWif = settings.GetString("neo.wif", section.GetString("neo.wif"));
            if (string.IsNullOrEmpty(this.NeoWif))
            {
                this.NeoWif = null;
            }
            this.EthWif = settings.GetString("eth.wif", section.GetString("eth.wif"));
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

        public SimulatorSettings(Arguments settings, DataNode section)
        {
            this.Enabled = settings.GetBool("simulator.enabled", section.GetBool("simulator.enabled"));
            this.Blocks = settings.GetBool("simulator.generate.blocks", section.GetBool("simulator.generate.blocks"));
        }
    }

    public class LogSettings
    {
        public string LogName { get; }
        public string LogPath { get; }
        public LogEventLevel FileLevel { get; }
        public LogEventLevel ShellLevel { get; }

        public LogSettings(Arguments settings, DataNode section)
        {
            this.LogName = settings.GetString("file.name", section.GetString("file.name", "spook.log"));
            this.LogPath = settings.GetString("file.path", section.GetString("file.path", Path.GetTempPath()));
            this.FileLevel = settings.GetEnum<LogEventLevel>("file.level", section.GetEnum<LogEventLevel>("file.level", LogEventLevel.Verbose));
            this.ShellLevel = settings.GetEnum<LogEventLevel>("shell.level", section.GetEnum<LogEventLevel>("shell.level", LogEventLevel.Information));
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

        public AppSettings(Arguments settings, DataNode section)
        {
            this.UseShell = settings.GetBool("shell.enabled", section.GetBool("shell.enabled"));
            this.AppName = settings.GetString("app.name", section.GetString("app.name"));
            this.NodeStart = settings.GetBool("node.start", section.GetBool("node.start"));
            this.History = settings.GetString("history", section.GetString("history"));
            this.Config = settings.GetString("config", section.GetString("config"));
            this.Prompt = settings.GetString("prompt", section.GetString("prompt"));
        }
    }

    public class RPCSettings
    {
        public string Address { get; }
        public uint Port { get; }

        public RPCSettings(Arguments settings, DataNode section)
        {
            this.Address = settings.GetString("rpc.address", section.GetString("rpc.address"));
            this.Port = settings.GetUInt("rpc.port", section.GetUInt32("rpc.port"));
        }
    }
}
