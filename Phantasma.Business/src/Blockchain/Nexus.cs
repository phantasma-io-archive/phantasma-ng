//#define ALLOWANCE_OPERATIONS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Storage;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.VM;
using Phantasma.Business.VM.Utils;
using Phantasma.Core;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Utils;
using Serilog;
using Timestamp = Phantasma.Core.Types.Timestamp;

namespace Phantasma.Business.Blockchain;

public class Nexus : INexus
{
    private string ChainNameMapKey => ".chain.name.";
    private string ChainAddressMapKey => ".chain.addr.";
    private string ChainOrgKey => ".chain.org.";
    private string ChainParentNameKey => ".chain.parent.";
    private string ChainChildrenBlockKey => ".chain.children.";

    private string ChainArchivesKey => ".chain.archives.";
    public static string NexusProtocolVersionTag => "nexus.protocol.version";
    
     private static string OLD_LP_CONTRACT_PVM =
        "000D01041C5068616E7461736D61204C69717569646974792050726F76696465720301082700000B000D0104024C500301083500000B000D010601000301084200000B000D010601010301084F00000B000D010301080301085C00000B000D010601000301086900000B000D010301000301087600000B000D010408446174612E4765740D0204024C500D0003010803000D0004065F6F776E6572030003020701040303030D00040941646472657373282907000403020301030108BF00000B000D00040F52756E74696D652E56657273696F6E070004000D010301001A0001000A001E010D00043243757272656E74206E657875732070726F746F636F6C2076657273696F6E2073686F756C642062652030206F72206D6F72650C0000040303030D000409416464726573732829070004030203040204010D040601000204020D05026C04076765744E616D6504000000000107746F6B656E4944030E6765744465736372697074696F6E045D0000000107746F6B656E4944030B676574496D61676555524C04F10000000107746F6B656E4944030A676574496E666F55524C041E0100000107746F6B656E4944030003050D0502FD52010004010D0004066D696E744944030003010D0004024C5003000D00041152756E74696D652E52656164546F6B656E070004023202020D0004066D696E744944300203000D0104044C5020230203020E020204230102040304085C00000B0004010D000403524F4D030003010D0004024C5003000D00041152756E74696D652E52656164546F6B656E070004023202020D000403524F4D300203003203030D0104124C6971756469747920666F7220706F6F6C200203020D04040753796D626F6C30300202040D040403202F200203050D06040753796D626F6C3130050506230405062302060423010402030208F000000B0004010D01041F68747470733A2F2F7068616E7461736D612E696F2F696D672F6C702E706E670301081D01000B0004010201020D01041868747470733A2F2F7068616E7461736D612E696F2F6C702F0202030E030304230103040304085101000B03050D0507040000000003050D0503010003050D0503010003050D0504024C50030502010503050D0404174E657875732E437265617465546F6B656E5365726965730704000D030408446174612E53657403010D0004065F6F776E65720300070303020D0004085F6368616E676564030007030B000D010408446174612E4765740D0204024C500D0003010803000D0004065F6F776E6572030003020701040303030D00040941646472657373282907000403040103010D000409416464726573732829070004010402320202040432040402030603060D05041152756E74696D652E49735769746E65737307050405090514040D06040E7769746E657373206661696C65640C06000D0703010103070204070E07070203070202070E07070203070D0704024C50030702010703070D0702220200FACEE6DD84C950B54361FB71DFE736CDC64D894294305B4FEEAF3C61ABF2F2F703070D0004094164647265737328290700040703070D06041152756E74696D652E4D696E74546F6B656E070604060206050205060306089A04000B00040103010D0004094164647265737328290700040104020E02020302010403040D03041152756E74696D652E49735769746E657373070304030903EE040D04040E7769746E657373206661696C65640C040002020403040D040404534F554C03040D0402220200FACEE6DD84C950B54361FB71DFE736CDC64D894294305B4FEEAF3C61ABF2F2F703040D00040941646472657373282907000404030402010403040D03041652756E74696D652E5472616E73666572546F6B656E73070302020403040D0402220200FACEE6DD84C950B54361FB71DFE736CDC64D894294305B4FEEAF3C61ABF2F2F703040D0004094164647265737328290700040403040D0304055374616B6503030D0304057374616B652D03032E03000B000D010408446174612E4765740D0204024C500D0003010803000D0004065F6F776E6572030003020701040303030D000409416464726573732829070004030D0003010603000D0004085F6368616E6765640300030207010404040103010D0004094164647265737328290700040102040215020209024D060D0504194F776E65722077617320616C7265616479206368616E6765640C050002030503050D02041152756E74696D652E49735769746E65737307020402090284060D05040E7769746E657373206661696C65640C05000D000420416464726573732E697353797374656D206E6F7420696D706C656D656E7465640C000902DC060D050427746865206E65772061646472657373206973206E6F7420612073797374656D20616464726573730C05000201020202030D02060101020204000D010408446174612E53657403030D0004065F6F776E65720300070103040D0004085F6368616E676564030007010B000D010408446174612E4765740D0204024C500D0003010803000D0004065F6F776E6572030003020701040303030D00040941646472657373282907000403040103010D0004094164647265737328290700040102030403040D02041152756E74696D652E49735769746E657373070204020902A5070D04040E7769746E657373206661696C65640C04000D04040B4D696772617465546F563303040D02040865786368616E67652D02022E02000B00040103010D000409416464726573732829070004010D0204174E6F7420616C6C6F77656420746F20757067726164652E0C02000B00040103010D00040941646472657373282907000401040203020D0004094164647265737328290700040202010403040D03041152756E74696D652E49735769746E65737307030403090360080D04040E7769746E657373206661696C65640C0400000B000D010408446174612E4765740D0204024C500D0003010803000D0004065F6F776E6572030003020701040303030D00040941646472657373282907000403040103010D00040941646472657373282907000401040203020D00040941646472657373282907000402040404050E0505030D0704024C50020706020407020608190708090909FF080D07040E696E76616C69642073796D626F6C0C070002030803080D07041152756E74696D652E49735769746E65737307070407090736090D08040E7769746E657373206661696C65640C0800083A09000B00040103010D0004094164647265737328290700040104020E02020302010403040D03041152756E74696D652E49735769746E6573730703040309038E090D04040E7769746E657373206661696C65640C0400089209000B00040103010D00040941646472657373282907000401040203020D00040941646472657373282907000402040304040E04040302010603060D05041152756E74696D652E49735769746E657373070504050905FD090D06040E7769746E657373206661696C65640C060008010A000B";

    private static string OLD_LP_CONTRACT_ABI =
        "12076765744E616D650400000000000967657453796D626F6C0428000000000E69735472616E7366657261626C650636000000000A69734275726E61626C650643000000000B676574446563696D616C7303500000000008697346696E697465065D000000000C6765744D6178537570706C79036A00000000086765744F776E65720877000000000A496E697469616C697A6500C0000000010D636F6E74726163744F776E657208044D696E74037F030000030466726F6D0803726F6D010372616D011153656E6446756E6473416E645374616B65009B040000020466726F6D0806616D6F756E74030B4368616E67654F776E657200B4050000010466726F6D080C75706772616465546F446578001A070000010466726F6D08096F6E5570677261646500C9070000010466726F6D08096F6E4D69677261746500FE070000020466726F6D0802746F08066F6E4D696E740062080000040466726F6D0802746F080673796D626F6C0407746F6B656E494403046275726E003B090000020466726F6D0807746F6B656E494403066F6E4275726E0093090000040466726F6D0802746F080673796D626F6C0407746F6B656E49440300";

    private static string NEW_LP_CONTRACT_PVM = "000D01041C5068616E7461736D61204C69717569646974792050726F76696465720301082700000B000D0104024C500301083500000B000D010601000301084200000B000D010601010301084F00000B000D010301080301085C00000B000D010601000301086900000B000D010301000301087600000B000D010408446174612E4765740D0204024C500D0003010803000D0004056F776E6572030003020701040303030D00040941646472657373282907000403020301030108BE00000B000D00040F52756E74696D652E56657273696F6E070004000D010301001A0001000A001D010D00043243757272656E74206E657875732070726F746F636F6C2076657273696F6E2073686F756C642062652030206F72206D6F72650C0000040303030D000409416464726573732829070004030203040204010D040601000204020D05026C04076765744E616D6504000000000107746F6B656E4944030E6765744465736372697074696F6E045D0000000107746F6B656E4944030B676574496D61676555524C04F10000000107746F6B656E4944030A676574496E666F55524C041E0100000107746F6B656E4944030003050D0502FD52010004010D0004066D696E744944030003010D0004024C5003000D00041152756E74696D652E52656164546F6B656E070004023202020D0004066D696E744944300203000D0104044C5020230203020E020204230102040304085C00000B0004010D000403524F4D030003010D0004024C5003000D00041152756E74696D652E52656164546F6B656E070004023202020D000403524F4D300203003203030D0104124C6971756469747920666F7220706F6F6C200203020D04040753796D626F6C30300202040D040403202F200203050D06040753796D626F6C3130050506230405062302060423010402030208F000000B0004010D01041F68747470733A2F2F7068616E7461736D612E696F2F696D672F6C702E706E670301081D01000B0004010201020D01041868747470733A2F2F7068616E7461736D612E696F2F6C702F0202030E030304230103040304085101000B03050D0507040000000003050D0503010003050D0503010003050D0504024C50030502010503050D0404174E657875732E437265617465546F6B656E5365726965730704000D030408446174612E53657403010D0004056F776E65720300070303020D0004085F6368616E676564030007030B000D010408446174612E4765740D0204024C500D0003010803000D0004056F776E6572030003020701040303030D00040941646472657373282907000403040103010D000409416464726573732829070004010402320202040432040402030603060D05041152756E74696D652E49735769746E65737307050405090511040D06040E7769746E657373206661696C65640C06000D0703010103070204070E07070203070202070E07070203070D0704024C50030702010703070D0702220200FACEE6DD84C950B54361FB71DFE736CDC64D894294305B4FEEAF3C61ABF2F2F703070D0004094164647265737328290700040703070D06041152756E74696D652E4D696E74546F6B656E070604060206050205060306089704000B00040103010D0004094164647265737328290700040104020E02020302010403040D03041152756E74696D652E49735769746E657373070304030903EB040D04040E7769746E657373206661696C65640C040002020403040D040404534F554C03040D0402220200FACEE6DD84C950B54361FB71DFE736CDC64D894294305B4FEEAF3C61ABF2F2F703040D00040941646472657373282907000404030402010403040D03041652756E74696D652E5472616E73666572546F6B656E73070302020403040D0402220200FACEE6DD84C950B54361FB71DFE736CDC64D894294305B4FEEAF3C61ABF2F2F703040D0004094164647265737328290700040403040D0304055374616B6503030D0304057374616B652D03032E03000B000D010408446174612E4765740D0204024C500D0003010803000D0004056F776E6572030003020701040303030D000409416464726573732829070004030D0003010603000D0004085F6368616E6765640300030207010404040103010D00040941646472657373282907000401020402150202090249060D0504194F776E65722077617320616C7265616479206368616E6765640C050002030503050D02041152756E74696D652E49735769746E65737307020402090280060D05040E7769746E657373206661696C65640C05000D000420416464726573732E697353797374656D206E6F7420696D706C656D656E7465640C000902D8060D050427746865206E65772061646472657373206973206E6F7420612073797374656D20616464726573730C05000201020202030D02060101020204000D010408446174612E53657403030D0004056F776E65720300070103040D0004085F6368616E676564030007010B000D010408446174612E4765740D0204024C500D0003010803000D0004056F776E6572030003020701040303030D00040941646472657373282907000403040103010D0004094164647265737328290700040102030403040D02041152756E74696D652E49735769746E6573730702040209029F070D04040E7769746E657373206661696C65640C04000D04040B4D696772617465546F563303040D02040865786368616E67652D02022E02000B000D010408446174612E4765740D0204024C500D0003010803000D0004056F776E6572030003020701040303030D00040941646472657373282907000403040103010D0004094164647265737328290700040102030403040D02041152756E74696D652E49735769746E6573730702040209024D080D04040E7769746E657373206661696C65640C0400085108000B00040103010D00040941646472657373282907000401040203020D0004094164647265737328290700040202010403040D03041152756E74696D652E49735769746E657373070304030903B4080D04040E7769746E657373206661696C65640C0400000B000D010408446174612E4765740D0204024C500D0003010803000D0004056F776E6572030003020701040303030D00040941646472657373282907000403040103010D00040941646472657373282907000401040203020D00040941646472657373282907000402040404050E0505030D0704024C5002070602040702060819070809090952090D07040E696E76616C69642073796D626F6C0C070002030803080D07041152756E74696D652E49735769746E65737307070407090789090D08040E7769746E657373206661696C65640C0800088D09000B00040103010D0004094164647265737328290700040104020E02020302010403040D03041152756E74696D652E49735769746E657373070304030903E1090D04040E7769746E657373206661696C65640C040008E509000B00040103010D00040941646472657373282907000401040203020D00040941646472657373282907000402040304040E04040302010603060D05041152756E74696D652E49735769746E657373070504050905500A0D06040E7769746E657373206661696C65640C0600020406030602010603060D0604076275726E4E465403060D05040865786368616E67652D05052E05087C0A000B00040103010D000409416464726573732829070004010D0204144E6F7420616C6C6F77656420746F206B696C6C2E0C02000B";
    private static string NEW_LP_CONTRACT_ABI = "13076765744E616D650400000000000967657453796D626F6C0428000000000E69735472616E7366657261626C650636000000000A69734275726E61626C650643000000000B676574446563696D616C7303500000000008697346696E697465065D000000000C6765744D6178537570706C79036A00000000086765744F776E65720877000000000A496E697469616C697A6500BF000000010D636F6E74726163744F776E657208044D696E74037D030000030466726F6D0803726F6D010372616D011153656E6446756E6473416E645374616B650098040000020466726F6D0806616D6F756E74030B4368616E67654F776E657200B1050000010466726F6D080C75706772616465546F4465780015070000010466726F6D08096F6E5570677261646500C3070000010466726F6D08096F6E4D6967726174650052080000020466726F6D0802746F08066F6E4D696E7400B6080000040466726F6D0802746F080673796D626F6C0407746F6B656E494403046275726E008E090000020466726F6D0807746F6B656E494403066F6E4275726E00E6090000040466726F6D0802746F080673796D626F6C0407746F6B656E494403066F6E4B696C6C007D0A0000010466726F6D0800";
    
