using System.Collections.Generic;
using System.Numerics;
using Google.Protobuf;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Storage.Context;
using Phantasma.Shared.Types;
using Tendermint;
using Tendermint.Types;
using TValidatorUpdate = Tendermint.Abci.ValidatorUpdate;

namespace Phantasma.Core.Domain;

public interface IChain
{
    INexus Nexus { get; }
    string Name { get; }
    Address Address { get; }
    Block CurrentBlock { get; }
    StorageChangeSetContext CurrentChangeSet { get; }
    PhantasmaKeys ValidatorKeys { get; set; }
    Address ValidatorAddress { get; }
    BigInteger Height { get; }
    StorageContext Storage { get; }
    bool IsRoot { get; }
    IContract[] GetContracts(StorageContext storage);

    IEnumerable<Transaction> BeginBlock(Header header, IEnumerable<Address> initialValidators);
    (CodeType, string) CheckTx(ByteString serializedTx);
    TransactionResult DeliverTx(ByteString serializedTx);
    byte[] Commit();
    IEnumerable<TValidatorUpdate> EndBlock();

    string ToString();
    void AddBlock(Block block, IEnumerable<Transaction> transactions, StorageChangeSetContext changeSet);

    BigInteger GetTokenBalance(StorageContext storage, IToken token, Address address);
    BigInteger GetTokenBalance(StorageContext storage, string symbol, Address address);
    BigInteger GetTokenSupply(StorageContext storage, string symbol);
    BigInteger[] GetOwnedTokens(StorageContext storage, string tokenSymbol, Address address);
    VMObject InvokeContractAtTimestamp(StorageContext storage, Timestamp time, NativeContractKind nativeContract, string methodName, params object[] args);
    VMObject InvokeContractAtTimestamp(StorageContext storage, Timestamp time, string contractName, string methodName, params object[] args);
    VMObject InvokeContract(StorageContext storage, NativeContractKind nativeContract, string methodName, params object[] args);
    VMObject InvokeContract(StorageContext storage, string contractName, string methodName, params object[] args);
    VMObject InvokeScript(StorageContext storage, byte[] script);
    VMObject InvokeScript(StorageContext storage, byte[] script, Timestamp time);
    BigInteger GenerateUID(StorageContext storage);
    BigInteger GetBlockReward(Block block);
    BigInteger GetTransactionFee(Transaction tx);
    BigInteger GetTransactionFee(Hash transactionHash);
    bool IsContractDeployed(StorageContext storage, string name);
    bool IsContractDeployed(StorageContext storage, Address contractAddress);
    bool DeployContractScript(StorageContext storage, Address contractOwner, string name, Address contractAddress, byte[] script, ContractInterface abi);
    SmartContract GetContractByAddress(StorageContext storage, Address contractAddress);
    SmartContract GetContractByName(StorageContext storage, string name);
    void UpgradeContract(StorageContext storage, string name, byte[] script, ContractInterface abi);
    void KillContract(StorageContext storage, string name);
    ExecutionContext GetContractContext(StorageContext storage, SmartContract contract);
    Address GetContractOwner(StorageContext storage, Address contractAddress);
    Hash GetLastBlockHash();
    Hash GetBlockHashAtHeight(BigInteger height);
    Block GetBlockByHash(Hash hash);
    bool ContainsBlockHash(Hash hash);
    BigInteger GetTransactionCount();
    bool ContainsTransaction(Hash hash);
    Transaction GetTransactionByHash(Hash hash);
    Hash GetBlockHashOfTransaction(Hash transactionHash);
    IEnumerable<Transaction> GetBlockTransactions(Block block);
    Hash[] GetTransactionHashesForAddress(Address address);
    Timestamp GetLastActivityOfAddress(Address address);
    void RegisterSwap(StorageContext storage, Address from, ChainSwap swap);
    ChainSwap GetSwap(StorageContext storage, Hash sourceHash);
    Hash[] GetSwapHashesForAddress(StorageContext storage, Address address);
    IChainTask StartTask(StorageContext storage, Address from, string contractName, ContractMethod method, uint frequency, uint delay, TaskFrequencyMode mode, BigInteger gasLimit);
    bool StopTask(StorageContext storage, BigInteger taskID);
    IChainTask GetTask(StorageContext storage, BigInteger taskID);
    Address GetValidator(StorageContext storage, Timestamp targetTime);
    void CloseBlock(Block block, StorageChangeSetContext storage);
    Address LookUpName(StorageContext storage, string name);
    string GetNameFromAddress(StorageContext storage, Address address);
}
