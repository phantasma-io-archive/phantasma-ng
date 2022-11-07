using System.Collections.Generic;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Storage;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;

namespace Phantasma.Core.Domain;

public interface INexus
{
    string Name { get; init; }
    IChain RootChain { get; }
    StorageContext RootStorage { get; init;  }
    BigInteger MaxGas { get; set; }

    bool HasGenesis();
    void CommitGenesis(Hash hash);
    void SetOracleReader(IOracleReader oracleReader);
    void Attach(IOracleObserver observer);
    void Detach(IOracleObserver observer);
    void Notify(StorageContext storage);
    bool LoadNexus(StorageContext storage);
    IKeyValueStoreAdapter CreateKeyStoreAdapter(string name);
    Block FindBlockByTransaction(Transaction tx);
    Block FindBlockByTransactionHash(Hash hash);
    Address LookUpName(StorageContext storage, string name);
    byte[] LookUpAddressScript(StorageContext storage, Address address);
    bool HasAddressScript(StorageContext storage, Address address);
    SmartContract GetContractByName(StorageContext storage, string contractName);
    Transaction FindTransactionByHash(Hash hash);
    bool CreateChain(StorageContext storage, string organization, string name, string parentChainName);
    string LookUpChainNameByAddress(Address address);
    bool ChainExists(StorageContext storage, string chainName);
    string GetParentChainByAddress(Address address);
    string GetParentChainByName(string chainName);
    string GetChainOrganization(string chainName);
    IEnumerable<string> GetChildChainsByAddress(StorageContext storage, Address chainAddress);
    IOracleReader GetOracleReader();
    IEnumerable<string> GetChildChainsByName(StorageContext storage, string chainName);
    IChain GetChainByAddress(Address address);
    IChain GetChainByName(string name);
    bool CreateFeed(StorageContext storage, Address owner, string name, FeedMode mode);
    bool FeedExists(StorageContext storage, string name);
    OracleFeed GetFeedInfo(StorageContext storage, string name);
    IToken CreateToken(StorageContext storage, string symbol, string name, Address owner, BigInteger maxSupply, int decimals, TokenFlags flags, byte[] script, ContractInterface abi = null);
    bool TokenExists(StorageContext storage, string symbol);
    IToken GetTokenInfo(StorageContext storage, string symbol);
    IToken GetTokenInfo(StorageContext storage, Address contractAddress);
    void MintToken(IRuntime Runtime, IToken token, Address source, Address destination, string sourceChain, BigInteger tokenID);
    void MintTokens(IRuntime Runtime, IToken token, Address source, Address destination, string sourceChain, BigInteger amount);
    BigInteger GetBurnedTokenSupply(StorageContext storage, string symbol);
    BigInteger GetBurnedTokenSupplyForSeries(StorageContext storage, string symbol, BigInteger seriesID);
    void BurnTokens(IRuntime Runtime, IToken token, Address source, Address destination, string targetChain, BigInteger amount);
    void BurnToken(IRuntime Runtime, IToken token, Address source, Address destination, string targetChain, BigInteger tokenID);
    void InfuseToken(IRuntime Runtime, IToken token, Address from, BigInteger tokenID, IToken infuseToken, BigInteger value);
    void TransferTokens(IRuntime Runtime, IToken token, Address source, Address destination, BigInteger amount, bool isInfusion = false);
    void TransferToken(IRuntime Runtime, IToken token, Address source, Address destination, BigInteger tokenID, bool isInfusion = false);
    byte[] GetKeyForNFT(string symbol, BigInteger tokenID);
    byte[] GetKeyForNFT(string symbol, string key);
    byte[] GetTokenSeriesKey(string symbol, BigInteger seriesID);
    BigInteger[] GetAllSeriesForToken(StorageContext storage, string symbol);
    TokenSeries CreateSeries(StorageContext storage, IToken token, BigInteger seriesID, BigInteger maxSupply, TokenSeriesMode mode, byte[] script, ContractInterface abi);
    TokenSeries GetTokenSeries(StorageContext storage, string symbol, BigInteger seriesID);
    BigInteger GenerateNFT(IRuntime Runtime, string symbol, string chainName, Address targetAddress, byte[] rom, byte[] ram, BigInteger seriesID);
    void DestroyNFT(IRuntime Runtime, string symbol, BigInteger tokenID, Address target);

    void WriteNFT(IRuntime Runtime, string symbol, BigInteger tokenID, string chainName, Address creator,
        Address owner, byte[] rom, byte[] ram, BigInteger seriesID, Timestamp timestamp,
        IEnumerable<TokenInfusion> infusion, bool mustExist);

