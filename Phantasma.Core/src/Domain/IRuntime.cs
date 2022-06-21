using System.Numerics;
using System.Threading.Tasks;
using Phantasma.Core.Context;
using Phantasma.Shared.Types;

namespace Phantasma.Core
{
    public interface IRuntime
    {
        IChain Chain { get; }
        Transaction Transaction { get; }
        Timestamp Time { get; }
        StorageContext Storage { get; }
        StorageContext RootStorage { get; }
        bool IsTrigger { get; }
        int TransactionIndex { get; }

        IChainTask CurrentTask { get; }

        ExecutionContext CurrentContext { get; }
        ExecutionContext PreviousContext { get; }

        Address GasTarget { get; }
        BigInteger UsedGas { get; }
        BigInteger GasPrice { get; }

        Block GetBlockByHash(Hash hash);
        Block GetBlockByHeight(BigInteger height);

        Address GetValidator(Timestamp time);

        bool HasGenesis { get; }
        string NexusName { get; }
        uint ProtocolVersion { get; }
        Address GenesisAddress { get; }
        Hash GenesisHash { get; }
        Timestamp GetGenesisTime();

        Transaction GetTransaction(Hash hash);

        string[] GetTokens();
        string[] GetChains();
        string[] GetPlatforms();
        string[] GetFeeds();
        string[] GetOrganizations();
        
        // returns contracts deployed on current chain
        IContract[] GetContracts();

        IToken GetToken(string symbol);
        Hash GetTokenPlatformHash(string symbol, IPlatform platform);
        IFeed GetFeed(string name);
        IContract GetContract(string name);
        Address GetContractOwner(Address address);

        IPlatform GetPlatformByName(string name);
        IPlatform GetPlatformByIndex(int index);

        bool TokenExists(string symbol);
        bool NFTExists(string symbol, BigInteger tokenID);
        bool FeedExists(string name);
        bool PlatformExists(string name);

        bool OrganizationExists(string name);
        IOrganization GetOrganization(string name);

        bool AddMember(string organization, Address admin, Address target);
        bool RemoveMember(string organization, Address admin, Address target);
        void MigrateMember(string organization, Address admin, Address source, Address destination);

        bool ContractExists(string name);
        bool ContractDeployed(string name);

        bool ArchiveExists(Hash hash);
        IArchive GetArchive(Hash hash);
        bool DeleteArchive(Hash hash);

        bool AddOwnerToArchive(Hash hash, Address address);

        bool RemoveOwnerFromArchive(Hash hash, Address address);

        bool WriteArchive(IArchive archive, int blockIndex, byte[] data);

        bool ChainExists(string name);
        IChain GetChainByAddress(Address address);
        IChain GetChainByName(string name);
        int GetIndexOfChain(string name);

        IChain GetChainParent(string name);

        void Log(string description);
        void Throw(string description);
        void Expect(bool condition, string description);
        void Notify(EventKind kind, Address address, byte[] data);
        Task<VMObject> CallContext(string contextName, uint jumpOffset, string methodName, params object[] args);
        Task<VMObject> CallInterop(string methodName, params object[] args);

        Address LookUpName(string name);
        bool HasAddressScript(Address from);
        byte[] GetAddressScript(Address from);
        Task<string> GetAddressName(Address from);

        Event[] GetTransactionEvents(Hash transactionHash);
        Hash[] GetTransactionHashesForAddress(Address address);

        Task<ValidatorEntry> GetValidatorByIndex(int index);
        Task<ValidatorEntry[]> GetValidators();
        Task<bool> IsPrimaryValidator(Address address);
        Task<bool> IsSecondaryValidator(Address address);
        Task<int> GetPrimaryValidatorCount();
        Task<int> GetSecondaryValidatorCount();
        Task<bool> IsKnownValidator(Address address);

        Task<bool> IsStakeMaster(Address address); // TODO remove
        Task<BigInteger> GetStake(Address address);

        Task<BigInteger> GetTokenPrice(string symbol);
        BigInteger GetGovernanceValue(string name);

        BigInteger GenerateUID();
        BigInteger GenerateRandomNumber();

        Task<TriggerResult> InvokeTrigger(bool allowThrow, byte[] script, string contextName, ContractInterface abi, string triggerName, params object[] args);

        Task<bool> IsWitness(Address address);

        BigInteger GetBalance(string symbol, Address address);
        BigInteger[] GetOwnerships(string symbol, Address address);
        BigInteger GetTokenSupply(string symbol);

        Task CreateToken(Address owner, string symbol, string name, BigInteger maxSupply, int decimals, TokenFlags flags, byte[] script, ContractInterface abi);
        Task SetPlatformTokenHash(string symbol, string platform, Hash hash);
        Task CreateChain(Address creator, string organization, string name, string parentChain);
        Task CreateFeed(Address owner, string name, FeedMode mode);
        IArchive CreateArchive(MerkleTree merkleTree, Address owner, string name, BigInteger size, Timestamp time, IArchiveEncryption encryption);

        Task<BigInteger> CreatePlatform(Address from, string name, string externalAddress, Address interopAddress, string fuelSymbol);

        bool IsAddressOfParentChain(Address address);
        bool IsAddressOfChildChain(Address address);

        bool IsPlatformAddress(Address address);
        void RegisterPlatformAddress(string platform, Address localAddress, string externalAddress);

        Task MintTokens(string symbol, Address from, Address target, BigInteger amount);
        void BurnTokens(string symbol, Address from, BigInteger amount);
        void TransferTokens(string symbol, Address source, Address destination, BigInteger amount);
        Task SwapTokens(string sourceChain, Address from, string targetChain, Address to, string symbol, BigInteger value);

        Task<BigInteger> MintToken(string symbol, Address from, Address target, byte[] rom, byte[] ram, BigInteger seriesID);
        Task BurnToken(string symbol, Address from, BigInteger tokenID);
        Task InfuseToken(string symbol, Address from, BigInteger tokenID, string infuseSymbol, BigInteger value);
        Task TransferToken(string symbol, Address source, Address destination, BigInteger tokenID);
        Task WriteToken(Address from, string tokenSymbol, BigInteger tokenID, byte[] ram);
        TokenContent ReadToken(string tokenSymbol, BigInteger tokenID);
        Task<ITokenSeries> CreateTokenSeries(string tokenSymbol, Address from, BigInteger seriesID, BigInteger maxSupply, TokenSeriesMode mode, byte[] script, ContractInterface abi);
        ITokenSeries GetTokenSeries(string symbol, BigInteger seriesID);

        Task<byte[]> ReadOracle(string URL);

        Task<IChainTask> StartTask(Address from, string contractName, ContractMethod method, uint frequency, uint delay, TaskFrequencyMode mode, BigInteger gasLimit);
        Task StopTask(IChainTask task);
        IChainTask GetTask(BigInteger taskID);

        Task<TriggerResult> InvokeTriggerOnAccount(bool allowThrow, Address address, AccountTrigger trigger, params object[] args);
        Task<TriggerResult> InvokeTriggerOnToken(bool allowThrow, IToken token, TokenTrigger trigger, params object[] args);
        void AddAllowance(Address destination, string symbol, BigInteger amount);
        bool SubtractAllowance(Address destination, string symbol, BigInteger amount);
        void RemoveAllowance(Address destination, string symbol);
    }
}
