using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Phantasma.Core.Cryptography.Structs;

namespace Phantasma.Node.Configuration;

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
    public string APIURL { get; } = "http://localhost:7077";

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
