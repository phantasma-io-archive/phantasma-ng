using System.Collections.Generic;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Validator;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Execution;
using Phantasma.Core.Domain.Oracle;
using Phantasma.Core.Domain.Tasks;
using Phantasma.Core.Domain.Token;
using Phantasma.Core.Domain.TransactionData;
using Phantasma.Core.Domain.Triggers;
using Phantasma.Core.Domain.Validation;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;

namespace Phantasma.Core.Domain.Interfaces
{
    public interface IRuntime
    {
        public Stack<VMObject> Stack { get; }
        IChain Chain { get; }
        Transaction Transaction { get; }
        public Timestamp Time { get; }
        public StorageContext Storage { get; }
        public StorageContext RootStorage { get; }
        public bool IsTrigger { get; }
        public int TransactionIndex { get; }
        public IEnumerable<Event> Events { get; }
        public ExecutionContext EntryContext { get; }

        public IChainTask CurrentTask { get; }
        public void RegisterContext(string contextName, ExecutionContext context);
        public void SetCurrentContext(ExecutionContext context);
        public ExecutionState Execute();
        public string ExceptionMessage { get; }
        public bool IsError { get; }

        ExecutionContext CurrentContext { get; }
        ExecutionContext PreviousContext { get; }

        public Address GasTarget { get; }
        public BigInteger UsedGas { get; }
        public BigInteger MaxGas { get; }
        public BigInteger GasPrice { get; }

        public Block GetBlockByHash(Hash hash);
        public Block GetBlockByHeight(BigInteger height);
        public bool IsRootChain();

        public IChain GetRootChain();

        public bool HasGenesis { get; }
        public string NexusName { get; }
        public uint ProtocolVersion { get; }
        public Hash GenesisHash { get; }
        public Timestamp GetGenesisTime();

        public Transaction GetTransaction(Hash hash);

        public string[] GetTokens();
        public string[] GetChains();
        public string[] GetPlatforms();
        public string[] GetFeeds();
        public string[] GetOrganizations();
        
        // returns contracts deployed on current chain
        public IContract[] GetContracts();

        public IToken GetToken(string symbol);
        public Hash GetTokenPlatformHash(string symbol, IPlatform platform);
        public IFeed GetFeed(string name);
        public IContract GetContract(string name);
        public Address GetContractOwner(Address address);

        public IPlatform GetPlatformByName(string name);
        public IPlatform GetPlatformByIndex(int index);

        public bool TokenExists(string symbol);
        public bool NFTExists(string symbol, BigInteger tokenID);
        public bool FeedExists(string name);
        public bool PlatformExists(string name);

        public bool OrganizationExists(string name);
        public IOrganization GetOrganization(string name);

        public bool AddMember(string organization, Address admin, Address target);
        public bool RemoveMember(string organization, Address admin, Address target);
        public void MigrateMember(string organization, Address admin, Address source, Address destination);
        public void MigrateToken(Address from, Address to);

        public bool ContractExists(string name);
        public bool ContractDeployed(string name);

        public bool ArchiveExists(Hash hash);
        public IArchive GetArchive(Hash hash);
        public bool DeleteArchive(Hash hash);

        public bool AddOwnerToArchive(Hash hash, Address address);

        public bool RemoveOwnerFromArchive(Hash hash, Address address);

        public bool WriteArchive(IArchive archive, int blockIndex, byte[] data);

        public bool ChainExists(string name);
        public IChain GetChainByAddress(Address address);
        public IChain GetChainByName(string name);
        public int GetIndexOfChain(string name);

        public IChain GetChainParent(string name);

        public void Log(string description);
        public void Throw(string description);
        void Expect(bool condition, string description);
        public void Notify(EventKind kind, Address address, byte[] data);
        public void Notify(EventKind kind, Address address, byte[] bytes, string contract);
        public VMObject CallContext(string contextName, uint jumpOffset, string methodName, params object[] args);
        public VMObject CallInterop(string methodName, params object[] args);

        public Address LookUpName(string name);
        public bool HasAddressScript(Address from);
        public byte[] GetAddressScript(Address from);
        public string GetAddressName(Address from);

