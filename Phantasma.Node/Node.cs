using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Grpc.Core;
using Nethereum.Hex.HexConvertors.Extensions;
using Phantasma.Business.Blockchain;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage;
using Phantasma.Core.Utils;
using Phantasma.Infrastructure.API;
using Phantasma.Infrastructure.Pay.Chains;
using Phantasma.Infrastructure.RocksDB;
using Phantasma.Node.Chains.Ethereum;
using Phantasma.Node.Chains.Neo2;
using Phantasma.Node.Converters;
using Phantasma.Node.Interop;
using Phantasma.Node.Oracles;
using Phantasma.Node.Shell;
using Phantasma.Node.Utils;
using Serilog;
using Tendermint.Abci;
using Tendermint.RPC;
using EthAccount = Nethereum.Web3.Accounts.Account;
using NeoAPI = Phantasma.Node.Chains.Neo2.NeoAPI;

namespace Phantasma.Node
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ModuleAttribute : Attribute
    {
        public readonly string Name;

        public ModuleAttribute(string name)
        {
            Name = name;
        }
    }

    public class Node: Runnable
    {
        public static string Version { get; private set; }
        public static string TxIdentifier => $"Node-{Version}";

        private PhantasmaKeys _nodeKeys;
        private bool _nodeReady = false;
        private List<string> _seeds = new List<string>();
        private NeoAPI _neoAPI;
        private EthAPI _ethAPI;
        private string _cryptoCompareAPIKey = null;
        private Thread _tokenSwapperThread;

        private string _tendermintHome;
        private string _tendermint_RPC_URL;
        private string _tendermint_Proxy_URL;
        private Process _tendermintProcess;

        public NeoAPI NeoAPI { get { return _neoAPI; } }
        public EthAPI EthAPI { get { return _ethAPI; } }
        public string CryptoCompareAPIKey  { get { return _cryptoCompareAPIKey; } }
        public PhantasmaKeys NodeKeys { get { return _nodeKeys; } }
        public ABCIConnector ABCIConnector { get; private set; }
        public NodeConnector NodeConnector { get; private set; }

        public Node()
        {
            this.NodeConnector = new NodeConnector(Settings.Instance.Validators);
            this.ABCIConnector = new ABCIConnector(Settings.Instance.Node.SeedValidators, Settings.Instance.Validators, this.NodeConnector, Settings.Instance.Node.MinimumFee);
        }

        protected override void OnStart()
        {
            Log.Information($"Starting ABCI Application");

            Console.CancelKeyPress += delegate
            {
                this.Terminate();
            };

            ValidateConfig();

            Version = Assembly.GetAssembly(typeof(Node)).GetVersion();

            _nodeKeys = SetupNodeKeys();

            _tendermint_RPC_URL = Settings.Instance.Node.TendermintRPCHost + ":" + Settings.Instance.Node.TendermintRPCPort;
            _tendermint_Proxy_URL = Settings.Instance.Node.TendermintProxyHost + ":" + Settings.Instance.Node.TendermintProxyPort;

            if (!SetupNexus())
            {
                Log.Information("Stopping node...");
                this.OnStop();
                return;
            }

            this.ABCIConnector.SetNodeInfo(NexusAPI.Nexus, _tendermint_RPC_URL, _nodeKeys);

            NexusAPI.isTransactionPending = ABCIConnector.IsTransactionPending;

            var options = new ChannelOption[] {
                new ChannelOption(ChannelOptions.MaxReceiveMessageLength, 1500*1024*1024)
            };

            var server = new Server(options)
            {
                Ports = { new ServerPort(Settings.Instance.Node.TendermintProxyHost, Settings.Instance.Node.TendermintProxyPort
                        , ServerCredentials.Insecure) },
                Services = { ABCIApplication.BindService(this.ABCIConnector) },
            };

            server.Start();

            
            Log.Information($"Server is up & running ()");

            //ShutdownAwaiter();

            //_logger.LogInformation("Shutting down...");
            //server.ShutdownAsync().Wait();
            //_logger.LogInformation("Done shutting down...");
            //
            // NEW NEW NEW NEW NEW 


            //SetupOracleApis();

            SetupNexusApi();

            //if (!string.IsNullOrEmpty(Settings.Default.Oracle.Swaps))
            //{
            //    StartTokenSwapper();
            //}
            
            // Clear whitelist
            //Filter.RemoveRedFilteredAddress(NexusAPI.Nexus.RootStorage, SmartContract.GetAddressForNative(NativeContractKind.Stake), "filter.red");
            //Filter.RemoveRedFilteredAddress(NexusAPI.Nexus.RootStorage, SmartContract.GetAddressForNative(NativeContractKind.Swap), "filter.red");
            //Filter.RemoveRedFilteredAddress(NexusAPI.Nexus.RootStorage, SmartContract.GetAddressForNative(NativeContractKind.Exchange), "filter.red");

            if (!string.IsNullOrEmpty(Settings.Instance.Node.TendermintPath))
            {
                LaunchTendermint(Settings.Instance.Node.TendermintPath);
            }
        }

        public TokenSwapper StartTokenSwapper()
        {
            var platforms = Settings.Instance.Oracle.Swaps.Split(',');
            var minimumFee = Settings.Instance.Node.MinimumFee;
            var oracleSettings = Settings.Instance.Oracle;
            var tokenSwapper = new TokenSwapper(this, _nodeKeys, _neoAPI, _ethAPI, minimumFee, platforms);
            NexusAPI.TokenSwapper = tokenSwapper;

            _tokenSwapperThread = new Thread(() =>
            {
                Log.Information("Running token swapping service...");
                while (Running)
                {
                    Log.Debug("Update TokenSwapper now");
                    Task.Delay(5000).Wait();
                    if (_nodeReady)
                    {
                        tokenSwapper.Update();
                    }
                }
            });

            _tokenSwapperThread.Start();

            return tokenSwapper;
        }

        private String prompt { get; set; } = "Node> ";

        private string PromptGenerator()
        {
            var height = NexusAPI.Nexus.RootChain.Height.ToString();
            return string.Format(prompt, height.Trim(new char[] { '"' }));
        }

        private void SetupOracleApis()
        {
            /*var neoScanURL = Settings.Instance.Oracle.NeoscanUrl;

            var neoRpcList = Settings.Instance.Oracle.NeoRpcNodes;
            this._neoAPI = new RemoteRPCNode(neoScanURL, neoRpcList.ToArray());
            this._neoAPI.SetLogger((s) => Log.Information(s));*/

            var ethRpcList = Settings.Instance.Oracle.EthRpcNodes;
            var ethWIF = Settings.Instance.GetInteropWif(_nodeKeys, EthereumWallet.EthereumPlatform);
            //TODO
            var ethKeys = PhantasmaKeys.FromWIF(ethWIF);

            this._ethAPI = new EthAPI(new EthAccount(ethKeys.PrivateKey.ToHex()));
            this._cryptoCompareAPIKey = Settings.Instance.Oracle.CryptoCompareAPIKey;
            if (!string.IsNullOrEmpty(this._cryptoCompareAPIKey))
            {
                Log.Information($"CryptoCompare API enabled.");
            }
            else
            {
                Log.Warning($"CryptoCompare API key missing, oracles won't work properly...");
            }
        }

        private PhantasmaKeys SetupNodeKeys()
        {
            var keyStr = Environment.GetEnvironmentVariable("PHA_KEY");

            PhantasmaKeys nodeKeys = null;

            if (!string.IsNullOrEmpty(keyStr))
            {
                nodeKeys = new PhantasmaKeys(Convert.FromBase64String(keyStr));
            }

            if (nodeKeys is null)
            {
                nodeKeys = new PhantasmaKeys(Convert.FromBase64String(Settings.Instance.Node.TendermintKey));
            }

            //if (nodeKeys is null)
            //{
            //    nodeKeys = PhantasmaKeys.FromWIF(Settings.Default.Node.NodeWif);
            //}

            //TODO wallet module?

            return nodeKeys;
        }

        public enum Platform
        {
            Windows,
            Linux,
            Mac
        }

        public static Platform GetRunningPlatform()
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                    // Well, there are chances MacOSX is reported as Unix instead of MacOSX.
                    // Instead of platform check, we'll do a feature checks (Mac specific root folders)
                    if (Directory.Exists("/Applications")
                        & Directory.Exists("/System")
                        & Directory.Exists("/Users")
                        & Directory.Exists("/Volumes"))
                        return Platform.Mac;
                    else
                        return Platform.Linux;

                case PlatformID.MacOSX:
                    return Platform.Mac;

                default:
                    return Platform.Windows;
            }
        }

        // NOTE - Tendermint is run here only if config.json has a path configured, instead start Tendermint externally, it also works
        private bool LaunchTendermint(string tendermintPath)
        {
            var platform = GetRunningPlatform();

            if (!File.Exists(tendermintPath))
            {
                var exeName = "tendermint";
                if (platform == Platform.Windows)
                {
                    exeName += ".exe";
                }

                var newPath = tendermintPath;

                if (!newPath.EndsWith(exeName))
                {
                    if (!newPath.EndsWith(Path.DirectorySeparatorChar))
                    {
                        newPath += Path.DirectorySeparatorChar;
                    }

                    newPath += exeName;
                }

                if (File.Exists(newPath))
                {
                    tendermintPath = newPath;
                }
                else
                {
                    Log.Error("Could not find Tendermint binary at location: " + tendermintPath);
                    Log.Error("Get latest version here: https://github.com/tendermint/tendermint/releases");
                    return false;
                }
            }

            _tendermintHome = Settings.Instance.Node.TendermintHome;

            if (string.IsNullOrEmpty(_tendermintHome))
            {
                _tendermintHome = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + Path.DirectorySeparatorChar + "tendermint";
            }

            if (!Directory.Exists(_tendermintHome))
            {
                Log.Warning("Initializing Tendermint home");

                if (!RunTendermint(tendermintPath, "init"))
                {
                    Log.Error("Could not initialize Tendermint home");
                    return false;
                }

                if (!PatchTendermintValidatorKey())
                {
                    Log.Error("Could not patch Tendermint private key");
                    return false;
                }

                if (!PatchTendermintConfigToml())
                {
                    Log.Error("Could not patch Tendermint config.toml: " + _patchError);
                    return false;
                }

                if (!PatchTendermintGenesis())
                {
                    Log.Error("Could not patch Tendermint genesis: " + _patchError);
                    return false;
                }
            }

            return RunTendermint(tendermintPath, "node");
        }

        private bool PatchTendermintValidatorKey()
        {
            var keyFile = Path.Combine(_tendermintHome, "config/priv_validator_key.json");

            if (!File.Exists(keyFile))
            {
                return false;
            }

            var lines = File.ReadAllLines(keyFile);

            var bytes = _nodeKeys.Address.ToByteArray()[2..][..20];
            var pubKey = Base16.Encode(bytes);

            lines[1] = $"\t\"address\": \"{_nodeKeys.Address.TendermintAddress}\",";
            lines[4] = $"\t\"value\": \"{pubKey}\"";
            lines[8] = $"\t\"value\": \"{Settings.Instance.Node.TendermintKey}\"";

            File.WriteAllLines(keyFile, lines);

            return true;
        }

        private bool PatchTendermintConfigToml()
        {
            var keyFile = Path.Combine(_tendermintHome, "config/config.toml");

            if (!File.Exists(keyFile))
            {
                _patchError = "Could not find file: " + keyFile;
                return false;
            }

            var isMainnet = Settings.Instance.Node.NexusName == DomainSettings.NexusMainnet;

            var lines = File.ReadAllLines(keyFile);

            if (string.IsNullOrEmpty(_tendermint_RPC_URL))
            {
                _patchError = "Tenderming RPC URL is not set or is invalid";
                return false;
            }

            if (string.IsNullOrEmpty(_tendermint_Proxy_URL))
            {
                _patchError = "Tendermint proxy URL is not set or is invalid";
                return false;
            }

            if (string.IsNullOrEmpty(Settings.Instance.Node.TendermintPeers))
            {
                _patchError = "tendermint.peers is not set or is invalid";
                return false;
            }


            if (!PatchLine(lines, "laddr", $"\"tcp://{StripURL(_tendermint_RPC_URL)}\""))
            {
                return false;
            }

            if (!PatchLine(lines, "proxy_app", $"\"tcp://{StripURL(_tendermint_Proxy_URL)}\""))
            {
                return false;
            }

            if (!PatchLine(lines, "abci", "\"grpc\""))
            {
                return false;
            }

            if (!PatchLine(lines, "addr_book_strict", isMainnet ? "true" : "false"))
            {
                return false;
            }

            if (!PatchLine(lines, "allow_duplicate_ip", isMainnet ? "false" : "true"))
            {
                return false;
            }

            if (!PatchLine(lines, "create_empty_blocks", "false"))
            {
                return false;
            }

            var peers = Settings.Instance.Node.TendermintPeers;
            if (!PatchLine(lines, "persistent_peers", $"\"{peers}\""))
            {
                return false;
            }

            File.WriteAllLines(keyFile, lines);

            return true;
        }

        // TODO rewrite this method using proper JSON manipulation, this is just a temp hack
        private bool PatchTendermintGenesis()
        {
            var genesisFile = Path.Combine(_tendermintHome, "config/genesis.json");

            if (!File.Exists(genesisFile))
            {
                _patchError = "Could not find file: " + genesisFile;
                return false;
            }

            if (string.IsNullOrEmpty(Settings.Instance.Node.TendermintChainID))
            {
                _patchError = "tendermint.chainid is not set or invalid";
                return false;
            }

            if (string.IsNullOrEmpty(Settings.Instance.Node.TendermintGenesis))
            {
                _patchError = "tendermint.genesis is not set or invalid";
                return false;
            }

            var lines = new List<string>();
            lines.Add("{");
            lines.Add($"\t\"genesis_time\": \"{Settings.Instance.Node.TendermintGenesis}\",");
            lines.Add($"\t\"chain_id\": \"{Settings.Instance.Node.TendermintChainID}\",");
            lines.Add(
$@"  ""initial_height"": ""0"",
  ""consensus_params"": {{
    ""block"": {{
      ""max_bytes"": ""22020096"",
      ""max_gas"": ""-1"",
      ""time_iota_ms"" : ""1000""
    }},
    ""evidence"": {{
      ""max_age_num_blocks"": ""100000"",
      ""max_age_duration"": ""172800000000000"",
      ""max_bytes"": ""1048576""
    }},
    ""validator"": {{
      ""pub_key_types"": [
        ""ed25519""
      ]
    }},
    ""version"": {{
      ""app_version"": ""0""
    }}
  }},
  ""validators"": [");

            int idx = 0;
            var validators = Settings.Instance.Node.SeedValidators;

            if (validators.Count < 3)
            {
                _patchError = "Validators array is not set or invalid";
                return false;
            }

            foreach (var validator in validators)
            {
                var name = "node" + idx;

                idx++;

                var pubKey = validator.ToByteArray()[2..];
                var encodedPubKey = Convert.ToBase64String(pubKey);

                lines.Add("\t{");
                lines.Add($"\t\t\"address\": \"{validator.TendermintAddress}\",");
                lines.Add("\t\t\"pub_key\": {");
                lines.Add("\t\t\t\"type\": \"tendermint/PubKeyEd25519\",");
                lines.Add($"\t\t\t\"value\": \"{encodedPubKey}\"");
                lines.Add("\t\t},");
                lines.Add("\t\t\"power\": \"1\",");
                lines.Add($"\t\t\"name\": \"{name}\"");
                lines.Add(idx == validators.Count ? "\t}" : "\t},");
            }

            lines.Add("  ],");
            lines.Add("  \"app_hash\": \"\"");
            lines.Add("}");
            File.WriteAllLines(genesisFile, lines);

            return true;
        }

        private static string StripURL(string url)
        {
            return url.Replace("http://", "", StringComparison.OrdinalIgnoreCase).Replace("https://", "", StringComparison.OrdinalIgnoreCase);
        }

        private string _patchError = "generic patch error";
        private bool PatchLine(string[] lines, string setting, string newValue)
        {
            var expectedLineStart = setting + " = ";

            for (int i=0; i<lines.Length; i++)
            {
                if (lines[i].Trim().StartsWith(expectedLineStart, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = expectedLineStart + newValue;
                    return true;
                }
            }

            _patchError = "Could not patch setting: " + setting;
            return false;
        }

        private void StopTendermint()
        {
            if (_tendermintProcess != null)
            {
                if (!_tendermintProcess.HasExited)
                {
                    Log.Warning("Stopping Tendermint process");
                    _tendermintProcess.Kill();
                }
            }
        }

        private bool RunTendermint(string tendermintPath, string launchArgs)
        {
            StopTendermint();

            launchArgs = $"--home \"{_tendermintHome}\" {launchArgs}";

            _tendermintProcess = new Process();
            _tendermintProcess.StartInfo = new ProcessStartInfo
            {
                FileName = tendermintPath,
                Arguments = launchArgs,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            var result = _tendermintProcess.Start();

            new Thread(() =>
            {
                DumpStream(_tendermintProcess.StandardOutput);
            }).Start();

            DumpStream(_tendermintProcess.StandardError);

            return result;
        }

        private void DumpStream(StreamReader stream)
        {
            while (!stream.EndOfStream)
            {
                string line = stream.ReadLine();

                lock (this)
                {
                    var tmp = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine(line);
                    Console.ForegroundColor = tmp;
                }
            }
        }

        private bool SetupNexus()
        {
            Log.Information("Setting up nexus...");
            var storagePath = Settings.Instance.Node.StoragePath;
            var oraclePath = Settings.Instance.Node.OraclePath;
            var nexusName = Settings.Instance.Node.NexusName;

            switch (Settings.Instance.Node.StorageBackend)
            {
                case StorageBackendType.CSV:
                    Log.Information("Setting CSV nexus...");
                    NexusAPI.Nexus = new Nexus(nexusName, (name) => new BasicDiskStore(storagePath + name + ".csv"));
                    break;

                case StorageBackendType.RocksDB:
                    Log.Information("Setting RocksDB nexus...");
                    NexusAPI.Nexus = new Nexus(nexusName, (name) => new DBPartition(storagePath + name));
                    break;
                default:
                    throw new Exception("Backend has to be set to either \"db\" or \"file\"");
            }

            NexusAPI.TRPC = new NodeRpcClient(_tendermint_RPC_URL);

            Log.Information("Nexus is set");

            NexusAPI.Nexus.SetOracleReader(new SpookOracle(this, NexusAPI.Nexus));

            return true;
        }

        private void SetupNexusApi()
        {
            Log.Information($"Initializing nexus API...");

            NexusAPI.Validators = Settings.Instance.Validators;
            NexusAPI.ApiLog = Settings.Instance.Node.ApiLog;
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

        private void ValidateConfig()
        {
            /*if (!Settings.Default.Node.IsValidator && !string.IsNullOrEmpty(Settings.Default.Oracle.Swaps))
            {
                    throw new Exception("Non-validator nodes cannot have swaps enabled");
            }*/


            // TODO to be continued...
        }


        protected override bool Run()
        {

            if (Settings.Instance.App.UseShell)
            {
                List<string> completionList = new List<string>();

                if (!string.IsNullOrEmpty(Settings.Instance.App.Prompt))
                {
                    prompt = Settings.Instance.App.Prompt;
                }

                var startupMsg = "Nodeshell " + Version + "\nLogs are stored in " + Settings.Instance.Log.LogPath + "\nTo exit use <ctrl-c> or \"exit\"!\n";

                Prompt.Run(
                    ((command, listCmd, list) =>
                    {
                        string command_main = command.Trim().Split(new char[] { ' ' }).First();

                        return "";
                    }), prompt, PromptGenerator, startupMsg, Path.GetTempPath() + Settings.Instance.App.History, null);
            }
            else
            {
                // Do nothing in this thread...
                Thread.Sleep(1000);
            }

            return this.Running;
        }

        protected override void OnStop()
        {
            Log.Information("Termination started...");

            StopTendermint();

            //if (_node != null && _node.IsRunning)
            //{
            //    Logger.Message("Stopping node...");
            //    _node.Stop();
            //}
        }

        public void Terminate()
        {
            if (!Running)
            {
                Log.Information("Termination already in progress...");
            }

            if (Prompt.Running)
            {
                Prompt.Running = false;
            }

            this.OnStop();

            //Thread.Sleep(3000);
            if (Prompt.Running)
            {
                Prompt.Running = false;
            }

            Log.Information("Termination complete...");
            Environment.Exit(0);
        }
        
    }
}