    public static readonly BigInteger FuelPerContractDeployDefault = UnitConversion.ToBigInteger(10, DomainSettings.FiatTokenDecimals);
    public static readonly BigInteger FuelPerTokenDeployDefault = UnitConversion.ToBigInteger(100, DomainSettings.FiatTokenDecimals);
    public static readonly BigInteger FuelPerOrganizationDeployDefault = UnitConversion.ToBigInteger(10, DomainSettings.FiatTokenDecimals);

    private bool _migratingNexus;
    private IEnumerable<Address> _initialValidators = Enumerable.Empty<Address>();
    private Dictionary<string, KeyValuePair<BigInteger, ChainConstraint[]>> _genesisValues = null;

    public string Name { get; init; }

    private IChain _rootChain = null;
    public IChain RootChain
    {
        get
        {
            if (_rootChain == null)
            {
                _rootChain = GetChainByName(DomainSettings.RootChainName);
            }
            return _rootChain;
        }
    }

    private KeyValueStore<Hash, byte[]> _archiveContents;

    private Func<string, IKeyValueStoreAdapter> _adapterFactory = null;
    private IOracleReader _oracleReader = null;
    private List<IOracleObserver> _observers = new List<IOracleObserver>();

    /// <summary>
    /// The constructor bootstraps the main chain and all core side chains.
    /// </summary>
    public Nexus(string name, Func<string, IKeyValueStoreAdapter> adapterFactory = null)
    {
        _adapterFactory = adapterFactory;

        var storage = new KeyStoreStorage(GetChainStorage(DomainSettings.RootChainName));
        RootStorage = storage;

        if (!ValidationUtils.IsValidIdentifier(name))
        {
            throw new ChainException("invalid nexus name");
        }

        this.Name = name;

        if (HasGenesis())
        {
            _migratingNexus = false;

            var res = LoadNexus(storage);
            if (!res)
            {
                throw new ChainException("failed load nexus for chain");
            }
        }
        else
        {
            InitGenesisValues();

            var tokens = GetTokens(storage);
            _migratingNexus = tokens.Any(x => x.Equals(DomainSettings.StakingTokenSymbol));

            if (_migratingNexus)
            {
                Log.Information("Detected old nexus data, migration will be executed.");
            }

            if (!ChainExists(storage, DomainSettings.RootChainName))
            {
                if (!CreateChain(storage, DomainSettings.ValidatorsOrganizationName, DomainSettings.RootChainName, null))
                {
                    throw new ChainException("failed to create root chain");
                }
            }
        }

        _archiveContents = new KeyValueStore<Hash, byte[]>(CreateKeyStoreAdapter("contents"));

        this._oracleReader = null;
    }

    public void SetInitialValidators(IEnumerable<Address> initialValidators)
    {
        this._initialValidators = initialValidators;
    }

    public bool HasGenesis()
    {
        var key = GetNexusKey("hash");
        return RootStorage.Has(key);
    }

    public void CommitGenesis(Hash hash)
    {
        Throw.If(HasGenesis(), "genesis already exists");
        var key = GetNexusKey("hash");
        RootStorage.Put(key, hash);
        _genesisValues.Clear();
    }

    public void SetOracleReader(IOracleReader oracleReader)
    {
        this._oracleReader = oracleReader;
    }

    public void Attach(IOracleObserver observer)
    {
        this._observers.Add(observer);
    }

    public void Detach(IOracleObserver observer)
    {
        this._observers.Remove(observer);
    }

    public void Notify(StorageContext storage)
    {
        foreach (var observer in _observers)
        {
            observer.Update(this, storage);
        }
    }

    public bool LoadNexus(StorageContext storage)
    {
        var chainList = this.GetChains(storage);
        foreach (var chainName in chainList)
        {
           var chain = GetChainByName(chainName);
           if (chain is null)
           {
               return false;
           }
        }
        return true;
    }

    private Dictionary<string, IKeyValueStoreAdapter> _keystoreCache = new Dictionary<string, IKeyValueStoreAdapter>();

    public IKeyValueStoreAdapter CreateKeyStoreAdapter(string name)
    {
        if (_keystoreCache.ContainsKey(name))
        {
            return _keystoreCache[name];
        }

        IKeyValueStoreAdapter result;

        if (_adapterFactory != null)
        {
            result = _adapterFactory(name);
            Throw.If(result == null, "keystore adapter factory failed");
        }
        else
        {
            result = new MemoryStore();
        }

        _keystoreCache[name] = result;
        return result;
    }

    public Block FindBlockByTransaction(Transaction tx)
    {
        return FindBlockByTransactionHash(tx.Hash);
    }

    public Block FindBlockByTransactionHash(Hash hash)
    {
        var chainNames = this.GetChains(RootStorage);
        foreach (var chainName in chainNames)
        {
            var chain = GetChainByName(chainName);
            if (chain.ContainsTransaction(hash))
            {
                var blockHash = chain.GetBlockHashOfTransaction(hash);
                return chain.GetBlockByHash(blockHash);
            }
        }

        return null;
    }

    #region NAME SERVICE
    public Address LookUpName(StorageContext storage, string name, Timestamp timestamp)
    {
        if (!ValidationUtils.IsValidIdentifier(name))
        {
            return Address.Null;
        }

        var contract = this.GetContractByName(storage, name);
        if (contract != null)
        {
            return contract.Address;
        }

        var dao = this.GetOrganizationByName(storage, name);
        if (dao != null)
        {
            return dao.Address;
        }

        var chain = RootChain;
        return chain.InvokeContractAtTimestamp(storage, timestamp, NativeContractKind.Account, nameof(AccountContract.LookUpName), name).AsAddress();
    }

    public byte[] LookUpAddressScript(StorageContext storage, Address address, Timestamp timestamp)
    {
        var chain = RootChain;
        return chain.InvokeContractAtTimestamp(storage, timestamp, NativeContractKind.Account, nameof(AccountContract.LookUpScript), address).AsByteArray();
    }

    public bool HasAddressScript(StorageContext storage, Address address, Timestamp timestamp)
    {
        var chain = RootChain;
        return chain.InvokeContractAtTimestamp(storage, timestamp, NativeContractKind.Account, nameof(AccountContract.HasScript), address).AsBool();
    }
    #endregion

    #region CONTRACTS
    public SmartContract GetContractByName(StorageContext storage, string contractName)
    {
        Throw.IfNullOrEmpty(contractName, nameof(contractName));

        if (ValidationUtils.IsValidTicker(contractName))
        {
            var tokenInfo = GetTokenInfo(storage, contractName);
            return new CustomContract(contractName, tokenInfo.Script, tokenInfo.ABI);
        }

        var address = SmartContract.GetAddressFromContractName(contractName);
        var result = NativeContract.GetNativeContractByAddress(address);

        return result;
    }

    #endregion

    #region TRANSACTIONS
    public Transaction FindTransactionByHash(Hash hash)
    {
        var chainNames = this.GetChains(RootStorage);
        foreach (var chainName in chainNames)
        {
            var chain = GetChainByName(chainName);
            var tx = chain.GetTransactionByHash(hash);
            if (tx != null)
            {
                return tx;
            }
        }

        return null;
    }

    #endregion

    #region CHAINS
    public bool CreateChain(StorageContext storage, string organization, string name, string parentChainName)
    {
        if (name != DomainSettings.RootChainName)
        {
            if (string.IsNullOrEmpty(parentChainName))
            {
                return false;
            }

            if (!ChainExists(storage, parentChainName))
            {
                return false;
            }
        }

        if (!ValidationUtils.IsValidIdentifier(name))
        {
            return false;
        }

        // check if already exists something with that name
        if (ChainExists(storage, name))
        {
            return false;
        }

        if (PlatformExists(storage, name))
        {
            return false;
        }

        var chain = new Chain(this, name);

        // add to persistent list of chains
        var chainList = this.GetSystemList(ChainTag, storage);
        chainList.Add(name);

        // add address and name mapping
        storage.Put(ChainNameMapKey + chain.Name, chain.Address.ToByteArray());
        storage.Put(ChainAddressMapKey + chain.Address.Text, Encoding.UTF8.GetBytes(chain.Name));
        storage.Put(ChainOrgKey + chain.Name, Encoding.UTF8.GetBytes(organization));

        if (!string.IsNullOrEmpty(parentChainName))
        {
            storage.Put(ChainParentNameKey + chain.Name, Encoding.UTF8.GetBytes(parentChainName));
            var childrenList = GetChildrenListOfChain(storage, parentChainName);
            childrenList.Add<string>(chain.Name);
        }

        _chainCache[chain.Name] = chain;

        return true;
    }

    public string LookUpChainNameByAddress(Address address)
    {
        var key = ChainAddressMapKey + address.Text;
        if (RootStorage.Has(key))
        {
            var bytes = RootStorage.Get(key);
            return Encoding.UTF8.GetString(bytes);
        }

        return null;
    }

    public bool ChainExists(StorageContext storage, string chainName)
    {
        if (string.IsNullOrEmpty(chainName))
        {
            return false;
        }

        var key = ChainNameMapKey + chainName;
        return storage.Has(key);
    }

    private Dictionary<string, Chain> _chainCache = new Dictionary<string, Chain>();

    public string GetParentChainByAddress(Address address)
    {
        var chain = GetChainByAddress(address);
        if (chain == null)
        {
            return null;
        }
        return GetParentChainByName(chain.Name);
    }

    public string GetParentChainByName(string chainName)
    {
        if (chainName == DomainSettings.RootChainName)
        {
            return null;
        }

        var key = ChainParentNameKey + chainName;
        if (RootStorage.Has(key))
        {
            var bytes = RootStorage.Get(key);
            var parentName = Encoding.UTF8.GetString(bytes);
            return parentName;
        }

        throw new Exception("Parent name not found for chain: " + chainName);
    }

    public string GetChainOrganization(string chainName)
    {
        var key = ChainOrgKey + chainName;
        if (RootStorage.Has(key))
        {
            var bytes = RootStorage.Get(key);
            var orgName = Encoding.UTF8.GetString(bytes);
            return orgName;
        }

        return null;
    }

    public IEnumerable<string> GetChildChainsByAddress(StorageContext storage, Address chainAddress)
    {
        var chain = GetChainByAddress(chainAddress);
        if (chain == null)
        {
            return null;
        }

        return GetChildChainsByName(storage, chain.Name);
    }

    public IOracleReader GetOracleReader()
    {
        Throw.If(_oracleReader == null, "Oracle reader has not been set yet.");
        return _oracleReader;
    }

    public IEnumerable<string> GetChildChainsByName(StorageContext storage, string chainName)
    {
        var list = GetChildrenListOfChain(storage, chainName);
        var count = (int)list.Count();
        var names = new string[count];
        for (int i = 0; i < count; i++)
        {
            names[i] = list.Get<string>(i);
        }

        return names;
    }

    private StorageList GetChildrenListOfChain(StorageContext storage, string chainName)
    {
        var key = Encoding.UTF8.GetBytes(ChainChildrenBlockKey + chainName);
        var list = new StorageList(key, storage);
        return list;
    }

    public IChain GetChainByAddress(Address address)
    {
        var name = LookUpChainNameByAddress(address);
        if (string.IsNullOrEmpty(name))
        {
            return null; // TODO should be exception
        }

        return GetChainByName(name);
    }

    public IChain GetChainByName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (_chainCache.ContainsKey(name))
        {
            return _chainCache[name];
        }

        if (ChainExists(RootStorage, name))
        {
            var chain = new Chain(this, name);
            _chainCache[name] = chain;
            return chain;
        }