        public Event[] GetTransactionEvents(Hash transactionHash);
        public Hash[] GetTransactionHashesForAddress(Address address);

        public ValidatorEntry GetValidatorByIndex(int index);
        public ValidatorEntry[] GetValidators();
        public bool IsPrimaryValidator(Address address);
        public bool IsSecondaryValidator(Address address);
        public int GetPrimaryValidatorCount();
        public int GetSecondaryValidatorCount();
        public bool IsKnownValidator(Address address);

        public bool IsStakeMaster(Address address);
        public BigInteger GetStake(Address address);

        public BigInteger GetTokenPrice(string symbol);
        public BigInteger GetGovernanceValue(string name);

        public BigInteger GenerateUID();

        public TriggerResult InvokeTrigger(bool allowThrow, byte[] script, string contextName, ContractInterface abi, string triggerName, params object[] args);

        public VMObject InvokeContractAtTimestamp(NativeContractKind nativeContract, string methodName, params object[] args);
        public VMObject InvokeContractAtTimestamp(string contractName, string methodName, params object[] args);

        public bool IsWitness(Address address);
        public BigInteger GetBalance(string symbol, Address address);
        public BigInteger[] GetOwnerships(string symbol, Address address);
        public BigInteger GetTokenSupply(string symbol);

        public void CreateToken(Address owner, string symbol, string name, BigInteger maxSupply, int decimals, TokenFlags flags, byte[] script, ContractInterface abi);
        //public void SetPlatformTokenHash(string symbol, string platform, Hash hash);
        public void CreateChain(Address creator, string organization, string name, string parentChain);
        public void CreateFeed(Address owner, string name, FeedMode mode);
        public IArchive CreateArchive(MerkleTree merkleTree, Address owner, string name, BigInteger size, Timestamp time, IArchiveEncryption encryption);

        //public BigInteger CreatePlatform(Address from, string name, string externalAddress, Address interopAddress, string fuelSymbol);

        public bool IsAddressOfParentChain(Address address);
        public bool IsAddressOfChildChain(Address address);

        public bool IsPlatformAddress(Address address);
        public void RegisterPlatformAddress(string platform, Address localAddress, string externalAddress);

        public void MintTokens(string symbol, Address from, Address target, BigInteger amount);
        public void BurnTokens(string symbol, Address target, BigInteger amount);
        public void TransferTokens(string symbol, Address source, Address destination, BigInteger amount);
        public void SwapTokens(string sourceChain, Address from, string targetChain, Address to, string symbol, BigInteger value);
        public bool IsSystemToken(string symbol);

        public BigInteger MintToken(string symbol, Address from, Address target, byte[] rom, byte[] ram, BigInteger seriesID);
        public void BurnToken(string symbol, Address target, BigInteger tokenID);
        public void InfuseToken(string symbol, Address from, BigInteger tokenID, string infuseSymbol, BigInteger value);
        public void TransferToken(string symbol, Address source, Address destination, BigInteger tokenID);
        public void WriteToken(Address from, string tokenSymbol, BigInteger tokenID, byte[] ram);
        public TokenContent ReadToken(string tokenSymbol, BigInteger tokenID);
        public ITokenSeries CreateTokenSeries(string symbol, Address from, BigInteger seriesID, BigInteger maxSupply, TokenSeriesMode mode, byte[] script, ContractInterface abi);
        public ITokenSeries GetTokenSeries(string symbol, BigInteger seriesID);

        public byte[] ReadOracle(string URL);

        public IChainTask StartTask(Address from, string contractName, ContractMethod method, uint frequency, uint delay, TaskFrequencyMode mode, BigInteger gasLimit);
        public void StopTask(IChainTask task);
        public IChainTask GetTask(BigInteger taskID);

        TriggerResult InvokeTriggerOnContract(bool allowThrow, Address address, ContractTrigger trigger, params object[] args);
        TriggerResult InvokeTriggerOnToken(bool allowThrow, IToken token, TokenTrigger trigger, params object[] args);

        /*void AddAllowance(Address destination, string symbol, BigInteger amount);
        bool SubtractAllowance(Address destination, string symbol, BigInteger amount);
        void RemoveAllowance(Address destination, string symbol);*/
    }
}
