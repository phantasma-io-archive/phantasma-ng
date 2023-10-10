//#define ALLOWANCE_OPERATIONS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Phantasma.Business.Blockchain.Archives;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.Blockchain.Tokens.Structs;
using Phantasma.Business.Blockchain.VM;
using Phantasma.Business.VM.Utils;

using Phantasma.Core;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Enums;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Contract.Governance;
using Phantasma.Core.Domain.Contract.Governance.Enums;
using Phantasma.Core.Domain.Contract.Governance.Structs;
using Phantasma.Core.Domain.Contract.Interop;
using Phantasma.Core.Domain.Contract.Interop.Structs;
using Phantasma.Core.Domain.Contract.Validator;
using Phantasma.Core.Domain.Contract.Validator.Enums;
using Phantasma.Core.Domain.Contract.Validator.Structs;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Domain.Exceptions;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Oracle;
using Phantasma.Core.Domain.Oracle.Enums;
using Phantasma.Core.Domain.Oracle.Structs;
using Phantasma.Core.Domain.Platform;
using Phantasma.Core.Domain.Platform.Structs;
using Phantasma.Core.Domain.Serializer;
using Phantasma.Core.Domain.Token;
using Phantasma.Core.Domain.Token.Enums;
using Phantasma.Core.Domain.Token.Structs;
using Phantasma.Core.Domain.TransactionData;
using Phantasma.Core.Domain.Triggers;
using Phantasma.Core.Domain.Triggers.Enums;
using Phantasma.Core.Domain.Validation;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Domain.VM.Enums;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Storage.Context.Structs;
using Phantasma.Core.Storage.Interfaces;
using Phantasma.Core.Utils;

using Serilog;
using Timestamp = Phantasma.Core.Types.Structs.Timestamp;

namespace Phantasma.Business.Blockchain;