    TokenContent ReadNFT(IRuntime Runtime, string symbol, BigInteger tokenID);
    TokenContent ReadNFT(StorageContext storage, string symbol, BigInteger tokenID);
    bool HasNFT(StorageContext storage, string symbol, BigInteger tokenID);
    void BeginInitialize(IRuntime vm, Address owner);
    void FinishInitialize(IRuntime vm, Address owner);
    void SetInitialValidators(IEnumerable<Address> initialValidators);
    Transaction CreateGenesisBlock(Timestamp timestamp, PhantasmaKeys owner);
    Timestamp GetValidatorLastActivity(Address target);
    ValidatorEntry[] GetValidators();
    int GetPrimaryValidatorCount();
    int GetSecondaryValidatorCount();
    ValidatorType GetValidatorType(Address address);
    bool IsPrimaryValidator(Address address);
    bool IsSecondaryValidator(Address address);
    bool IsKnownValidator(Address address);
    BigInteger GetStakeFromAddress(StorageContext storage, Address address);
    BigInteger GetUnclaimedFuelFromAddress(StorageContext storage, Address address);
    Timestamp GetStakeTimestampOfAddress(StorageContext storage, Address address);
    bool IsStakeMaster(StorageContext storage, Address address);
    int GetIndexOfValidator(Address address);
    ValidatorEntry GetValidatorByIndex(int index);
    IArchive GetArchive(StorageContext storage, Hash hash);
    bool ArchiveExists(StorageContext storage, Hash hash);
    bool IsArchiveComplete(IArchive archive);
    IArchive CreateArchive(StorageContext storage, MerkleTree merkleTree, Address owner, string name, BigInteger size, Timestamp time, IArchiveEncryption encryption);
    bool DeleteArchive(StorageContext storage, IArchive archive);
    bool HasArchiveBlock(IArchive archive, int blockIndex);
    void WriteArchiveBlock(IArchive archive, int blockIndex, byte[] content);
    byte[] ReadArchiveBlock(IArchive archive, int blockIndex);
    void AddOwnerToArchive(StorageContext storage, IArchive archive, Address owner);
    void RemoveOwnerFromArchive(StorageContext storage, IArchive archive, Address owner);
    BigInteger GetRelayBalance(Address address);
    int CreatePlatform(StorageContext storage, string externalAddress, Address interopAddress, string name, string fuelSymbol);
    bool PlatformExists(StorageContext storage, string name);
    PlatformInfo GetPlatformInfo(StorageContext storage, string name);
    void CreateContract(StorageContext storage, string name, byte[] script);
    bool ContractExists(StorageContext storage, string name);
    void CreateOrganization(StorageContext storage, string ID, string name, byte[] script);
    bool OrganizationExists(StorageContext storage, string name);
    IOrganization GetOrganizationByName(StorageContext storage, string name);
    IOrganization GetOrganizationByAddress(StorageContext storage, Address address);
    int GetIndexOfChain(string name);
    IKeyValueStoreAdapter GetChainStorage(string name);
    BigInteger GetGovernanceValue(StorageContext storage, string name);
    ValidatorEntry GetValidator(StorageContext storage, string tAddress);
    void RegisterPlatformAddress(StorageContext storage, string platform, Address localAddress, string externalAddress);
    bool IsPlatformAddress(StorageContext storage, Address address);
    string[] GetTokens(StorageContext storage);
    string[] GetContracts(StorageContext storage);
    string[] GetChains(StorageContext storage);
    string[] GetPlatforms(StorageContext storage);
    string[] GetFeeds(StorageContext storage);
    string[] GetOrganizations(StorageContext storage);
    Hash GetGenesisHash(StorageContext storage);
    Block GetGenesisBlock();
    bool TokenExistsOnPlatform(string symbol, string platform, StorageContext storage);
    Hash GetTokenPlatformHash(string symbol, string platform, StorageContext storage);
    Hash[] GetPlatformTokenHashes(string platform, StorageContext storage);
    string GetPlatformTokenByHash(Hash hash, string platform, StorageContext storage);
    void SetPlatformTokenHash(string symbol, string platform, Hash hash, StorageContext storage);
    bool HasTokenPlatformHash(string symbol, string platform, StorageContext storage);
    void MigrateTokenOwner(StorageContext storage, Address oldOwner, Address newOwner);
    void UpgradeTokenContract(StorageContext storage, string symbol, byte[] script, ContractInterface abi);
    SmartContract GetTokenContract(StorageContext storage, string symbol);
    SmartContract GetTokenContract(StorageContext storage, Address contractAddress);
    uint GetProtocolVersion(StorageContext storage);
    bool IsNativeContract(string name);
}
