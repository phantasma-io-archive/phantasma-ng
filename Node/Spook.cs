using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Serializer;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Nethereum.Hex.HexConvertors.Extensions;
using Phantasma.Business;
using Phantasma.Core;
using Phantasma.Infrastructure;
using Phantasma.Infrastructure.Chains;
using Phantasma.Shared;
using Phantasma.Spook.Authentication;
using Phantasma.Spook.Caching;
using Phantasma.Spook.Chains;
using Phantasma.Spook.Command;
using Phantasma.Spook.Converters;
using Phantasma.Spook.Events;
using Phantasma.Spook.Hosting;
using Phantasma.Spook.Interop;
using Phantasma.Spook.Middleware;
using Phantasma.Spook.Oracles;
using Phantasma.Spook.Shell;
using Phantasma.Spook.Swagger;
using Phantasma.Spook.Utils;
using Serilog;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Tendermint.Abci;
using EthAccount = Nethereum.Web3.Accounts.Account;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using NeoAPI = Phantasma.Spook.Chains.NeoAPI;

namespace Phantasma.Spook
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

    public class Spook : Runnable
    {
        private static readonly string ConfigDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".");
        private static string ConfigFile => System.IO.Path.Combine(ConfigDirectory, "config.json");

        public static string Version { get; private set; }
        public static string TxIdentifier => $"SPK{Version}";

        private Nexus _nexus;
        private NexusAPI _nexusApi;
        private Mempool _mempool = null;
        private PhantasmaKeys _nodeKeys;
        private bool _nodeReady = false;
        private List<string> _seeds = new List<string>();
        private NeoAPI _neoAPI;
        private EthAPI _ethAPI;
        private CommandDispatcher _commandDispatcher;
        private TokenSwapper _tokenSwapper;
        private string _cryptoCompareAPIKey = null;
        private Thread _tokenSwapperThread;
        private Process _tendermintProcess;

        public NexusAPI NexusAPI { get { return _nexusApi; } }
        public Nexus Nexus { get { return _nexus; } }
        public NeoAPI NeoAPI { get { return _neoAPI; } }
        public EthAPI EthAPI { get { return _ethAPI; } }
        public string CryptoCompareAPIKey  { get { return _cryptoCompareAPIKey; } }
        public TokenSwapper TokenSwapper { get { return _tokenSwapper; } }
        public Mempool Mempool { get { return _mempool; } }
        public PhantasmaKeys NodeKeys { get { return _nodeKeys; } }
        public ABCIConnector ABCIConnector { get; private set; }

        public Spook(string[] args)
        {
            Settings.Load(args, new ConfigurationBuilder().AddJsonFile(ConfigFile, optional: false).AddEnvironmentVariables().Build().GetSection("ApplicationConfiguration"));

            this.ABCIConnector = new ABCIConnector();
        }

        protected override void OnStart()
        {
            Log.Information($"Starting Tendermint Engine");

            SetupTendermint();

            Log.Information($"Starting ABCI Application");

            var server = new Server()
            {
                Ports = { new ServerPort("127.0.0.1", 26658, ServerCredentials.Insecure) },
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

            Console.CancelKeyPress += delegate
            {
                this.Terminate();
            };

            ValidateConfig();

            Version = Assembly.GetAssembly(typeof(Spook)).GetVersion();

            _nodeKeys = SetupNodeKeys();

            if (!SetupNexus())
            {
                this.OnStop();
                return;
            }

            SetupOracleApis();

            if (Settings.Default.Node.HasMempool && !Settings.Default.Node.Readonly)
            {
                _mempool = SetupMempool();
            }

            _nexusApi = SetupNexusApi();

            _commandDispatcher = SetupCommandDispatcher();

            MakeReady(_commandDispatcher);

            if (!string.IsNullOrEmpty(Settings.Default.Oracle.Swaps))
            {
                _tokenSwapper = StartTokenSwapper();
            }
        }

        public TokenSwapper StartTokenSwapper()
        {
            var platforms = Settings.Default.Oracle.Swaps.Split(',');
            var minimumFee = Settings.Default.Node.MinimumFee;
            var oracleSettings = Settings.Default.Oracle;
            var tokenSwapper = new TokenSwapper(this, _nodeKeys, _neoAPI, _ethAPI, minimumFee, platforms);
            _nexusApi.TokenSwapper = tokenSwapper;

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

        private CommandDispatcher SetupCommandDispatcher()
        {
            Log.Information("Initializing command dispatcher...");
            var dispatcher = new CommandDispatcher(this);
            Log.Information("Command dispatcher initialized successfully.");
            return dispatcher;
        }


        private String prompt { get; set; } = "spook> ";

        private CommandDispatcher _dispatcher;

        public void MakeReady(CommandDispatcher dispatcher)
        {
            var nodeMode = Settings.Default.Node.Mode;
            Log.Information($"Node is now running in {nodeMode.ToString().ToLower()} mode!");
            _nodeReady = true;
        }

        private string PromptGenerator()
        {
            var height = this.ExecuteAPIR("getBlockHeight", new string[] { "main" });
            return string.Format(prompt, height.Trim(new char[] { '"' }));
        }

        private void SetupOracleApis()
        {
            var neoScanURL = Settings.Default.Oracle.NeoscanUrl;

            var neoRpcList = Settings.Default.Oracle.NeoRpcNodes;
            this._neoAPI = new RemoteRPCNode(neoScanURL, neoRpcList.ToArray());
            this._neoAPI.SetLogger((s) => Log.Information(s));

            var ethRpcList = Settings.Default.Oracle.EthRpcNodes;
            
            var ethWIF = Settings.Default.GetInteropWif(Nexus, _nodeKeys, EthereumWallet.EthereumPlatform);
            var ethKeys = PhantasmaKeys.FromWIF("L4GcHJVrUPz6nW2EKJJGV2yxfa5UoaG8nfnaTAgzmWyuAmt3BYKg");

            this._ethAPI = new EthAPI(this.Nexus, new EthAccount(ethKeys.PrivateKey.ToHex()));
            this._cryptoCompareAPIKey = Settings.Default.Oracle.CryptoCompareAPIKey;
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
            PhantasmaKeys nodeKeys = PhantasmaKeys.FromWIF(Settings.Default.Node.NodeWif);;
            //TODO wallet module?

            return nodeKeys;
        }

        private bool SetupTendermint()
        {
            // TODO: Platform-specific path
            var tendermintPath = "tendermint";

            if (!File.Exists(tendermintPath))
            {
                Log.Error("Could not find Tendermint binary, make sure its next to Phantasma");
                return false;
            }

            var launchArgs = new[]
            {
                "start",
                "--abci grpc",
                 "--proxy_app 127.0.0.1:44900"
            };

            _tendermintProcess = new Process();
            _tendermintProcess.StartInfo = new ProcessStartInfo
            {
                FileName = tendermintPath,
                Arguments = string.Join(' ', launchArgs),
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            _tendermintProcess.OutputDataReceived += (sender, a) =>
                Console.WriteLine(a.Data);
            _tendermintProcess.Start();
            _tendermintProcess.BeginOutputReadLine();

            return true;
        }

        private bool SetupNexus()
        {
            Log.Information("Setting up nexus...");
            var storagePath = Settings.Default.Node.StoragePath;
            var oraclePath = Settings.Default.Node.OraclePath;
            var nexusName = Settings.Default.Node.NexusName;

            switch (Settings.Default.Node.StorageBackend)
            {
                case StorageBackendType.CSV:
                    Log.Information("Setting CSV nexus...");
                    _nexus = new Nexus(nexusName, (name) => new BasicDiskStore(storagePath + name + ".csv"));
                    break;

                case StorageBackendType.RocksDB:
                    Log.Information("Setting RocksDB nexus...");
                    _nexus = new Nexus(nexusName, (name) => new DBPartition(storagePath + name));
                    break;
                default:
                    throw new Exception("Backend has to be set to either \"db\" or \"file\"");
            }

            Log.Information("Nexus is set");

            _nexus.SetOracleReader(new SpookOracle(this, _nexus));

            return true;
        }

        private Mempool SetupMempool()
        {
            var mempool = new Mempool(_nexus
                    , Settings.Default.Node.BlockTime
                    , Settings.Default.Node.MinimumFee
                    , System.Text.Encoding.UTF8.GetBytes(TxIdentifier)
                    , 0
                    , Settings.Default.Node.ProfilerPath
                    );

            if (Settings.Default.Node.MempoolLog)
            {
                mempool.OnTransactionFailed += Mempool_OnTransactionFailed;
                mempool.OnTransactionAdded += (hash) => Log.Information($"Received transaction {hash}");
                mempool.OnTransactionCommitted += (hash) => Log.Information($"Commited transaction {hash}");
                mempool.OnTransactionDiscarded += (hash) => Log.Information($"Discarded transaction {hash}");
            }
            if (Settings.Default.App.NodeStart)
            {
                mempool.StartInThread(ThreadPriority.AboveNormal);
            }
            return mempool;

        }

        private void Mempool_OnTransactionFailed(Hash hash)
        {
            if (!Running || _mempool == null)
            {
                return;
            }

            var status = _mempool.GetTransactionStatus(hash, out string reason);
            Log.Warning($"Rejected transaction {hash} => " + reason);
        }

        private NexusAPI SetupNexusApi()
        {
            Log.Information($"Initializing nexus API...");

            var apiCache = Settings.Default.Node.ApiCache;
            var apiLog = Settings.Default.Node.ApiLog;
            var apiProxyURL = Settings.Default.Node.ApiProxyUrl;
            var readOnlyMode = Settings.Default.Node.Readonly;
            var hasRPC = Settings.Default.Node.HasRpc;
            var hasREST = Settings.Default.Node.HasRest;

            NexusAPI nexusApi = new NexusAPI(_nexus, apiCache, apiLog);

            //if (apiProxyURL != null)
            //{
            //    nexusApi.ProxyURL = apiProxyURL;
            //    // TEMP Normal node needs a proxy url set to relay transactions to the BPs
            //    //nexusApi.Node = _node;
            //    Logger.Message($"API will be acting as proxy for {apiProxyURL}");
            //}
            //else
            //{
            //    nexusApi.Node = _node;
            //}

            if (readOnlyMode)
            {
                Log.Warning($"Node will be running in read-only mode.");
            }
            else
            {
                nexusApi.Mempool = _mempool;
            }

            // RPC setup
            if (hasRPC)
            {
                //var rpcPort = Settings.Node.RpcPort;
                //Logger.Information($"RPC server listening on port {rpcPort}...");
                //var rpcServer = new RPCServer(nexusApi, "/rpc", rpcPort, (level, text) => WebLogMapper("rpc", level, text));
                //rpcServer.StartInThread(ThreadPriority.AboveNormal);
            }

            // REST setup
            if (hasREST)
            {
                Log.Information($"REST API enabled...");
                var builder = WebApplication.CreateBuilder();
                builder.Configuration.AddJsonFile(Settings.Default._configFile).AddEnvironmentVariables();
                builder.WebHost.UseSerilog(Log.Logger);
                builder.Services.AddAuthorization();
                builder.Services.AddAuthentication().AddBasicAuthentication();
                builder.Services.AddHttpContextAccessor();
                builder.Services.AddTransient<IPrincipal>(sp =>
                    sp.GetRequiredService<IHttpContextAccessor>().HttpContext?.User);
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddHealthChecks();
                builder.Services.Configure<JsonOptions>(options =>
                {
                    // Ensure settings here match GetDefaultSerializerOptions()
                    options.SerializerOptions.IncludeFields = true;
                    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                    options.SerializerOptions.Converters.Add(new EnumerableJsonConverterFactory());
                });
                builder.Services.AddCors(options =>
                {
                    options.AddDefaultPolicy(policyBuilder =>
                    {
                        policyBuilder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                    });
                });
                Log.Information($"REST API: Initializing endpoints...");
                builder.Services.AddTransient<IApiEndpoint, NexusAPI>();

                Log.Information($"REST API: Configuring cache...");
                var redis = builder.Configuration.GetValue<string>("Redis");
                if (!string.IsNullOrEmpty(redis))
                {
                    Log.Information("Using Redis cache");
                    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
                        ConnectionMultiplexer.Connect(ConfigurationOptions.Parse(redis)));
                    builder.Services.AddSingleton<ICacheClient>(sp => new RedisCacheClient(optionsBuilder =>
                        optionsBuilder.ConnectionMultiplexer(sp.GetRequiredService<IConnectionMultiplexer>())
                            .LoggerFactory(sp.GetRequiredService<ILoggerFactory>()).Serializer(
                                new SystemTextJsonSerializer(GetDefaultSerializerOptions()))));
                }
                else
                {
                    Log.Information("Using in-memory cache");
                    builder.Services.AddSingleton<ICacheClient>(sp => new InMemoryCacheClient(optionsBuilder =>
                        optionsBuilder.CloneValues(true).MaxItems(10000)
                            .LoggerFactory(sp.GetRequiredService<ILoggerFactory>()).Serializer(
                                new SystemTextJsonSerializer(GetDefaultSerializerOptions()))));
                }

                builder.Services.AddSingleton<IMessageBus>(sp => new InMemoryMessageBus(optionsBuilder =>
                    optionsBuilder.LoggerFactory(sp.GetRequiredService<ILoggerFactory>())));
                builder.Services.AddSingleton<IMessagePublisher>(sp => sp.GetRequiredService<IMessageBus>());
                builder.Services.AddSingleton<IMessageSubscriber>(sp => sp.GetRequiredService<IMessageBus>());
                builder.Services.AddScoped<IEndpointCacheManager, EndpointCacheManager>();
                builder.Services.AddSingleton<IEventBus, EventBus>();
                builder.Services.AddHostedService<EventBusBackgroundService>();
                builder.Services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1",
                        new OpenApiInfo
                        {
                            Title = "Phantasma API",
                            Description = "",
                            Version = "v1",
                            Contact = new OpenApiContact
                            {
                                Name = "Phantasma",
                                Url = new Uri("https://phantasma.io")
                            }
                        });
                    c.SwaggerDoc("v1-internal",
                        new OpenApiInfo { Title = "Phantasma API (Internal)", Version = "v1-internal" });
                    c.DocumentFilter<InternalDocumentFilter>();
                });
                var app = builder.Build();

                // Redirect home page to swagger documentation
                app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

                const string basePath = "/api/v1";
                var httpMethods = new List<Type>
                {
                    typeof(HttpDeleteAttribute),
                    typeof(HttpGetAttribute),
                    //typeof(HttpHeadAttribute),
                    //typeof(HttpOptionsAttribute),
                    //typeof(HttpPatchAttribute),
                    typeof(HttpPostAttribute),
                    typeof(HttpPutAttribute)
                };

                var type = typeof(NexusAPI);
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m =>
                    m.GetCustomAttributes<APIInfoAttribute>().Any()).ToArray();

                foreach (var method in methods)
                {
                    var attribute = method.GetCustomAttributes().FirstOrDefault(a => httpMethods.Contains(a.GetType())) ??
                                    new HttpGetAttribute();

                    var methodName = method.Name.ToLowerInvariant();
                    var path = $"{basePath}/{methodName}";

                    var handler = Delegate.CreateDelegate(
                        Expression.GetDelegateType(method.GetParameters().Select(parameter => parameter.ParameterType)
                            .Concat(new[] { method.ReturnType }).ToArray()),
                        nexusApi, method);

                    switch (attribute)
                    {
                        case HttpDeleteAttribute:
                            app.MapDelete(path, handler);
                            break;
                        case HttpPostAttribute:
                            app.MapPost(path, handler);
                            break;
                        case HttpPutAttribute:
                            app.MapPut(path, handler);
                            break;
                        default:
                            // Assume GET
                            app.MapGet(path, handler);
                            break;
                    }
                }

                Log.Information($"API enabled. {methods.Length} methods available.");

                app.UseSerilogRequestLogging();
                app.UseCors();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseMiddleware<SwaggerAuthorizationMiddleware>();
                /*app.UseSwagger();
                app.UseSwaggerUI(options =>
                {
                    options.RoutePrefix = "swagger";
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
                });
                app.UseSwaggerUI(options =>
                {
                    options.RoutePrefix = "swagger-internal";
                    options.SwaggerEndpoint("/swagger/v1-internal/swagger.json", "API v1 (Internal)");
                });*/
                app.UseMiddleware<ErrorLoggingMiddleware>();
                app.UseMiddleware<CacheMiddleware>();
                // TODO 20211123 RJ: Enabling this makes the Minimal API endpoints lose some middleware capabilities
                //app.UseRouting();
                //app.UseEndpoints(endpoints => { endpoints.MapHealthChecks("/health"); });
                app.RunAsync();
            }

            return nexusApi;
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
            if (Settings.Default.Node.ApiProxyUrl != null)
            {
                if (!Settings.Default.Node.ApiCache)
                {
                    throw new Exception("A proxy node must have api cache enabled.");
                }

                // TEMP commented for now, "Normal" node needs a proxy url to relay transactions to the BPs
                //if (Settings.Node.Mode != NodeMode.Proxy)
                //{
                //    throw new Exception($"A {Settings.Node.Mode.ToString().ToLower()} node cannot have a proxy url specified.");
                //}

                if (!Settings.Default.Node.HasRpc && !Settings.Default.Node.HasRest)
                {
                    throw new Exception("API proxy must have REST or RPC enabled.");
                }
            }
            else
            {
                if (Settings.Default.Node.Mode == NodeMode.Proxy)
                {
                    throw new Exception($"A {Settings.Default.Node.Mode.ToString().ToLower()} node must have a proxy url specified.");
                }
            }

            if (!Settings.Default.Node.IsValidator && !string.IsNullOrEmpty(Settings.Default.Oracle.Swaps))
            {
                    throw new Exception("Non-validator nodes cannot have swaps enabled");
            }


            // TODO to be continued...
        }


        protected override bool Run()
        {

            if (Settings.Default.App.UseShell)
            {
                _dispatcher = new CommandDispatcher(this);

                List<string> completionList = new List<string>();

                if (!string.IsNullOrEmpty(Settings.Default.App.Prompt))
                {
                    prompt = Settings.Default.App.Prompt;
                }

                var startupMsg = "Spook shell " + Version + "\nLogs are stored in " + Settings.Default.Log.LogPath + "\nTo exit use <ctrl-c> or \"exit\"!\n";

                Prompt.Run(
                    ((command, listCmd, list) =>
                    {
                        string command_main = command.Trim().Split(new char[] { ' ' }).First();

                        if (!_dispatcher.OnCommand(command))
                        {
                            Console.WriteLine("error: Command not found");
                        }

                        return "";
                    }), prompt, PromptGenerator, startupMsg, Path.GetTempPath() + Settings.Default.App.History, _dispatcher.Verbs);
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

            if (_mempool != null && _mempool.IsRunning)
            {
                Log.Information("Stopping mempool...");
                _mempool.Stop();
            }

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

            if (Prompt.running)
            {
                Prompt.running = false;
            }

            this.OnStop();

            //Thread.Sleep(3000);
            if (Prompt.running)
            {
                Prompt.running = false;
            }

            Log.Information("Termination complete...");
            Environment.Exit(0);
        }

        public string ExecuteAPIR(string name, string[] args)
        {
            // TODO fix
            /*var result = _nexusApi.Execute(name, args);
            if (result == null)
            {
                return "";
            }

            return result;*/
            return null;
        }

        public void ExecuteAPI(string name, string[] args)
        {
            // TODO fix
            /*
            var result = _nexusApi.Execute(name, args);
            if (result == null)
            {
                Logger.Warning("API returned null value...");
                return;
            }

            Logger.Information(result);*/
        }
    }
}