public partial class Nexus : INexus
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

    private static string NEW_LP_CONTRACT_PVM = "000D01041C5068616E7461736D61204C69717569646974792050726F76696465720301082700000B000D0104024C500301083500000B000D010601000301084200000B000D010601010301084F00000B000D010301080301085C00000B000D010601000301086900000B000D010301000301087600000B000D010408446174612E4765740D0204024C500D0003010803000D0004056F776E6572030003020701040303030D00040941646472657373282907000403020301030108BE00000B000D00040F52756E74696D652E56657273696F6E070004000D010301001A0001000A001D010D00043243757272656E74206E657875732070726F746F636F6C2076657273696F6E2073686F756C642062652030206F72206D6F72650C0000040303030D000409416464726573732829070004030203040204010D040601000204020D05026C04076765744E616D6504000000000107746F6B656E4944030E6765744465736372697074696F6E045D0000000107746F6B656E4944030B676574496D61676555524C04F10000000107746F6B656E4944030A676574496E666F55524C041E0100000107746F6B656E4944030003050D0502FD52010004010D0004066D696E744944030003010D0004024C5003000D00041152756E74696D652E52656164546F6B656E070004023202020D0004066D696E744944300203000D0104044C5020230203020E020204230102040304085C00000B0004010D000403524F4D030003010D0004024C5003000D00041152756E74696D652E52656164546F6B656E070004023202020D000403524F4D300203003203030D0104124C6971756469747920666F7220706F6F6C200203020D04040753796D626F6C30300202040D040403202F200203050D06040753796D626F6C3130050506230405062302060423010402030208F000000B0004010D01041F68747470733A2F2F7068616E7461736D612E696F2F696D672F6C702E706E670301081D01000B0004010201020D01041868747470733A2F2F7068616E7461736D612E696F2F6C702F0202030E030304230103040304085101000B03050D0507040000000003050D0503010003050D0503010003050D0504024C50030502010503050D0404174E657875732E437265617465546F6B656E5365726965730704000D030408446174612E53657403010D0004056F776E65720300070303020D0004085F6368616E676564030007030B000D010408446174612E4765740D0204024C500D0003010803000D0004056F776E6572030003020701040303030D00040941646472657373282907000403040103010D000409416464726573732829070004010402320202040432040402030603060D05041152756E74696D652E49735769746E65737307050405090511040D06040E7769746E657373206661696C65640C06000D0703010103070204070E07070203070202070E07070203070D0704024C50030702010703070D0702220200FACEE6DD84C950B54361FB71DFE736CDC64D894294305B4FEEAF3C61ABF2F2F703070D0004094164647265737328290700040703070D06041152756E74696D652E4D696E74546F6B656E070604060206050205060306089704000B00040103010D0004094164647265737328290700040104020E02020302010403040D03041152756E74696D652E49735769746E657373070304030903EB040D04040E7769746E657373206661696C65640C040002020403040D040404534F554C03040D0402220200FACEE6DD84C950B54361FB71DFE736CDC64D894294305B4FEEAF3C61ABF2F2F703040D00040941646472657373282907000404030402010403040D03041652756E74696D652E5472616E73666572546F6B656E73070302020403040D0402220200FACEE6DD84C950B54361FB71DFE736CDC64D894294305B4FEEAF3C61ABF2F2F703040D0004094164647265737328290700040403040D0304055374616B6503030D0304057374616B652D03032E03000B000D010408446174612E4765740D0204024C500D0003010803000D0004056F776E6572030003020701040303030D000409416464726573732829070004030D0003010603000D0004085F6368616E6765640300030207010404040103010D00040941646472657373282907000401020402150202090249060D0504194F776E65722077617320616C7265616479206368616E6765640C050002030503050D02041152756E74696D652E49735769746E65737307020402090280060D05040E7769746E657373206661696C65640C05000D000420416464726573732E697353797374656D206E6F7420696D706C656D656E7465640C000902D8060D050427746865206E65772061646472657373206973206E6F7420612073797374656D20616464726573730C05000201020202030D02060101020204000D010408446174612E53657403030D0004056F776E65720300070103040D0004085F6368616E676564030007010B000D010408446174612E4765740D0204024C500D0003010803000D0004056F776E6572030003020701040303030D00040941646472657373282907000403040103010D0004094164647265737328290700040102030403040D02041152756E74696D652E49735769746E6573730702040209029F070D04040E7769746E657373206661696C65640C04000D04040B4D696772617465546F563303040D02040865786368616E67652D02022E02000B000D010408446174612E4765740D0204024C500D0003010803000D0004056F776E6572030003020701040303030D00040941646472657373282907000403040103010D0004094164647265737328290700040102030403040D02041152756E74696D652E49735769746E6573730702040209024D080D04040E7769746E657373206661696C65640C04000D0404074D69677261746503040D02040865786368616E67652D02022E02000B000D010408446174612E4765740D0204024C500D0003010803000D0004056F776E6572030003020701040303030D00040941646472657373282907000403040103010D0004094164647265737328290700040102030403040D02041152756E74696D652E49735769746E657373070204020902F7080D04040E7769746E657373206661696C65640C040008FB08000B00040103010D00040941646472657373282907000401040203020D0004094164647265737328290700040202010403040D03041152756E74696D652E49735769746E6573730703040309035E090D04040E7769746E657373206661696C65640C0400000B000D010408446174612E4765740D0204024C500D0003010803000D0004056F776E6572030003020701040303030D00040941646472657373282907000403040103010D00040941646472657373282907000401040203020D00040941646472657373282907000402040404050E0505030D0704024C50020706020407020608190708090909FC090D07040E696E76616C69642073796D626F6C0C070002030803080D07041152756E74696D652E49735769746E657373070704070907330A0D08040E7769746E657373206661696C65640C080008370A000B00040103010D0004094164647265737328290700040104020E02020302010403040D03041152756E74696D652E49735769746E6573730703040309038B0A0D04040E7769746E657373206661696C65640C0400088F0A000B00040103010D00040941646472657373282907000401040203020D00040941646472657373282907000402040304040E04040302010603060D05041152756E74696D652E49735769746E657373070504050905FA0A0D06040E7769746E657373206661696C65640C0600020406030602010603060D0604074275726E4E465403060D05040865786368616E67652D05052E0508260B000B00040103010D000409416464726573732829070004010D0204144E6F7420616C6C6F77656420746F206B696C6C2E0C02000B";
    private static string NEW_LP_CONTRACT_ABI = "14076765744E616D650400000000000967657453796D626F6C0428000000000E69735472616E7366657261626C650636000000000A69734275726E61626C650643000000000B676574446563696D616C7303500000000008697346696E697465065D000000000C6765744D6178537570706C79036A00000000086765744F776E65720877000000000A496E697469616C697A6500BF000000010D636F6E74726163744F776E657208044D696E74037D030000030466726F6D0803726F6D010372616D011153656E6446756E6473416E645374616B650098040000020466726F6D0806616D6F756E74030B4368616E67654F776E657200B1050000010466726F6D080C75706772616465546F4465780015070000010466726F6D081275706772616465546F4465784E6F506F6F6C00C3070000010466726F6D08096F6E55706772616465006D080000010466726F6D08096F6E4D69677261746500FC080000020466726F6D0802746F08066F6E4D696E740060090000040466726F6D0802746F080673796D626F6C0407746F6B656E494403046275726E00380A0000020466726F6D0807746F6B656E494403066F6E4275726E00900A0000040466726F6D0802746F080673796D626F6C0407746F6B656E494403066F6E4B696C6C00270B0000010466726F6D0800";
    
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
    
    public StorageContext RootStorage { get; init;  }

    private const string TokenTag = "tokens";
    private const string ChainTag = "chains";
    private const string PlatformTag = "platforms";
    private const string FeedTag = "feeds";
    private const string OrganizationTag = "orgs";

    private KeyValueStore<Hash, byte[]> _archiveContents;

    private Func<string, IKeyValueStoreAdapter> _adapterFactory = null;
    private IOracleReader _oracleReader = null;
    private List<IOracleObserver> _observers = new List<IOracleObserver>();

    private Func<Nexus, string, IChain> _instantiateChain;

    public static Nexus Initialize<T>(string name, Func<string, IKeyValueStoreAdapter> adapterFactory = null) where T : IChain
    {
        Func<Nexus, string, IChain> chainActivator = (nexus, chainName) =>
        {
            var chain = (T)Activator.CreateInstance(typeof(T), nexus, chainName);
            return chain;
        };

        var nexus = new Nexus(name, chainActivator, adapterFactory);

        return nexus;
    }

    /// <summary>
    /// The constructor bootstraps the main chain and all core side chains.
    /// </summary>
    private Nexus(string name, Func<Nexus, string, IChain> chainActivator, Func<string, IKeyValueStoreAdapter> adapterFactory = null)
    {
        Throw.IfNull(name, nameof(name));
        Throw.IfNull(chainActivator, nameof(chainActivator));

        _adapterFactory = adapterFactory;
        _instantiateChain = chainActivator;

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

            var tokens = GetAvailableTokenSymbols(storage);
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

        // Generate extra KCAL in simnet only
        if (Name == DomainSettings.NexusSimnet)
        {
            //sb.MintTokens(DomainSettings.StakingTokenSymbol, owner.Address, owner.Address, UnitConversion.ToBigInteger(10000000, DomainSettings.StakingTokenDecimals));
            sb.MintTokens(DomainSettings.FuelTokenSymbol, owner.Address, owner.Address, UnitConversion.ToBigInteger(10000000, DomainSettings.FuelTokenDecimals));
        }

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
            //var lpABI = ContractInterface.FromBytes(Base16.Decode(NEW_LP_CONTRACT_ABI));
            //CreateToken(storage, "LP", "Phantasma Liquidity Provider", owner, 0, 0, TokenFlags.Transferable | TokenFlags.Mintable | TokenFlags.Swappable | TokenFlags.Burnable, Base16.Decode(NEW_LP_CONTRACT_PVM), lpABI );

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

        var symbols = GetAvailableTokenSymbols(storage);
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
                     ValidatorContract.ValidatorMaxOfflineTimeTag,  new KeyValuePair<BigInteger, ChainConstraint[]>(
                     ValidatorContract.ValidatorMaxOfflineTimeDefault, new ChainConstraint[]
                     {
                         new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 3600}, // 1 Hour
                         new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = 604800}, // 1 Week
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
#endregion
    
#region Block
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
    
    public string[] GetFeeds(StorageContext storage)
    {
        var list = GetSystemList(FeedTag, storage);
        return list.All<string>();
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
    
    public string[] GetAvailableTokenSymbols(StorageContext storage)
    {
        var list = GetSystemList(TokenTag, storage);
        return list.All<string>();
    }

    public byte[] GetNexusKey(string key)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes($".nexus.{key}");
        return bytes;
    }

    public string[] GetAddressesBySymbol(string symbol)
    {
        List<string> addresses = new List<string>();
        var prefix = BalanceSheet.MakePrefix(symbol);

        RootStorage.Visit((key, value) =>
        {
            var keyString = Encoding.UTF8.GetString(key);
            if (keyString.Contains($".balances.{symbol}"))
            {
                byte[] addrChunk = key[prefix.Length..];
                addresses.Add(Address.FromBytes(addrChunk).Text);
            }
        });
        
        return addresses.ToArray();
    }

#region Protocol Version
    /// <summary>
    /// Get Protocol Version from Storage
    /// </summary>
    /// <param name="storage"></param>
    /// <returns></returns>
    public uint GetProtocolVersion(StorageContext storage)
    {
        if (!HasGenesis())
        {
            return DomainSettings.Phantasma30Protocol; // DomainSettings.LatestKnownProtocol;
        }

        return (uint)this.GetGovernanceValue(storage, NexusProtocolVersionTag);
    }
    
    /// <summary>
    /// Get Protocol Version from Storage
    /// </summary>
    /// <returns></returns>
    public uint GetProtocolVersion()
    {
        return GetProtocolVersion(RootStorage);
    }

    /// <summary>
    /// Get Protocol Version from block height
    /// </summary>
    /// <param name="blockHeight"></param>
    /// <returns></returns>
    public uint GetProtocolVersion(uint blockHeight)
    {
        if (blockHeight < 1)
        {
            return DomainSettings.Phantasma30Protocol;
        }
        
        var blockHash = RootChain.GetBlockHashAtHeight(blockHeight);
        var block = RootChain.GetBlockByHash(blockHash);
        return block.Protocol;
    }

    /// <summary>
    /// Get Protocol Version from block hash
    /// </summary>
    /// <param name="blockHash"></param>
    /// <returns></returns>
    public uint GetProtocolVersion(Hash blockHash)
    {
        if (blockHash == Hash.Null)
        {
            return DomainSettings.Phantasma30Protocol;
        }
        
        var block = RootChain.GetBlockByHash(blockHash);
        return block.Protocol;
    }
#endregion

}