        //throw new Exception("Chain not found: " + name);
        return null;
    }

    #endregion

    #region FEEDS
    public bool CreateFeed(StorageContext storage, Address owner, string name, FeedMode mode)
    {
        if (name == null)
        {
            return false;
        }

        if (!owner.IsUser)
        {
            return false;
        }

        // check if already exists something with that name
        if (FeedExists(storage, name))
        {
            return false;
        }

        var feedInfo = new OracleFeed(name, owner, mode);
        EditFeed(storage, name, feedInfo);

        // add to persistent list of feeds
        var feedList = this.GetSystemList(FeedTag, storage);
        feedList.Add(name);

        return true;
    }

    private string GetFeedInfoKey(string name)
    {
        return ".feed:" + name.ToUpper();
    }

    private void EditFeed(StorageContext storage, string name, OracleFeed feed)
    {
        var key = GetFeedInfoKey(name);
        var bytes = Serialization.Serialize(feed);
        storage.Put(key, bytes);
    }

    public bool FeedExists(StorageContext storage, string name)
    {
        var key = GetFeedInfoKey(name);
        return storage.Has(key);
    }

    public OracleFeed GetFeedInfo(StorageContext storage, string name)
    {
        var key = GetFeedInfoKey(name);
        if (storage.Has(key))
        {
            var bytes = storage.Get(key);
            return Serialization.Unserialize<OracleFeed>(bytes);
        }

        throw new ChainException($"Oracle feed does not exist ({name})");
    }
    #endregion

    #region TOKENS

    public IToken CreateToken(StorageContext storage, string symbol, string name, Address owner, BigInteger maxSupply, int decimals, TokenFlags flags, byte[] script, ContractInterface abi = null)
    {
        Throw.IfNull(script, nameof(script));
        Throw.IfNull(abi, nameof(abi));

        var tokenInfo = new TokenInfo(symbol, name, owner, maxSupply, decimals, flags, script, abi);
        EditToken(storage, symbol, tokenInfo);

        // TODO_Migration, migrete TTRS with standard conform script!
        if (symbol == "TTRS")  // support for 22series tokens with a dummy script that conforms to the standard
        {
            byte[] nftScript;
            ContractInterface nftABI;

            var url = "https://www.22series.com/part_info?id=*";
            Tokens.TokenUtils.GenerateNFTDummyScript(symbol, $"{symbol} #*", $"{symbol} #*", url, url, out nftScript, out nftABI);

            CreateSeries(storage, tokenInfo, 0, maxSupply, TokenSeriesMode.Unique, nftScript, nftABI);
        }
        else if (symbol == DomainSettings.RewardTokenSymbol)
        {
            byte[] nftScript;
            ContractInterface nftABI;

            var url = "https://phantasma.io/crown?id=*";
            Tokens.TokenUtils.GenerateNFTDummyScript(symbol, $"{symbol} #*", $"{symbol} #*", url, url, out nftScript, out nftABI);

            CreateSeries(storage, tokenInfo, 0, maxSupply, TokenSeriesMode.Unique, nftScript, nftABI);
        }

        // add to persistent list of tokens
        var tokenList = this.GetSystemList(TokenTag, storage);
        tokenList.Add(symbol);

        // we need to flush every chain ABI cache otherwise calls to the new token methods wont work
        var chainNames = GetChains(RootStorage);
        foreach (var chainName in chainNames)
        {
            var chain = GetChainByName(chainName) as Chain;
            chain.FlushExtCalls();
        }

        return tokenInfo;
    }

    private string GetTokenInfoKey(string symbol)
    {
        return ".token:" + symbol;
    }

    private void EditToken(StorageContext storage, string symbol, TokenInfo tokenInfo)
    {
        var key = GetTokenInfoKey(symbol);
        var bytes = Serialization.Serialize(tokenInfo);
        storage.Put(key, bytes);
    }

    public bool TokenExists(StorageContext storage, string symbol)
    {
        var key = GetTokenInfoKey(symbol);
        return storage.Has(key);
    }

    public bool IsSystemToken(string symbol)
    {
        if (DomainSettings.SystemTokens.Contains(symbol, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public IToken GetTokenInfo(StorageContext storage, string symbol)
    {
        var key = GetTokenInfoKey(symbol);
        if (storage.Has(key))
        {
            var bytes = storage.Get(key);
            var token = Serialization.Unserialize<TokenInfo>(bytes);

            TokenUtils.FetchProperty(storage, this.RootChain, "getOwner", token, (prop, value) =>
            {
                token.Owner = value.AsAddress();
            });

            return token;
        }

        throw new ChainException($"Token does not exist ({symbol})");
    }

    private static readonly string[] _dangerousSymbols = new[]
    {
        DomainSettings.StakingTokenSymbol ,
        DomainSettings.FuelTokenSymbol,
        DomainSettings.FiatTokenSymbol,
        DomainSettings.RewardTokenSymbol,
        "ETH" , "GAS" , "NEO" , "BNB" , "USDT" , "USDC" , "DAI" , "BTC"
    }; 

    public static bool IsDangerousSymbol(string symbol)
    {
        return _dangerousSymbols.Any(x => x.Equals(symbol, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsDangerousAddress(Address address, params Address[] ignoredAddresses)
    {
        foreach (var excludedAddress in ignoredAddresses)
        {
            if (excludedAddress == address)
            {
                return false;
            }
        }

        var nativeContract = NativeContract.GetNativeContractByAddress(address);
        if (nativeContract != null)
        {
            return true;
        }

        foreach (var symbol in _dangerousSymbols)
        {
            var tokenAddress = TokenUtils.GetContractAddress(symbol);
            if (tokenAddress == address)
            {
                return true;
            }
        }

        return false;
    }

    public void MintTokens(IRuntime Runtime, IToken token, Address source, Address destination, string sourceChain, BigInteger amount)
    {
        Runtime.Expect(token.IsFungible(), "must be fungible");
        Runtime.Expect(amount > 0, "invalid amount");

        if (Runtime.HasGenesis)
        {
            if (token.Symbol == DomainSettings.StakingTokenSymbol)
            {
                if (Runtime.ProtocolVersion <= 8)
                {
                    Runtime.ExpectFiltered(Runtime.CurrentContext.Name == NativeContractKind.Stake.GetContractName(), $"minting of {token.Symbol} can only happen via master claim", source);
                }
                else
                {
                    var currentSupply = Runtime.GetTokenSupply(token.Symbol);
                    var totalSupply = currentSupply + amount;
                    var maxSupply = UnitConversion.ToBigInteger(decimal.Parse((100000000 * Math.Pow(1.03, ((DateTime)Runtime.Time).Year - 2018 - 1)).ToString()), DomainSettings.StakingTokenDecimals);

                    if (Runtime.CurrentContext.Name == "entry" && Runtime.IsPrimaryValidator(source) &&
                        Runtime.IsPrimaryValidator(destination))
                    {
                        if (totalSupply <= maxSupply)
                        {
                            Runtime.ExpectWarning(totalSupply <= maxSupply,
                                $"minting of {token.Symbol} can only happen if the amount is lower than 100M", source);
                            Runtime.ExpectWarning(Runtime.IsWitness(token.Owner),
                                $"minting of {token.Symbol} can only happen if the owner of the contract does it.",
                                source);
                            Runtime.ExpectWarning(Runtime.IsPrimaryValidator(source),
                                $"minting of {token.Symbol} can only happen if the owner of the contract does it.",
                                source);
                            Runtime.ExpectWarning(Runtime.IsPrimaryValidator(destination),
                                $"minting of {token.Symbol} can only happen if the destination is a validator.",
                                source);

                            var org = GetOrganizationByName(Runtime.RootStorage, DomainSettings.ValidatorsOrganizationName);
                            Runtime.ExpectWarning(org != null, "moving funds from null org currently not possible",
                                source);

                            var orgMembers = org.GetMembers();
                            // TODO: Check if it needs to be a DAO member
                            //Runtime.ExpectFiltered(orgMembers.Contains(destination), "destination must be a member of the org", destination);
                            Runtime.ExpectWarning(Runtime.Transaction.Signatures.Length == orgMembers.Length,
                                "must be signed by all org members", source);
                            var msg = Runtime.Transaction.ToByteArray(false);
                            foreach (var signature in Runtime.Transaction.Signatures)
                            {
                                Runtime.ExpectWarning(signature.Verify(msg, orgMembers), "invalid signature", source);
                            }
                        }
                    }
                    else
                    {
                        bool isValidContext = Runtime.CurrentContext.Name == NativeContractKind.Stake.GetContractName() ||
                                              Runtime.CurrentContext.Name == NativeContractKind.Gas.GetContractName();
                        bool isValidOrigin = source == SmartContract.GetAddressForNative(NativeContractKind.Stake) || 
                                             source == SmartContract.GetAddressForNative(NativeContractKind.Gas);

                        Runtime.ExpectWarning(isValidContext , $"minting of {token.Symbol} can only happen via master claim", source);
                        //Runtime.ExpectFiltered(source == destination, $"minting of {token.Symbol} can only happen if the owner of the contract.", source);
                        Runtime.ExpectWarning(isValidOrigin, $"minting of {token.Symbol} can only happen if it's the stake or gas address.", source);
                    }
                }
            }
            else if (token.Symbol == DomainSettings.FuelTokenSymbol )
            {
                if (Runtime.ProtocolVersion <= 8)
                {
                    Runtime.ExpectFiltered(Runtime.CurrentContext.Name == NativeContractKind.Stake.GetContractName(), $"minting of {token.Symbol} can only happen via claiming", source);
                }
                else
                {
                    Runtime.ExpectWarning(Runtime.CurrentContext.Name == NativeContractKind.Stake.GetContractName(), $"minting of {token.Symbol} can only happen via claiming", source);
                }
            }
            else
            {
                if (Runtime.ProtocolVersion <= 8)
                {
                    Runtime.ExpectFiltered(!IsDangerousSymbol(token.Symbol), $"minting of {token.Symbol} failed",
                        source);
                }
                else
                {
                    Runtime.ExpectWarning(!IsDangerousSymbol(token.Symbol), $"minting of {token.Symbol} failed", source);
                }
            }
        }

        var isSettlement = sourceChain != Runtime.Chain.Name;

        var supply = new SupplySheet(token.Symbol, Runtime.Chain, this);
        Runtime.Expect(supply.Mint(Runtime.Storage, amount, token.MaxSupply), "mint supply failed");

        var balances = new BalanceSheet(token);
        Runtime.Expect(balances.Add(Runtime.Storage, destination, amount), "balance add failed");

        if (!Runtime.IsSystemToken(token.Symbol))
        {
            // for non system tokens, the onMint trigger is mandatory
            var tokenTrigger = isSettlement ? TokenTrigger.OnReceive : TokenTrigger.OnMint;
            var tokenTriggerResult = Runtime.InvokeTriggerOnToken(true, token, tokenTrigger, source, destination, token.Symbol, amount);
            Runtime.Expect(tokenTriggerResult == TriggerResult.Success, $"token trigger {tokenTrigger} failed or missing");
        }

        var accountTrigger = isSettlement ? AccountTrigger.OnReceive : AccountTrigger.OnMint;
        Runtime.Expect(Runtime.InvokeTriggerOnAccount(true, destination, accountTrigger, source, destination, token.Symbol, amount) != TriggerResult.Failure, $"account trigger {accountTrigger} failed");

        if (isSettlement)
        {
            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, amount, sourceChain));
            Runtime.Notify(EventKind.TokenClaim, destination, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
        }
        else
        {
            Runtime.Notify(EventKind.TokenMint, destination, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
        }
    }

    // NFT version
    public void MintToken(IRuntime Runtime, IToken token, Address source, Address destination, string sourceChain, BigInteger tokenID)
    {
        Runtime.Expect(!token.IsFungible(), "cant be fungible");

        var isSettlement = sourceChain != Runtime.Chain.Name;

        var supply = new SupplySheet(token.Symbol, Runtime.Chain, this);
        Runtime.Expect(supply.Mint(Runtime.Storage, 1, token.MaxSupply), "supply mint failed");

        var ownerships = new OwnershipSheet(token.Symbol);
        Runtime.Expect(ownerships.Add(Runtime.Storage, destination, tokenID), "ownership add failed");

        if (!Runtime.IsSystemToken(token.Symbol))
        {
            // for non system tokens, the onMint trigger is mandatory
            var tokenTrigger = isSettlement ? TokenTrigger.OnReceive : TokenTrigger.OnMint;
            var tokenTriggerResult = Runtime.InvokeTriggerOnToken(true, token, tokenTrigger, source, destination, token.Symbol, tokenID); 
            Runtime.Expect(tokenTriggerResult == TriggerResult.Success, $"token {tokenTrigger} trigger failed or missing");
        }

        var accountTrigger = isSettlement ? AccountTrigger.OnReceive : AccountTrigger.OnMint;
        Runtime.Expect(Runtime.InvokeTriggerOnAccount(true, destination, accountTrigger, source, destination, token.Symbol, tokenID) != TriggerResult.Failure, $"account trigger {accountTrigger} failed");

        var nft = ReadNFT(Runtime, token.Symbol, tokenID);
        WriteNFT(Runtime, token.Symbol, tokenID, Runtime.Chain.Name, source, destination, nft.ROM, nft.RAM,
                    nft.SeriesID, nft.Timestamp, nft.Infusion, !isSettlement);

        if (isSettlement)
        {
            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, tokenID, sourceChain));
            Runtime.Notify(EventKind.TokenClaim, destination, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
        }
        else
        {
            Runtime.Notify(EventKind.TokenMint, destination, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
        }
    }

    private string GetBurnKey(string symbol)
    {
        return $".burned.{symbol}";
    }

    private void Internal_UpdateBurnedSupply(StorageContext storage, string burnKey, BigInteger burnAmount)
    {
        var burnedSupply = storage.Has(burnKey) ? storage.Get<BigInteger>(burnKey) : 0;
        burnedSupply += burnAmount;
        storage.Put<BigInteger>(burnKey, burnedSupply);
    }

    private void UpdateBurnedSupply(StorageContext storage, string symbol, BigInteger burnAmount)
    {
        var burnKey = GetBurnKey(symbol);
        Internal_UpdateBurnedSupply(storage, burnKey, burnAmount);
    }

    private void UpdateBurnedSupplyForSeries(StorageContext storage, string symbol, BigInteger burnAmount, BigInteger seriesID)
    {
        var burnKey = GetBurnKey($"{symbol}.{seriesID}");
        Internal_UpdateBurnedSupply(storage, burnKey, burnAmount);
    }

    public BigInteger GetBurnedTokenSupply(StorageContext storage, string symbol)
    {
        var burnKey = GetBurnKey(symbol);
        var burnedSupply = storage.Has(burnKey) ? storage.Get<BigInteger>(burnKey) : 0;
        return burnedSupply;
    }

    public BigInteger GetBurnedTokenSupplyForSeries(StorageContext storage, string symbol, BigInteger seriesID)
    {
        var burnKey = GetBurnKey($"{symbol}.{seriesID}");
        var burnedSupply = storage.Has(burnKey) ? storage.Get<BigInteger>(burnKey) : 0;
        return burnedSupply;
    }

    public void BurnTokens(IRuntime Runtime, IToken token, Address source, Address destination, string targetChain, BigInteger amount)
    {
        Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "must be fungible");

        Runtime.Expect(amount > 0, "invalid amount");

        var allowed = Runtime.IsWitness(source);

        Runtime.CheckFilterAmountThreshold(token, source, amount, "Burn Tokens");

#if ALLOWANCE_OPERATIONS
        if (!allowed)
        {
            allowed = Runtime.SubtractAllowance(source, token.Symbol, amount);
        }
#endif

        Runtime.Expect(allowed, "invalid witness or allowance");

        var isSettlement = targetChain != Runtime.Chain.Name;

        var supply = new SupplySheet(token.Symbol, Runtime.Chain, this);

        Runtime.Expect(supply.Burn(Runtime.Storage, amount), $"{token.Symbol} burn failed");

        var balances = new BalanceSheet(token);
        Runtime.Expect(balances.Subtract(Runtime.Storage, source, amount), $"{token.Symbol} balance subtract failed from {source.Text}");

        // If trigger is missing the code will be executed
        Runtime.Expect(Runtime.InvokeTriggerOnToken(true, token, isSettlement ? TokenTrigger.OnSend : TokenTrigger.OnBurn, source, destination, token.Symbol, amount) != TriggerResult.Failure, "token trigger failed");

        // If trigger is missing the code will be executed
        Runtime.Expect(Runtime.InvokeTriggerOnAccount(true, source, isSettlement ? AccountTrigger.OnSend : AccountTrigger.OnBurn, source, destination, token.Symbol, amount) != TriggerResult.Failure, "account trigger failed");

        if (isSettlement)
        {
            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
            Runtime.Notify(EventKind.TokenStake, destination, new TokenEventData(token.Symbol, amount, targetChain));
        }
        else
        {
            UpdateBurnedSupply(Runtime.Storage, token.Symbol, amount);
            Runtime.Notify(EventKind.TokenBurn, source, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
        }
    }

    // NFT version
    public void BurnToken(IRuntime Runtime, IToken token, Address source, Address destination, string targetChain, BigInteger tokenID)
    {
        Runtime.Expect(!token.Flags.HasFlag(TokenFlags.Fungible), $"{token.Symbol} can't be fungible");

        var isSettlement = targetChain != Runtime.Chain.Name;

        var nft = Runtime.ReadToken(token.Symbol, tokenID);
        Runtime.Expect(nft.CurrentOwner != Address.Null, "nft already destroyed");
        Runtime.Expect(nft.CurrentChain == Runtime.Chain.Name, "not on this chain");

        Runtime.Expect(nft.CurrentOwner == source, $"{source} is not the owner of {token.Symbol} #{tokenID}");

        Runtime.Expect(source != DomainSettings.InfusionAddress, $"{token.Symbol} #{tokenID} is currently infused");

        var chain = RootChain;
        var supply = new SupplySheet(token.Symbol, chain, this);

        Runtime.Expect(supply.Burn(Runtime.Storage, 1), "supply burning failed");
        
        if (Runtime.ProtocolVersion <= DomainSettings.Phantasma30Protocol)
        {
            DestroyNFTIfSettlement(Runtime, token, source, destination, tokenID, isSettlement);
            
            var ownerships = new OwnershipSheet(token.Symbol);
            Runtime.Expect(ownerships.Remove(Runtime.Storage, source, tokenID), "ownership removal failed");

            ValidateBurnTriggers(Runtime, token, source, destination, targetChain, tokenID, isSettlement);
            
        }
        else
        {
            ValidateBurnTriggers(Runtime, token, source, destination, targetChain, tokenID, isSettlement);
            
            DestroyNFTIfSettlement(Runtime, token, source, destination, tokenID, isSettlement);
            
            var ownerships = new OwnershipSheet(token.Symbol);
            Runtime.Expect(ownerships.Remove(Runtime.Storage, source, tokenID), "ownership removal failed");
        }

        if (isSettlement)
        {
            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
            Runtime.Notify(EventKind.TokenStake, destination, new TokenEventData(token.Symbol, tokenID, targetChain));
            Runtime.Notify(EventKind.PackedNFT, destination, new PackedNFTData(token.Symbol, nft.ROM, nft.RAM));
        }
        else
        {
            UpdateBurnedSupply(Runtime.Storage, token.Symbol, 1);
            UpdateBurnedSupplyForSeries(Runtime.Storage, token.Symbol, 1, nft.SeriesID);
            Runtime.Notify(EventKind.TokenBurn, source, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
        }
    }

    /// <summary>
    /// To validate the burn triggers to call the onBurn trigger on the token and on the account
    /// </summary>
    /// <param name="Runtime"></param>
    /// <param name="token"></param>
    /// <param name="source"></param>
    /// <param name="destination"></param>
    /// <param name="targetChain"></param>
    /// <param name="tokenID"></param>
    /// <param name="isSettlement"></param>
    private void ValidateBurnTriggers(IRuntime Runtime, IToken token, Address source, Address destination, string targetChain, BigInteger tokenID, bool isSettlement )
    {
        // If trigger is missing the code will be executed
        var tokenTrigger = isSettlement ? TokenTrigger.OnSend : TokenTrigger.OnBurn;
        Runtime.Expect(Runtime.InvokeTriggerOnToken(true, token, tokenTrigger, source, destination, token.Symbol, tokenID) != TriggerResult.Failure, $"token {tokenTrigger} trigger failed: ");
        
        // If trigger is missing the code will be executed
        var accountTrigger = isSettlement ? AccountTrigger.OnSend : AccountTrigger.OnBurn;
        Runtime.Expect(Runtime.InvokeTriggerOnAccount(true, source, accountTrigger, source, destination, token.Symbol, tokenID) != TriggerResult.Failure, $"accont {accountTrigger} trigger failed: ");
    }
    
    private void DestroyNFTIfSettlement(IRuntime Runtime, IToken token, Address source, Address destination, BigInteger tokenID, bool isSettlement)
    {
        if (!isSettlement)
        {
            Runtime.Expect(source == destination, "source and destination must match when burning");
            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");
            DestroyNFT(Runtime, token.Symbol, tokenID, source);
        }
    }

    public void InfuseToken(IRuntime Runtime, IToken token, Address from, BigInteger tokenID, IToken infuseToken, BigInteger value)
    {
        Runtime.Expect(!token.Flags.HasFlag(TokenFlags.Fungible), "can't be fungible");

        var nft = Runtime.ReadToken(token.Symbol, tokenID);
        Runtime.Expect(nft.CurrentOwner != Address.Null, "nft already destroyed");
        Runtime.Expect(nft.CurrentOwner == from, "nft does not belong to " + from);
        Runtime.Expect(nft.CurrentChain == Runtime.Chain.Name, "not on this chain");

        if (token.Symbol == infuseToken.Symbol)
        {
            Runtime.Expect(value != tokenID, "cannot infuse token into itself");
        }

        var target = DomainSettings.InfusionAddress;

        // If trigger is missing the code will be executed
        var tokenTrigger = TokenTrigger.OnInfuse;
        Runtime.Expect(Runtime.InvokeTriggerOnToken(true, token, tokenTrigger, from, target, infuseToken.Symbol, value) != TriggerResult.Failure, $"token {tokenTrigger} trigger failed: ");
        
        if (infuseToken.IsFungible())
        {
            Runtime.CheckFilterAmountThreshold(infuseToken, from, value, "Infuse Tokens");
            this.TransferTokens(Runtime, infuseToken, from, target, value, true);
        }
        else
        {
            this.TransferToken(Runtime, infuseToken, from, target, value, true);
        }

        int index = -1;

        if (infuseToken.IsFungible())
        {
            for (int i = 0; i < nft.Infusion.Length; i++)
            {
                if (nft.Infusion[i].Symbol == infuseToken.Symbol)
                {
                    index = i;
                    break;
                }
            }
        }

        var infusion = nft.Infusion.ToList();

        if (index < 0)
        {
            infusion.Add(new TokenInfusion(infuseToken.Symbol, value));
        }
        else
        {
            var temp = nft.Infusion[index];
            infusion[index] = new TokenInfusion(infuseToken.Symbol, value + temp.Value);
        }

        WriteNFT(Runtime, token.Symbol, tokenID, nft.CurrentChain, nft.Creator, nft.CurrentOwner, nft.ROM, nft.RAM,
                nft.SeriesID, nft.Timestamp, infusion, true);

        Runtime.Notify(EventKind.Infusion, nft.CurrentOwner, new InfusionEventData(token.Symbol, tokenID, infuseToken.Symbol, value, nft.CurrentChain));
    }

    public void TransferTokens(IRuntime Runtime, IToken token, Address source, Address destination, BigInteger amount, bool isInfusion = false)
    {
        Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "Not transferable");
        Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "must be fungible");

        Runtime.Expect(amount > 0, "invalid amount");
        Runtime.Expect(source != destination, "source and destination must be different");
        Runtime.Expect(!destination.IsNull, "invalid destination");

        if (destination.IsSystem)
        {
            var destName = Runtime.Chain.GetNameFromAddress(Runtime.Storage, destination, Runtime.Time);
            Runtime.Expect(destName != ValidationUtils.ANONYMOUS_NAME, "anonymous system address as destination");
        }

        bool isOrganaizationTransaction = false;
        if (source.IsSystem)
        {
            var org = GetOrganizationByAddress(Runtime.RootStorage, source);
            if (org != null)
            {
                if ( Runtime.ProtocolVersion <= 8 )
                    Runtime.ExpectFiltered(org == null, "moving funds from orgs currently not possible", source);
                else
                {
                    Runtime.ExpectWarning(org != null, "moving funds from orgs currently not possible", source);
                    var orgMembers = org.GetMembers();
                    // TODO: Check if it needs to be a DAO member
                    //Runtime.ExpectFiltered(orgMembers.Contains(destination), "destination must be a member of the org", destination);
                    Runtime.ExpectWarning(Runtime.Transaction.Signatures.Length == orgMembers.Length, "must be signed by all org members", source);
                    var msg = Runtime.Transaction.ToByteArray(false);
                    foreach (var signature in Runtime.Transaction.Signatures)
                    {
                        Runtime.ExpectWarning(signature.Verify(msg, orgMembers), "invalid signature", source);
                    }

                    isOrganaizationTransaction = true;
                }
            }
            else
            if (source == DomainSettings.InfusionAddress)
            {
                Runtime.Expect(!_infusionOperationAddress.IsNull, "infusion address is currently locked");
                Runtime.Expect(destination == _infusionOperationAddress, "not valid target for infusion address transfer");
            }
            else
            {
                Runtime.Expect(Runtime.CurrentContext.Name != VirtualMachine.EntryContextName, "moving funds from system address if forbidden");

                var sourceContract = Runtime.Chain.GetContractByAddress(Runtime.Storage, source);
                Runtime.Expect(sourceContract != null, "cannot find matching contract for address: " + source);

                var isKnownExceptionToRule = false;

                if (Runtime.CurrentContext.Name == NativeContractKind.Stake.GetContractName())
                {
                    if (IsNativeContract(sourceContract.Name))
                    {
                        isKnownExceptionToRule = true;
                    }
                    else
                    if (TokenExists(Runtime.RootStorage, sourceContract.Name))
                    {
                        isKnownExceptionToRule = true;
                    }
                }

                if (!isKnownExceptionToRule)
                {
                    Runtime.Expect(Runtime.CurrentContext.Name == sourceContract.Name, "moving funds from a contract is forbidden if not made by the contract itself");
                }
            }
        }

        if (Runtime.HasGenesis)
        {
            var isSystemDestination = destination.IsSystem && NativeContract.GetNativeContractByAddress(destination) != null;
            var isSystemSource = source.IsSystem;
            if (Runtime.ProtocolVersion <= 8)
            {
                if (!isSystemDestination)
                {
                    Runtime.CheckFilterAmountThreshold(token, source, amount, "Transfer Tokens");
                }
            }
            else
            {
                if ( !isSystemDestination && !isSystemSource )
                {
                    Runtime.CheckFilterAmountThreshold(token, source, amount, "Transfer Tokens");
                }
                else if (isSystemSource && !isSystemDestination)
                {
                    if ( !isOrganaizationTransaction )
                        Runtime.CheckFilterAmountThreshold(token, source, amount, "Transfer Tokens");
                    else
                        Runtime.ExpectWarning(Runtime.IsWitness(source), "source is system address and not a witness", source);
                }else if (!isSystemDestination)
                {
                    Runtime.CheckFilterAmountThreshold(token, source, amount, "Transfer Tokens");
                }
            }
        }

        bool allowed;

        if (Runtime.HasGenesis)
        {
            if (Runtime.ProtocolVersion <= 8)
            {
                allowed = Runtime.IsWitness(source);
            }
            else if (isOrganaizationTransaction)
            {
                allowed = true;
            }
            else
            {
                allowed = Runtime.IsWitness(source);
            }
        }
        else
        {
            allowed = Runtime.IsPrimaryValidator(source);
        }

#if ALLOWANCE_OPERATIONS
        if (!allowed)
        {
            allowed = Runtime.SubtractAllowance(source, token.Symbol, amount);
        }
#endif

        if (!allowed && source == DomainSettings.InfusionAddress && destination == _infusionOperationAddress)
        {
            allowed = true;
        }

        Runtime.Expect(allowed, "invalid witness or allowance");

        var balances = new BalanceSheet(token);
        Runtime.Expect(balances.Subtract(Runtime.Storage, source, amount), $"{token.Symbol} balance subtract failed from {source.Text}");
        Runtime.Expect(balances.Add(Runtime.Storage, destination, amount), $"{token.Symbol} balance add failed to {destination.Text}");

#if ALLOWANCE_OPERATIONS
        Runtime.AddAllowance(destination, token.Symbol, amount);
#endif

        Runtime.Expect(Runtime.InvokeTriggerOnToken(true, token, TokenTrigger.OnSend, source, destination, token.Symbol, amount) != TriggerResult.Failure, "token onSend trigger failed");
        Runtime.Expect(Runtime.InvokeTriggerOnToken(true, token, TokenTrigger.OnReceive, source, destination, token.Symbol, amount) != TriggerResult.Failure, "token onReceive trigger failed");

        Runtime.Expect(Runtime.InvokeTriggerOnAccount(true, source, AccountTrigger.OnSend, source, destination, token.Symbol, amount) != TriggerResult.Failure, "account onSend trigger failed");
        Runtime.Expect(Runtime.InvokeTriggerOnAccount(true, destination, AccountTrigger.OnReceive, source, destination, token.Symbol, amount) != TriggerResult.Failure, "account onReceive trigger failed");

#if ALLOWANCE_OPERATIONS
        Runtime.RemoveAllowance(destination, token.Symbol);
#endif

        if (destination.IsSystem && (destination == Runtime.CurrentContext.Address || isInfusion))
        {
            Runtime.Notify(EventKind.TokenStake, source, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
        }
        else if (source.IsSystem && (source == Runtime.CurrentContext.Address || isInfusion))
        {
            Runtime.Notify(EventKind.TokenClaim, destination, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
        }
        else
        {
            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
            Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
        }
    }

    public void TransferToken(IRuntime Runtime, IToken token, Address source, Address destination, BigInteger tokenID, bool isInfusion = false)
    {
        Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "Not transferable");
        Runtime.Expect(!token.Flags.HasFlag(TokenFlags.Fungible), "Should be non-fungible");

        Runtime.Expect(tokenID > 0, "invalid nft id");

        Runtime.Expect(source != destination, "source and destination must be different");

        Runtime.Expect(!destination.IsNull, "destination cant be null");

        var nft = ReadNFT(Runtime, token.Symbol, tokenID);
        Runtime.Expect(nft.CurrentOwner != Address.Null, "nft already destroyed");

        var ownerships = new OwnershipSheet(token.Symbol);
        Runtime.Expect(ownerships.Remove(Runtime.Storage, source, tokenID), "ownership remove failed");

        Runtime.Expect(ownerships.Add(Runtime.Storage, destination, tokenID), "ownership add failed");

        Runtime.Expect(Runtime.InvokeTriggerOnToken(true, token, TokenTrigger.OnSend, source, destination, token.Symbol, tokenID) != TriggerResult.Failure, "token send trigger failed");

        Runtime.Expect(Runtime.InvokeTriggerOnToken(true, token, TokenTrigger.OnReceive, source, destination, token.Symbol, tokenID) != TriggerResult.Failure, "token receive trigger failed");

        Runtime.Expect(Runtime.InvokeTriggerOnAccount(true, source, AccountTrigger.OnSend, source, destination, token.Symbol, tokenID) != TriggerResult.Failure, "account send trigger failed");

        Runtime.Expect(Runtime.InvokeTriggerOnAccount(true, destination, AccountTrigger.OnReceive, source, destination, token.Symbol, tokenID) != TriggerResult.Failure, "account received trigger failed");

        WriteNFT(Runtime, token.Symbol, tokenID, Runtime.Chain.Name, nft.Creator, destination, nft.ROM, nft.RAM,
                nft.SeriesID, Runtime.Time, nft.Infusion, true);

        if (destination.IsSystem && (destination == Runtime.CurrentContext.Address || isInfusion))
        {
            Runtime.Notify(EventKind.TokenStake, source, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
        }
        else if (source.IsSystem && (source == Runtime.CurrentContext.Address || isInfusion))
        {
            Runtime.Notify(EventKind.TokenClaim, destination, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
        }
        else
        {
            Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
            Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
        }
    }

    #endregion

    #region NFT

    public byte[] GetKeyForNFT(string symbol, BigInteger tokenID)
    {
        return GetKeyForNFT(symbol, tokenID.ToString());
    }

    public byte[] GetKeyForNFT(string symbol, string key)
    {
        var tokenKey = SmartContract.GetKeyForField(symbol, key, false);
        return tokenKey;
    }

    private StorageList GetSeriesList(StorageContext storage, string symbol)
    {
        var key = System.Text.Encoding.ASCII.GetBytes("series." + symbol);
        return new StorageList(key, storage);
    }

    public BigInteger[] GetAllSeriesForToken(StorageContext storage, string symbol)
    {
        var list = GetSeriesList(storage, symbol);
        return list.All<BigInteger>();
    }

    public TokenSeries CreateSeries(StorageContext storage, IToken token, BigInteger seriesID, BigInteger maxSupply, TokenSeriesMode mode, byte[] script, ContractInterface abi)
    {
        if (token.IsFungible())
        {
            throw new ChainException($"Can't create series for fungible token");
        }

        var key = GetTokenSeriesKey(token.Symbol, seriesID);

        if (storage.Has(key))
        {
            throw new ChainException($"Series {seriesID} of token {token.Symbol} already exist");
        }

        if (token.IsCapped() && maxSupply < 1)
        {
            throw new ChainException($"Token series supply must be 1 or more");
        }

        var nftStandard = Tokens.TokenUtils.GetNFTStandard();

        if (!abi.Implements(nftStandard))
        {
            throw new ChainException($"Token series abi does not implement the NFT standard");
        }

        var series = new TokenSeries(0, maxSupply, mode, script, abi, null);
        WriteTokenSeries(storage, token.Symbol, seriesID, series);

        var list = GetSeriesList(storage, token.Symbol);
        list.Add(seriesID);

        return series;
    }

    public byte[] GetTokenSeriesKey(string symbol, BigInteger seriesID)
    {
        return GetKeyForNFT(symbol, $"serie{seriesID}");
    }

    public TokenSeries GetTokenSeries(StorageContext storage, string symbol, BigInteger seriesID)
    {
        var key = GetTokenSeriesKey(symbol, seriesID);

        if (storage.Has(key))
        {
            return storage.Get<TokenSeries>(key);
        }

        return null;
    }

    private void WriteTokenSeries(StorageContext storage, string symbol, BigInteger seriesID, ITokenSeries series)
    {
        var key = GetTokenSeriesKey(symbol, seriesID);
        storage.Put<TokenSeries>(key, (TokenSeries)series);
    }

    public BigInteger GenerateNFT(IRuntime Runtime, string symbol, string chainName, Address targetAddress, byte[] rom, byte[] ram, BigInteger seriesID)
    {
        Runtime.Expect(ram != null, "invalid nft ram");

        Runtime.Expect(seriesID >= 0, "invalid series ID");

        var series = GetTokenSeries(Runtime.RootStorage, symbol, seriesID);
        Runtime.Expect(series != null, $"{symbol} series {seriesID} does not exist");

        BigInteger mintID = series.GenerateMintID();
        Runtime.Expect(mintID > 0, "invalid mintID generated");

        if (series.Mode == TokenSeriesMode.Duplicated)
        {
            if (mintID > 1)
            {
                if (rom == null || rom.Length == 0)
                {
                    rom = series.ROM;
                }
                else
                {
                    Runtime.Expect(ByteArrayUtils.CompareBytes(rom, series.ROM), $"rom can't be unique in {symbol} series {seriesID}");
                }
            }
            else
            {
                series.SetROM(rom);
            }

            rom = new byte[0];
        }
        else
        {
            Runtime.Expect(rom != null && rom.Length > 0, "invalid nft rom");
        }

        WriteTokenSeries(Runtime.RootStorage, symbol, seriesID, series);

        var token = Runtime.GetToken(symbol);

        if (series.MaxSupply > 0)
        {
            Runtime.Expect(mintID <= series.MaxSupply, $"{symbol} series {seriesID} reached max supply already");
        }
        else
        {
            Runtime.Expect(!token.IsCapped(), $"{symbol} series {seriesID} max supply is not defined yet");
        }

        var content = new TokenContent(seriesID, mintID, chainName, targetAddress, targetAddress, rom, ram, Runtime.Time, null, series.Mode);

        var tokenKey = GetKeyForNFT(symbol, content.TokenID);
        Runtime.Expect(!Runtime.Storage.Has(tokenKey), "duplicated nft");

        var contractAddress = token.GetContractAddress();

        var bytes = content.ToByteArray();
        bytes = CompressionUtils.Compress(bytes);

        Runtime.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.WriteData), contractAddress, tokenKey, bytes);

        return content.TokenID;
    }

    private Address _infusionOperationAddress = Address.Null;

    private void DoInfusionOperation(Address targetAdress, Action callback)
    {
        _infusionOperationAddress = targetAdress;
        callback();
        _infusionOperationAddress = Address.Null;
    }


    public void DestroyNFT(IRuntime Runtime, string symbol, BigInteger tokenID, Address target)
    {
        var infusionAddress = DomainSettings.InfusionAddress;

        var tokenContent = ReadNFT(Runtime, symbol, tokenID);

        foreach (var asset in tokenContent.Infusion)
        {
            var assetInfo = this.GetTokenInfo(Runtime.RootStorage, asset.Symbol);

#if ALLOWANCE_OPERATIONS
            Runtime.AddAllowance(infusionAddress, asset.Symbol, asset.Value);
#endif

            DoInfusionOperation(target, () =>
            {
                if (assetInfo.IsFungible())
                {
                    Runtime.CheckFilterAmountThreshold(assetInfo, target, asset.Value, "Burn Token (DestroyNFT)");
                    this.TransferTokens(Runtime, assetInfo, infusionAddress, target, asset.Value, true);
                }
                else
                {
                    this.TransferToken(Runtime, assetInfo, infusionAddress, target, asset.Value, true);
                }
            });

#if ALLOWANCE_OPERATIONS
            Runtime.RemoveAllowance(infusionAddress, asset.Symbol);
#endif
        }

        var token = Runtime.GetToken(symbol);
        var contractAddress = token.GetContractAddress();

        var tokenKey = GetKeyForNFT(symbol, tokenID);

        Runtime.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.DeleteData), contractAddress, tokenKey);
    }

    public void WriteNFT(IRuntime Runtime, string symbol, BigInteger tokenID, string chainName, Address creator,
            Address owner, byte[] rom, byte[] ram, BigInteger seriesID, Timestamp timestamp,
            IEnumerable<TokenInfusion> infusion, bool mustExist)
    {
        Runtime.Expect(ram != null && ram.Length < TokenContent.MaxRAMSize, "invalid nft ram update");

        var tokenKey = GetKeyForNFT(symbol, tokenID);

        if (Runtime.RootStorage.Has(tokenKey))
        {
            var content = ReadNFTRaw(Runtime.RootStorage, tokenKey, Runtime.ProtocolVersion);

            var series = GetTokenSeries(Runtime.RootStorage, symbol, content.SeriesID);
            Runtime.Expect(series != null, $"could not find series {seriesID} for {symbol}");

            switch (series.Mode)
            {
                case TokenSeriesMode.Unique:
                    Runtime.Expect(rom.CompareBytes(content.ROM), "rom does not match original value");
                    break;

                case TokenSeriesMode.Duplicated:
                    Runtime.Expect(rom.Length == 0 || rom.CompareBytes(series.ROM), "rom does not match original value");
                    break;

                default:
                    throw new ChainException("WriteNFT: unsupported series mode: " + series.Mode);
            }

            content = new TokenContent(content.SeriesID, content.MintID, chainName, content.Creator, owner, content.ROM, ram, timestamp, infusion, series.Mode);

            var token = Runtime.GetToken(symbol);
            var contractAddress = token.GetContractAddress();

            var bytes = content.ToByteArray();
            bytes = CompressionUtils.Compress(bytes);

            Runtime.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.WriteData), contractAddress, tokenKey, bytes);
        }
        else
        {
            Runtime.Expect(!mustExist, $"nft {symbol} {tokenID} does not exist");
            Address _creator = creator;

            var genID = GenerateNFT(Runtime, symbol, chainName, _creator, rom, ram, seriesID);
            Runtime.Expect(genID == tokenID, "failed to regenerate NFT");
        }
    }

    public TokenContent ReadNFT(IRuntime Runtime, string symbol, BigInteger tokenID)
    {
        return ReadNFT(Runtime.RootStorage, symbol, tokenID, Runtime.ProtocolVersion);
    }

    public TokenContent ReadNFT(StorageContext storage, string symbol, BigInteger tokenID)
    {
        var protocol = this.GetProtocolVersion(storage);
        return ReadNFT(storage, symbol, tokenID, protocol);
    }

    private TokenContent ReadNFTRaw(StorageContext storage, byte[] tokenKey, uint ProtocolVersion)
    {
        var bytes = storage.Get(tokenKey);

        bytes = CompressionUtils.Decompress(bytes);

        var content = Serialization.Unserialize<TokenContent>(bytes);
        return content;
    }

    private TokenContent ReadNFT(StorageContext storage, string symbol, BigInteger tokenID, uint ProtocolVersion)
    {
        var tokenKey = GetKeyForNFT(symbol, tokenID);

        Throw.If(!storage.Has(tokenKey), $"nft {symbol} {tokenID} does not exist");

        var content = ReadNFTRaw(storage, tokenKey, ProtocolVersion);

        var series = GetTokenSeries(storage, symbol, content.SeriesID);

        content.UpdateTokenID(series.Mode);

        if (series.Mode == TokenSeriesMode.Duplicated)
        {
            content.ReplaceROM(series.ROM);
        }
        return content;
    }

    public bool HasNFT(StorageContext storage, string symbol, BigInteger tokenID)
    {
        var tokenKey = GetKeyForNFT(symbol, tokenID);
        return storage.Has(tokenKey);
    }
    #endregion

    #region GENESIS

    private void DeployNativeContract(ScriptBuilder sb, PhantasmaKeys owner, NativeContractKind nativeContract)
    {
        if (nativeContract == NativeContractKind.Unknown)
        {
            throw new ChainException("Invalid native contract: " + nativeContract);
        }

        var script = new byte[] { (byte)Opcode.RET };
        var abi = new byte[0] { };

        var contractName = nativeContract.GetContractName();

        sb.CallInterop("Runtime.DeployContract", owner.Address, contractName, script, abi);
    }

    private Transaction NexusCreateTx(PhantasmaKeys owner, Timestamp genesisTime, int protocolVersion)
    {
        var sb = ScriptUtils.BeginScript();

        sb.CallInterop("Nexus.BeginInit", owner.Address);

        if (!_migratingNexus)
        {
            DeployNativeContract(sb, owner, NativeContractKind.Validator);
            DeployNativeContract(sb, owner, NativeContractKind.Governance);
            DeployNativeContract(sb, owner, NativeContractKind.Consensus);
            DeployNativeContract(sb, owner, NativeContractKind.Account);
            DeployNativeContract(sb, owner, NativeContractKind.Exchange);
            DeployNativeContract(sb, owner, NativeContractKind.Swap);
            DeployNativeContract(sb, owner, NativeContractKind.Stake);
            DeployNativeContract(sb, owner, NativeContractKind.Storage);
            DeployNativeContract(sb, owner, NativeContractKind.Market);
            DeployNativeContract(sb, owner, NativeContractKind.Sale);
            DeployNativeContract(sb, owner, NativeContractKind.Relay);
            DeployNativeContract(sb, owner, NativeContractKind.Ranking);
            DeployNativeContract(sb, owner, NativeContractKind.Mail);
            DeployNativeContract(sb, owner, NativeContractKind.Friends);
        }

        _genesisValues[NexusProtocolVersionTag] = new KeyValuePair<BigInteger, ChainConstraint[]>(protocolVersion, new ChainConstraint[]
        {
            new ChainConstraint() { Kind = ConstraintKind.MustIncrease}
        });

        foreach (var entry in _genesisValues)
        {
            var name = entry.Key;
            var initial = entry.Value.Key;
            var constraints = entry.Value.Value;
            var bytes = Serialization.Serialize(constraints);
            sb.CallContract(NativeContractKind.Governance, nameof(GovernanceContract.CreateValue), owner.Address, name, initial, bytes);
        }

        if (!_migratingNexus)
        {
            var orgInterop = "Nexus.CreateOrganization";
            var orgScript = new byte[0];
            sb.CallInterop(orgInterop, owner.Address, DomainSettings.ValidatorsOrganizationName, "Block Producers", orgScript);
            sb.CallInterop(orgInterop, owner.Address, DomainSettings.MastersOrganizationName, "Soul Masters", orgScript);
            sb.CallInterop(orgInterop, owner.Address, DomainSettings.StakersOrganizationName, "Soul Stakers", orgScript);
            sb.CallInterop(orgInterop, owner.Address, DomainSettings.PhantomForceOrganizationName, "Phantom Force", orgScript);
        }

        var validatorInitialBalance = StakeContract.DefaultMasterThreshold;

        if (_migratingNexus)
        {
            BigInteger swapAmount = 40000000; // legacy NEP5 supply
            swapAmount /= _initialValidators.Count();
            validatorInitialBalance += swapAmount;
        }
        else
        if (Name != DomainSettings.NexusMainnet)
        {
            validatorInitialBalance *= 10; // extra funding for testnet / simnet
        }

        // initial SOUL distribution to validators
        foreach (var validator in _initialValidators)
        {
            sb.MintTokens(DomainSettings.StakingTokenSymbol, owner.Address, validator, validatorInitialBalance);
            sb.MintTokens(DomainSettings.FuelTokenSymbol, owner.Address, validator, UnitConversion.ToBigInteger(10000, DomainSettings.FuelTokenDecimals));

            // requires staking token to be created previously
            sb.CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), validator, StakeContract.DefaultMasterThreshold);
            //sb.CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim), owner.Address, owner.Address);
        }

        var orgFunding = UnitConversion.ToBigInteger(1863626, DomainSettings.StakingTokenDecimals);
        var orgAddress = Organization.GetAddressFromID(DomainSettings.PhantomForceOrganizationName);
        sb.MintTokens(DomainSettings.StakingTokenSymbol, owner.Address, orgAddress, UnitConversion.ToBigInteger(1214623, DomainSettings.StakingTokenDecimals));

        //sb.MintTokens(DomainSettings.StakingTokenSymbol, owner.Address, owner.Address, UnitConversion.ToBigInteger(2863626, DomainSettings.StakingTokenDecimals));
        //sb.MintTokens(DomainSettings.FuelTokenSymbol, owner.Address, owner.Address, UnitConversion.ToBigInteger(1000000, DomainSettings.FuelTokenDecimals));

        sb.CallContract(NativeContractKind.Validator, nameof(ValidatorContract.SetValidator), owner.Address, new BigInteger(0), ValidatorType.Primary);

        var index = 1;
        foreach (var validator in _initialValidators.Where(x => x != owner.Address))
        {
            sb.CallContract(NativeContractKind.Validator, nameof(ValidatorContract.SetValidator), validator, new BigInteger(index), ValidatorType.Primary);
            index++;
        }
        
        // Deploy LP Contract
        if (this.GetProtocolVersion(RootStorage) <= 8)
        {
            sb.CallInterop("Nexus.CreateToken", owner.Address, Base16.Decode(OLD_LP_CONTRACT_PVM),  Base16.Decode(OLD_LP_CONTRACT_ABI));
        }
        else
        {
            sb.CallInterop("Nexus.CreateToken", owner.Address, Base16.Decode(NEW_LP_CONTRACT_PVM),  Base16.Decode(NEW_LP_CONTRACT_ABI));
        }

        sb.CallInterop("Nexus.EndInit", owner.Address);

        var script = sb.EndScript();

        var tx = new Transaction(this.Name, DomainSettings.RootChainName, script, genesisTime + TimeSpan.FromDays(1000));
        tx.Mine(ProofOfWork.Minimal);
        tx.Sign(owner);

        return tx;
    }

    public void BeginInitialize(IRuntime vm, Address owner)
    {
        var storage = RootStorage;

        storage.Put(GetNexusKey("owner"), owner);

        var tokenScript = new byte[] { (byte)Opcode.RET };
        var abi = ContractInterface.Empty;

        if (!_migratingNexus)
        {
            CreateToken(storage, DomainSettings.StakingTokenSymbol, DomainSettings.StakingTokenName, owner, 0, DomainSettings.StakingTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Stakable, tokenScript, abi);
            CreateToken(storage, DomainSettings.FuelTokenSymbol, DomainSettings.FuelTokenName, owner, 0, DomainSettings.FuelTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Burnable | TokenFlags.Fuel, tokenScript, abi);
            CreateToken(storage, DomainSettings.RewardTokenSymbol, DomainSettings.RewardTokenName, owner, 0, 0, TokenFlags.Transferable | TokenFlags.Burnable, tokenScript, abi);
            CreateToken(storage, DomainSettings.FiatTokenSymbol, DomainSettings.FiatTokenName, owner, 0, DomainSettings.FiatTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Fiat, tokenScript, abi);

            CreateToken(storage, "NEO", "NEO", owner, UnitConversion.ToBigInteger(100000000, 0), 0, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite, tokenScript, abi);
            CreateToken(storage, "GAS", "GAS", owner, UnitConversion.ToBigInteger(100000000, 8), 8, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Finite, tokenScript, abi);
            CreateToken(storage, "ETH", "Ethereum", owner, UnitConversion.ToBigInteger(0, 18), 18, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible, tokenScript, abi);
            //CreateToken(storage, "DAI", "Dai Stablecoin", owner, UnitConversion.ToBigInteger(0, 18), 18, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Foreign, tokenScript, abi);
            //GenerateToken(_owner, "EOS", "EOS", "EOS", UnitConversion.ToBigInteger(1006245120, 18), 18, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible | TokenFlags.External, tokenScript, abi);

            SetPlatformTokenHash(DomainSettings.StakingTokenSymbol, "neo", Hash.FromUnpaddedHex("ed07cffad18f1308db51920d99a2af60ac66a7b3"), storage);
            SetPlatformTokenHash("NEO", "neo", Hash.FromUnpaddedHex("c56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b"), storage);
            SetPlatformTokenHash("GAS", "neo", Hash.FromUnpaddedHex("602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7"), storage);
            SetPlatformTokenHash("ETH", "ethereum", Hash.FromString("ETH"), storage);
            //SetPlatformTokenHash("DAI", "ethereum", Hash.FromUnpaddedHex("6b175474e89094c44da98b954eedeac495271d0f"), storage);
        }
    }

    public void FinishInitialize(IRuntime vm, Address owner)
    {
        if (_migratingNexus)
        {
            return;
        }

        var storage = RootStorage;

        var symbols = GetTokens(storage);
        foreach (var symbol in symbols)
        {
            var token = GetTokenInfo(storage, symbol);

            var constructor = token.ABI.FindMethod(SmartContract.ConstructorName);

            if (constructor != null)
            {
                vm.CallContext(symbol, constructor, owner);
            }
        }
    }

    private void InitGenesisValues()
    {
        var version = DomainSettings.LatestKnownProtocol;
        
        _genesisValues = new Dictionary<string, KeyValuePair<BigInteger, ChainConstraint[]>>() {
                 {
                     NexusProtocolVersionTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                         version, new ChainConstraint[]
                     {
                         new ChainConstraint() { Kind = ConstraintKind.MustIncrease}
                     })
                 },

                 {
                     ValidatorContract.ValidatorSlotsTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                         DomainSettings.InitialValidatorCount, new ChainConstraint[]
                     {
                         new ChainConstraint() { Kind = ConstraintKind.MustIncrease}
                     })
                 },

                 {
                     ValidatorContract.ValidatorRotationTimeTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                         ValidatorContract.ValidatorRotationTimeDefault, new ChainConstraint[]
                     {
                         new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 30},
                         new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = 3600},
                     })
                 },

                 {
                     ConsensusContract.PollVoteLimitTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                         ConsensusContract.PollVoteLimitDefault, new ChainConstraint[]
                     {
                         new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 100},
                         new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = 500000},
                     })
                 },

                 {
                     ConsensusContract.MaxEntriesPerPollTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                         ConsensusContract.MaxEntriesPerPollDefault, new ChainConstraint[]
                     {
                         new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 2},
                         new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = 1000},
                     })
                 },

                 {
                     ConsensusContract.MaximumPollLengthTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                         ConsensusContract.MaximumPollLengthDefault, new ChainConstraint[]
                     {
                         new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 86400 * 2},
                         new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = 86400 * 120},
                     })
                 },

                 {
                     StakeContract.MasterStakeThresholdTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                         StakeContract.DefaultMasterThreshold, new ChainConstraint[]
                     {
                         new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = UnitConversion.ToBigInteger(1000, DomainSettings.StakingTokenDecimals)},
                         new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = UnitConversion.ToBigInteger(200000, DomainSettings.StakingTokenDecimals)},
                     })
                 },

                 {
                     StakeContract.StakeSingleBonusPercentTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                         StakeContract.StakeSingleBonusPercentDefault, new ChainConstraint[]
                     {
                         new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 0},
                         new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = 100 },
                     })
                 },

                 {
                     StakeContract.StakeMaxBonusPercentTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                         StakeContract.StakeMaxBonusPercentDefault, new ChainConstraint[]
                     {
                         new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 50},
                         new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = 500 },
                     })
                 },

                 {
                     StakeContract.VotingStakeThresholdTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                         StakeContract.VotingStakeThresholdDefault, new ChainConstraint[]
                     {
                         new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals)},
                         new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = UnitConversion.ToBigInteger(10000, DomainSettings.StakingTokenDecimals)},
                     })
                 },

                 {
                     SwapContract.SwapMakerFeePercentTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                         SwapContract.SwapMakerFeePercentDefault, new ChainConstraint[]
                     {
                         new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 0},
                         new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = 20},
                         new ChainConstraint() { Kind = ConstraintKind.LessThanOther, Tag = SwapContract.SwapTakerFeePercentTag},
                     })
                 },


                 {
                     SwapContract.SwapTakerFeePercentTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                         SwapContract.SwapTakerFeePercentDefault, new ChainConstraint[]
                     {
                         new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 1},
                         new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = 20},
                         new ChainConstraint() { Kind = ConstraintKind.GreatThanOther, Tag = SwapContract.SwapMakerFeePercentTag},
                     })
                 },

                 {
                     StorageContract.KilobytesPerStakeTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                         StorageContract.KilobytesPerStakeDefault, new ChainConstraint[]
                     {
                         new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 1},
                         new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = 10000},
                     })
                 },

                 {
                     StorageContract.FreeStoragePerContractTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                         StorageContract.FreeStoragePerContractDefault, new ChainConstraint[]
                     {
                         new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 0},
                         new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = 1024 * 512},
                     })
                 },

                 {
                     DomainSettings.FuelPerContractDeployTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                         FuelPerContractDeployDefault, new ChainConstraint[]
                     {
                         new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 0},
                         new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = UnitConversion.ToBigInteger(1000, DomainSettings.FiatTokenDecimals)},
                     })
                 },

                 {
                     DomainSettings.FuelPerTokenDeployTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                         FuelPerTokenDeployDefault, new ChainConstraint[]
                     {
                         new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 0},
                         new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = UnitConversion.ToBigInteger(1000, DomainSettings.FiatTokenDecimals)},
                     })
                 },

                 {
                     DomainSettings.FuelPerOrganizationDeployTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                         FuelPerOrganizationDeployDefault, new ChainConstraint[]
                     {
                         new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 0},
                         new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = UnitConversion.ToBigInteger(1000, DomainSettings.FiatTokenDecimals)},
                     })
                 },

                 {
                   GovernanceContract.GasMinimumFeeTag, new KeyValuePair<BigInteger, ChainConstraint[]>(DomainSettings.DefaultMinimumGasFee, new[]
                   {
                       new ChainConstraint {Kind = ConstraintKind.MinValue, Value = DomainSettings.DefaultMinimumGasFee},
                       new ChainConstraint {Kind = ConstraintKind.MustIncrease}
                   })
                 },
        };
    }

    public Transaction CreateGenesisTransaction(Timestamp timestamp, PhantasmaKeys owner)
    {
        var version = (int)GetProtocolVersion(RootStorage);
        return CreateGenesisTransaction(timestamp, owner, version);
    }

    public Transaction CreateGenesisTransaction(Timestamp timestamp, PhantasmaKeys owner, int protocolVersion)
    {
        Throw.If(HasGenesis(), "genesis block already exists");

        Throw.If(!_initialValidators.Any(), "initial validators have not been set");

        return NexusCreateTx(owner, timestamp, protocolVersion);
    }

    #endregion

    #region VALIDATORS
    public Timestamp GetValidatorLastActivity(Address target)
    {
        throw new NotImplementedException();
    }

    public ValidatorEntry[] GetValidators(Timestamp timestamp)
    {
        var validators = (ValidatorEntry[])RootChain.InvokeContractAtTimestamp(this.RootStorage, timestamp, NativeContractKind.Validator, nameof(ValidatorContract.GetValidators)).ToObject();
        return validators;
    }

    public int GetPrimaryValidatorCount(Timestamp timestamp)
    {
        var count = RootChain.InvokeContractAtTimestamp(this.RootStorage, timestamp, NativeContractKind.Validator, nameof(ValidatorContract.GetValidatorCount), ValidatorType.Primary).AsNumber();
        if (count < 1)
        {
            return 1;
        }
        return (int)count;
    }

    public int GetSecondaryValidatorCount(Timestamp timestamp)
    {
        var count = RootChain.InvokeContractAtTimestamp(this.RootStorage, timestamp, NativeContractKind.Validator, nameof(ValidatorContract.GetValidatorCount), ValidatorType.Primary).AsNumber();
        return (int)count;
    }

    public ValidatorType GetValidatorType(Address address, Timestamp timestamp)
    {
        var result = RootChain.InvokeContractAtTimestamp(this.RootStorage, timestamp, NativeContractKind.Validator, nameof(ValidatorContract.GetValidatorType), address).AsEnum<ValidatorType>();
        return result;
    }

    public bool IsPrimaryValidator(Address address, Timestamp timestamp)
    {
        if (address.IsNull)
        {
            return false;
        }

        if (!HasGenesis())
        {
            return _initialValidators.Contains(address);
        }

        var result = GetValidatorType(address, timestamp);
        return result == ValidatorType.Primary;
    }

    public bool IsSecondaryValidator(Address address, Timestamp timestamp)
    {
        var result = GetValidatorType(address, timestamp);
        return result == ValidatorType.Secondary;
    }

    // this returns true for both active and waiting
    public bool IsKnownValidator(Address address, Timestamp timestamp)
    {
        var result = GetValidatorType(address, timestamp);
        return result != ValidatorType.Invalid;
    }

    public BigInteger GetStakeFromAddress(StorageContext storage, Address address, Timestamp timestamp)
    {
        var result = RootChain.InvokeContractAtTimestamp(storage, timestamp, NativeContractKind.Stake, nameof(StakeContract.GetStake), address).AsNumber();
        return result;
    }

    public BigInteger GetUnclaimedFuelFromAddress(StorageContext storage, Address address, Timestamp timestamp)
    {
        var result = RootChain.InvokeContractAtTimestamp(storage, timestamp, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), address).AsNumber();
        return result;
    }

    public Timestamp GetStakeTimestampOfAddress(StorageContext storage, Address address, Timestamp timestamp)
    {
        var result = RootChain.InvokeContractAtTimestamp(storage, timestamp, NativeContractKind.Stake, nameof(StakeContract.GetStakeTimestamp), address).AsTimestamp();
        return result;
    }

    public bool IsStakeMaster(StorageContext storage, Address address, Timestamp timestamp)
    {
        var stake = GetStakeFromAddress(storage, address, timestamp);
        if (stake <= 0)
        {
            return false;
        }

        var masterThresold = RootChain.InvokeContractAtTimestamp(storage, timestamp, NativeContractKind.Stake, nameof(StakeContract.GetMasterThreshold)).AsNumber();
        return stake >= masterThresold;
    }

    public int GetIndexOfValidator(Address address, Timestamp timestamp)
    {
        if (!address.IsUser)
        {
            return -1;
        }

        if (RootChain == null)
        {
            return -1;
        }

        var result = (int)RootChain.InvokeContractAtTimestamp(this.RootStorage, timestamp, NativeContractKind.Validator, nameof(ValidatorContract.GetIndexOfValidator), address).AsNumber();
        return result;
    }

    public ValidatorEntry GetValidatorByIndex(int index, Timestamp timestamp)
    {
        if (RootChain == null)
        {
            return new ValidatorEntry()
            {
                address = Address.Null,
                election = new Timestamp(0),
                type = ValidatorType.Invalid
            };
        }

        Throw.If(index < 0, "invalid validator index");

        var result = (ValidatorEntry)RootChain.InvokeContractAtTimestamp(this.RootStorage, timestamp, NativeContractKind.Validator, nameof(ValidatorContract.GetValidatorByIndex), (BigInteger)index).ToObject();
        return result;
    }
    #endregion

    #region STORAGE

    private StorageMap GetArchiveMap(StorageContext storage)
    {
        var map = new StorageMap(ChainArchivesKey, storage);
        return map;
    }

    public IArchive GetArchive(StorageContext storage, Hash hash)
    {
        var map = GetArchiveMap(storage);

        if (map.ContainsKey(hash))
        {
            var bytes = map.Get<Hash, byte[]>(hash);
            var archive = Archive.Unserialize(bytes);
            return archive;
        }

        return null;
    }

    public bool ArchiveExists(StorageContext storage, Hash hash)
    {
        var map = GetArchiveMap(storage);
        return map.ContainsKey(hash);
    }

    public bool IsArchiveComplete(IArchive archive)
    {
        for (int i = 0; i < archive.BlockCount; i++)
        {
            if (!HasArchiveBlock(archive, i))
            {
                return false;
            }
        }

        return true;
    }

    public IArchive CreateArchive(StorageContext storage, MerkleTree merkleTree, Address owner, string name, BigInteger size, Timestamp time, IArchiveEncryption encryption)
    {
        var archive = GetArchive(storage, merkleTree.Root);
        Throw.If(archive != null, "archive already exists");

        archive = new Archive(merkleTree, name, size, time, encryption,
            Enumerable.Range(0, (int)MerkleTree.GetChunkCountForSize(size)).ToList());
        var archiveHash = merkleTree.Root;

        AddOwnerToArchive(storage, archive, owner);

        // ModifyArchive(storage, archive); => not necessary, addOwner already calls this

        return archive;
    }

    private void ModifyArchive(StorageContext storage, IArchive archive)
    {
        var map = GetArchiveMap(storage);
        var bytes = archive.ToByteArray();
        map.Set<Hash, byte[]>(archive.Hash, bytes);
    }

    public bool DeleteArchive(StorageContext storage, IArchive archive)
    {
        Throw.IfNull(archive, nameof(archive));

        Throw.If(archive.OwnerCount > 0, "can't delete archive, still has owners");

        for (int i = 0; i < archive.BlockCount; i++)
        {
            var blockHash = archive.MerkleTree.GetHash(i);
            if (_archiveContents.ContainsKey(blockHash))
            {
                _archiveContents.Remove(blockHash);
            }
        }

        var map = GetArchiveMap(storage);
        map.Remove(archive.Hash);

        return true;
    }

    public bool HasArchiveBlock(IArchive archive, int blockIndex)
    {
        Throw.IfNull(archive, nameof(archive));
        Throw.If(blockIndex < 0 || blockIndex >= archive.BlockCount, "invalid block index");

        var hash = archive.MerkleTree.GetHash(blockIndex);
        return _archiveContents.ContainsKey(hash);
    }

    public void WriteArchiveBlock(IArchive archive, int blockIndex, byte[] content)
    {
        Throw.IfNull(archive, nameof(archive));
        Throw.IfNull(content, nameof(content));
        Throw.If(blockIndex < 0 || blockIndex >= archive.BlockCount, "invalid block index");

        var hash = MerkleTree.CalculateBlockHash(content);

        if (_archiveContents.ContainsKey(hash))
        {
            return;
        }

        if (!archive.MerkleTree.VerifyContent(hash, blockIndex))
        {
            throw new ArchiveException("Block content mismatch");
        }

        _archiveContents.Set(hash, content);

        archive.AddMissingBlock(blockIndex);
        ModifyArchive(RootStorage, archive);
    }

    public byte[] ReadArchiveBlock(IArchive archive, int blockIndex)
    {
        Throw.IfNull(archive, nameof(archive));
        Throw.If(blockIndex < 0 || blockIndex >= archive.BlockCount, "invalid block index");

        var hash = archive.MerkleTree.GetHash(blockIndex);

        if (_archiveContents.ContainsKey(hash))
        {
            return _archiveContents.Get(hash);
        }

        return null;
    }

    public void AddOwnerToArchive(StorageContext storage, IArchive archive, Address owner)
    {
        archive.AddOwner(owner);
        ModifyArchive(storage, archive);
    }

    public void RemoveOwnerFromArchive(StorageContext storage, IArchive archive, Address owner)
    {
        archive.RemoveOwner(owner);

        if (archive.OwnerCount <= 0)
        {
            DeleteArchive(storage, archive);
        }
        else
        {
            ModifyArchive(storage, archive);
        }
    }

    #endregion

    #region CHANNELS
    public BigInteger GetRelayBalance(Address address, Timestamp timestamp)
    {
        var chain = RootChain;
        try
        {
            var result = chain.InvokeContractAtTimestamp(this.RootStorage, timestamp, "relay", "GetBalance", address).AsNumber();
            return result;
        }
        catch
        {
            return 0;
        }
    }
    #endregion

    #region PLATFORMS
    public int CreatePlatform(StorageContext storage, string externalAddress, Address interopAddress, string name, string fuelSymbol)
    {
        // check if something with this name already exists
        if (PlatformExists(storage, name))
        {
            return -1;
        }

        var platformList = this.GetSystemList(PlatformTag, storage);
        var platformID = (byte)(1 + platformList.Count());

        //var chainAddress = Address.FromHash(name);
        var entry = new PlatformInfo(name, fuelSymbol, new PlatformSwapAddress[] {
            new PlatformSwapAddress() { LocalAddress = interopAddress, ExternalAddress = externalAddress }
        });

        // add to persistent list of tokens
        platformList.Add(name);

        EditPlatform(storage, name, entry);
        // notify oracles on new platform
        this.Notify(storage);
        return platformID;
    }

    private byte[] GetPlatformInfoKey(string name)
    {
        return GetNexusKey($"platform.{name}");
    }

    private void EditPlatform(StorageContext storage, string name, PlatformInfo platformInfo)
    {
        var key = GetPlatformInfoKey(name);
        var bytes = Serialization.Serialize(platformInfo);
        storage.Put(key, bytes);
    }

    public bool PlatformExists(StorageContext storage, string name)
    {
        if (name == DomainSettings.PlatformName)
        {
            return true;
        }

        var key = GetPlatformInfoKey(name);
        return storage.Has(key);
    }

    public PlatformInfo GetPlatformInfo(StorageContext storage, string name)
    {
        var key = GetPlatformInfoKey(name);
        if (storage.Has(key))
        {
            var bytes = storage.Get(key);
            return Serialization.Unserialize<PlatformInfo>(bytes);
        }

        throw new ChainException($"Platform does not exist ({name})");
    }
    #endregion

    #region Contracts
    private byte[] GetContractInfoKey(string name)
    {
        return GetNexusKey($"contract.{name}");
    }

    /*
    private void EditContract(StorageContext storage, string name, PlatformInfo platformInfo)
    {
        var key = GetPlatformInfoKey(name);
        var bytes = Serialization.Serialize(platformInfo);
        storage.Put(key, bytes);
    }*/

    public static bool IsNativeContract(string name)
    {
        NativeContractKind kind;
        return Enum.TryParse<NativeContractKind>(name, true, out kind);
    }

    public bool ContractExists(StorageContext storage, string name)
    {
        if (IsNativeContract(name))
        {
            return true;
        }

        var key = GetContractInfoKey(name);
        return storage.Has(key);
    }

    /*
    public PlatformInfo GetPlatformInfo(StorageContext storage, string name)
    {
        var key = GetPlatformInfoKey(name);
        if (storage.Has(key))
        {
            var bytes = storage.Get(key);
            return Serialization.Unserialize<PlatformInfo>(bytes);
        }

        throw new ChainException($"Platform does not exist ({name})");
    }*/
    #endregion

    #region ORGANIZATIONS
    public void CreateOrganization(StorageContext storage, string ID, string name, byte[] script)
    {
        var organizationList = GetSystemList(OrganizationTag, storage);

        var organization = new Organization(ID, storage);
        organization.Init(name, script);

        // add to persistent list of tokens
        organizationList.Add(ID);

        var organizationMap = GetSystemMap(OrganizationTag, storage);
        organizationMap.Set<Address, string>(organization.Address, ID);
    }

    public bool OrganizationExists(StorageContext storage, string name) // name in this case is actually the id....
    {
        var orgs = GetOrganizations(storage);
        return orgs.Contains(name);
    }

    public IOrganization GetOrganizationByName(StorageContext storage, string name) // name in this case is actually the id....
    {
        if (OrganizationExists(storage, name))
        {
            var org = new Organization(name, storage);
            return org;
        }

        return null;
    }

    public IOrganization GetOrganizationByAddress(StorageContext storage, Address address)
    {
        var organizationMap = GetSystemMap(OrganizationTag, storage);
        if (organizationMap.ContainsKey<Address>(address))
        {
            var name = organizationMap.Get<Address, string>(address);
            return GetOrganizationByName(storage, name);
        }

        return null;
    }
    #endregion

    public int GetIndexOfChain(string name)
    {
        var chains = this.GetChains(RootStorage);
        int index = 0;
        foreach (var chain in chains)
        {
            if (chain == name)
            {
                return index;
            }

            index++;
        }
        return -1;
    }

    public IKeyValueStoreAdapter GetChainStorage(string name)
    {
        return this.CreateKeyStoreAdapter($"chain.{name}");
    }

    public ValidatorEntry GetValidator(StorageContext storage, string tAddress)
    {
        var validatorContractName = NativeContractKind.Validator.GetContractName();
        // TODO use builtin methods instead of doing this directly
        var valueMapKey = Encoding.UTF8.GetBytes($".{validatorContractName}._validators");
        var validators = new StorageMap(valueMapKey, storage);

        foreach (var validator in validators.AllValues<ValidatorEntry>())
        {
            if (validator.address.TendermintAddress == tAddress)
            {
                return validator;
            }
        }

        return new ValidatorEntry()
        {
            address = Address.Null,
            type = ValidatorType.Invalid,
            election = new Timestamp(0)
        };
    }

    public BigInteger GetGovernanceValue(StorageContext storage, string name)
    {
        if (HasGenesis())
        {
            return OptimizedGetGovernanceValue(storage, name);
        }

        if (_genesisValues != null)
        {
            foreach (var entry in _genesisValues)
            {
                if (entry.Key == name)
                {
                    return entry.Value.Key;
                }
            }
        }

        return 0;
        //throw new ChainException("Cannot read governance values without a genesis block");
    }


    private static byte[] _optimizedGovernanceKey = null;

    private BigInteger OptimizedGetGovernanceValue(StorageContext storage, string name)
    {
        if (_optimizedGovernanceKey == null)
        {
            var governanceContractName = NativeContractKind.Governance.GetContractName();
            _optimizedGovernanceKey = Encoding.UTF8.GetBytes($".{governanceContractName}._valueMap");
        }

        var valueMap = new StorageMap(_optimizedGovernanceKey, storage);

        if (!valueMap.ContainsKey(name))
        {
            if (name == GovernanceContract.GasMinimumFeeTag)
            {
                return DomainSettings.DefaultMinimumGasFee;
            }

            throw new ChainException("invalid governance value name: " + name);            
        }

        var value = valueMap.Get<string, BigInteger>(name);
        return value;
    }

    public void RegisterPlatformAddress(StorageContext storage, string platform, Address localAddress, string externalAddress)
    {
        var platformInfo = GetPlatformInfo(storage, platform);

        foreach (var entry in platformInfo.InteropAddresses)
        {
            Throw.If(entry.LocalAddress == localAddress || entry.ExternalAddress== externalAddress, "address already part of platform interops");
        }

        var newEntry = new PlatformSwapAddress()
        {
            ExternalAddress = externalAddress,
            LocalAddress = localAddress,
        };

        platformInfo.AddAddress(newEntry);
        EditPlatform(storage, platform, platformInfo);
    }

    // TODO optimize this
    public bool IsPlatformAddress(StorageContext storage, Address address)
    {
        if (!address.IsInterop)
        {
            return false;
        }

        var platforms = this.GetPlatforms(storage);
        foreach (var platform in platforms)
        {
            var info = GetPlatformInfo(storage, platform);

            foreach (var entry in info.InteropAddresses)
            {
                if (entry.LocalAddress == address)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public StorageContext RootStorage { get; init;  }

    private StorageList GetSystemList(string name, StorageContext storage)
    {
        var key = System.Text.Encoding.UTF8.GetBytes($".{name}.list");
        return new StorageList(key, storage);
    }

    private StorageMap GetSystemMap(string name, StorageContext storage)
    {
        var key = System.Text.Encoding.UTF8.GetBytes($".{name}.map");
        return new StorageMap(key, storage);
    }

    private const string TokenTag = "tokens";
    private const string ChainTag = "chains";
    private const string PlatformTag = "platforms";
    private const string FeedTag = "feeds";
    private const string OrganizationTag = "orgs";

    public string[] GetTokens(StorageContext storage)
    {
        var list = GetSystemList(TokenTag, storage);
        return list.All<string>();
    }

    public string[] GetChains(StorageContext storage)
    {
        var list = GetSystemList(ChainTag, storage);
        return list.All<string>();
    }

    public string[] GetPlatforms(StorageContext storage)
    {
        var list = GetSystemList(PlatformTag, storage);
        return list.All<string>();
    }

    public string[] GetFeeds(StorageContext storage)
    {
        var list = GetSystemList(FeedTag, storage);
        return list.All<string>();
    }

    public string[] GetOrganizations(StorageContext storage)
    {
        var list = GetSystemList(OrganizationTag, storage);
        return list.All<string>();
    }

    public byte[] GetNexusKey(string key)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes($".nexus.{key}");
        return bytes;
    }

    public Hash GetGenesisHash(StorageContext storage)
    {
        var key = GetNexusKey("hash");
        if (storage.Has(key))
        {
            return storage.Get<Hash>(key);
        }

        return Hash.Null;
    }

    public Block GetGenesisBlock()
    {
        if (HasGenesis())
        {
            var genesisHash = GetGenesisHash(RootStorage);
            return RootChain.GetBlockByHash(genesisHash);
        }

        return null;
    }

    public bool TokenExistsOnPlatform(string symbol, string platform, StorageContext storage)
    {
        var key = GetNexusKey($"{symbol}.{platform}.hash");
        if (storage.Has(key))
        {
            return true;
        }

        return false;
    }

    public Hash GetTokenPlatformHash(string symbol, string platform, StorageContext storage)
    {
        if (platform == DomainSettings.PlatformName)
        {
            return Hash.FromString(symbol);
        }

        var key = GetNexusKey($"{symbol}.{platform}.hash");
        if (storage.Has(key))
        {
            return storage.Get<Hash>(key);
        }

        return Hash.Null;
    }

    public Hash[] GetPlatformTokenHashes(string platform, StorageContext storage)
    {
        var tokens = GetTokens(storage);

        var hashes = new List<Hash>();

        if (platform == DomainSettings.PlatformName)
        {
            foreach (var token in tokens)
            {
                hashes.Add(Hash.FromString(token));
            }
            return hashes.ToArray();
        }

        foreach (var token in tokens)
        {
            var key = GetNexusKey($"{token}.{platform}.hash");
            if (storage.Has(key))
            {
                var tokenHash = storage.Get<Hash>(key);
                if (tokenHash != Hash.Null)
                {
                    hashes.Add(tokenHash);
                }
            }
        }

        return hashes.Distinct().ToArray();
    }

    public string GetPlatformTokenByHash(Hash hash, string platform, StorageContext storage)
    {
        var tokens = GetTokens(storage);

        if (platform == DomainSettings.PlatformName)
        {
            foreach (var token in tokens)
            {
                if (Hash.FromString(token) == hash)
                    return token;
            }
        }

        foreach (var token in tokens)
        {
            var key = GetNexusKey($"{token}.{platform}.hash");
            if (HasTokenPlatformHash(token, platform, storage))
            {
                var tokenHash = storage.Get<Hash>(key);
                if (tokenHash == hash)
                {
                    return token;
                }
            }
        }

        Log.Warning($"Token hash {hash} doesn't exist!");
        return null;
    }

    public void SetPlatformTokenHash(string symbol, string platform, Hash hash, StorageContext storage)
    {
        var tokenKey = GetTokenInfoKey(symbol);
        if (!storage.Has(tokenKey))
        {
            throw new ChainException($"Token does not exist ({symbol})");
        }

        if (platform == DomainSettings.PlatformName)
        {
            throw new ChainException($"cannot set token hash of {symbol} for native platform");
        }

        var bytes = storage.Get(tokenKey);
        var info = Serialization.Unserialize<TokenInfo>(bytes);

        if (!info.Flags.HasFlag(TokenFlags.Swappable))
        {
            info.Flags |= TokenFlags.Swappable;
            EditToken(storage, symbol, info);
        }

        var hashKey = GetNexusKey($"{symbol}.{platform}.hash");

        //should be updateable since a foreign token hash could change
        if (storage.Has(hashKey))
        {
            Log.Warning($"Token hash of {symbol} already set for platform {platform}, updating to {hash}");
        }

        storage.Put<Hash>(hashKey, hash);
    }

    public bool HasTokenPlatformHash(string symbol, string platform, StorageContext storage)
    {
        if (platform == DomainSettings.PlatformName)
        {
            return true;
        }

        var key = GetNexusKey($"{symbol}.{platform}.hash");
        return storage.Has(key);
    }

    public IToken GetTokenInfo(StorageContext storage, Address contractAddress)
    {
        var symbols = GetTokens(storage);
        foreach (var symbol in symbols)
        {
            var tokenAddress = TokenUtils.GetContractAddress(symbol);

            if (tokenAddress == contractAddress)
            {
                var token = GetTokenInfo(storage, symbol);
                return token;
            }
        }

        return null;
    }

    public void MigrateTokenOwner(StorageContext storage, Address oldOwner, Address newOwner)
    {
        var symbols = GetTokens(storage);
        foreach (var symbol in symbols)
        {
            var token = (TokenInfo) GetTokenInfo(storage, symbol);
            if (token.Owner == oldOwner)
            {
                token.Owner = newOwner;
                EditToken(storage, symbol, token);
            }
        }
    }

    public void UpgradeTokenContract(StorageContext storage, string symbol, byte[] script, ContractInterface abi)
    {
        var key = GetTokenInfoKey(symbol);
        if (!storage.Has(key))
        {
            throw new ChainException($"Cannot upgrade non-existing token contract: {symbol}");
        }

        if (IsDangerousSymbol(symbol))
        {
            throw new ChainException($"Forbidden to upgrade token contract: {symbol}");
        }

        var bytes = storage.Get(key);
        var info = Serialization.Unserialize<TokenInfo>(bytes);

        info = new TokenInfo(info.Symbol, info.Name, info.Owner, info.MaxSupply, info.Decimals, info.Flags, script, abi);
        bytes = Serialization.Serialize(info);
        storage.Put(key, bytes);
    }

    public SmartContract GetTokenContract(StorageContext storage, string symbol)
    {
        if (TokenExists(storage, symbol))
        {
            var token = GetTokenInfo(storage, symbol);

            return new CustomContract(symbol, token.Script, token.ABI);
        }

        return null;
    }

    public SmartContract GetTokenContract(StorageContext storage, Address contractAddress)
    {
        var token = GetTokenInfo(storage, contractAddress);
        if (token != null)
        {
            return new CustomContract(token.Symbol, token.Script, token.ABI);
        }

        return null;
    }

    public uint GetProtocolVersion(StorageContext storage)
    {
        if (!HasGenesis())
        {
            return DomainSettings.Phantasma30Protocol;
        }

        return (uint)this.GetGovernanceValue(storage, NexusProtocolVersionTag);
    }

}
