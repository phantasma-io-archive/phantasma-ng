using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Numerics;
using System.Text;
using Google.Protobuf;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.VM.Utils;
using Phantasma.Core;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;
using Serilog;

namespace Phantasma.Business.Blockchain
{
    public sealed class Chain : IChain
    {
        private const string TransactionHashMapTag = ".txs";
        private const string BlockHashMapTag = ".blocks";
        private const string BlockHeightListTag = ".height";
        private const string TxBlockHashMapTag = ".txblmp";
        private const string AddressTxHashMapTag = ".adblmp";
        private const string TaskListTag = ".tasks";

        private List<Transaction> CurrentTransactions = new();

        private Dictionary<string, int> _methodTableForGasExtraction = null;

#region PUBLIC
        public static readonly uint InitialHeight = 1;

        public INexus Nexus { get; private set; }

        public string Name { get; private set; }
        public Address Address { get; private set; }

        public Block CurrentBlock{ get; private set; }
        public string CurrentProposer { get; private set; }

        public StorageChangeSetContext CurrentChangeSet { get; private set; }

        public PhantasmaKeys ValidatorKeys { get; set; }
        public Address ValidatorAddress => ValidatorKeys != null ? ValidatorKeys.Address : Address.Null;

        public BigInteger Height => GetBlockHeight();

        public StorageContext Storage { get; private set; }

        public bool IsRoot => this.Name == DomainSettings.RootChainName;
#endregion

        public Chain(INexus nexus, string name)
        {
            Throw.IfNull(nexus, "nexus required");

            this.Name = name;
            this.Nexus = nexus;
            this.ValidatorKeys = null;

            this.Address = Address.FromHash(this.Name);

            this.Storage = (StorageContext)new KeyStoreStorage(Nexus.GetChainStorage(this.Name));
        }

        public Chain(INexus nexus, string name, PhantasmaKeys keys)
        {
            Throw.IfNull(nexus, "nexus required");

            this.Name = name;
            this.Nexus = nexus;
            this.ValidatorKeys = keys;

            this.Address = Address.FromHash(this.Name);

            this.Storage = (StorageContext)new KeyStoreStorage(Nexus.GetChainStorage(this.Name));
        }

        public IEnumerable<Transaction> BeginBlock(string proposerAddress, BigInteger height, BigInteger minimumFee, Timestamp timestamp, IEnumerable<Address> availableValidators)
        {
            // should never happen
            if (this.CurrentBlock != null)
            {
                // TODO error message
                throw new Exception("Cannot begin new block, current block has not been processed yet");
            }

            var lastBlockHash = this.GetLastBlockHash();
            var lastBlock = this.GetBlockByHash(lastBlockHash);
            var isFirstBlock = lastBlock == null;

            var protocol = Nexus.GetProtocolVersion(Nexus.RootStorage);
            this.CurrentProposer = proposerAddress;
            var validator = Nexus.GetValidator(this.Storage, this.CurrentProposer);

            Address validatorAddress = validator.address;

            if (validator.address == Address.Null)
            {
                foreach (var address in availableValidators)
                {
                    if (address.TendermintAddress == this.CurrentProposer)
                    {
                        validatorAddress = address;
                        break;
                    }
                }

                if (validatorAddress == Address.Null)
                {
                    throw new Exception("Unknown validator");
                }
            }

            this.CurrentBlock = new Block(height
                , this.Address
                , timestamp
                , isFirstBlock ? Hash.Null : lastBlock.Hash
                , protocol
                , validatorAddress
                , new byte[0]
            );

            // create new storage context
            this.CurrentChangeSet = new StorageChangeSetContext(this.Storage);
            List<Transaction> systemTransactions = new ();

            if (this.IsRoot)
            {
                var inflationReady = NativeContract.LoadFieldFromStorage<bool>(this.CurrentChangeSet, NativeContractKind.Gas, nameof(GasContract._inflationReady));
                if (inflationReady)
                {
                    var senderAddress = this.CurrentBlock.Validator;

                    // NOTE inflation is a expensive transaction so it requires a larger gas limit compared to other transactions
                    var requiredGasLimit = Transaction.DefaultGasLimit * 4;

                    var script = new ScriptBuilder()
                        .AllowGas(senderAddress, Address.Null, minimumFee, requiredGasLimit)
                        .CallContract(NativeContractKind.Gas, nameof(GasContract.ApplyInflation), this.CurrentBlock.Validator)
                        .SpendGas(senderAddress)
                        .EndScript();

                    var transaction = new Transaction(
                            this.Nexus.Name,
                            this.Name,
                            script,
                            this.CurrentBlock.Timestamp.Value + 1,
                            "SYSTEM");

                    transaction.Sign(this.ValidatorKeys);
                    systemTransactions.Add(transaction);
                }
            }

            var oracle = Nexus.GetOracleReader();
            systemTransactions.AddRange(ProcessPendingTasks(this.CurrentBlock, oracle, minimumFee, this.CurrentChangeSet));

            // returns eventual system transactions that need to be broadcasted to tenderm int to be included into the current block
            return systemTransactions;
        }

        public (CodeType, string) CheckTx(Transaction tx, Timestamp timestamp)
        {
            Log.Information("check tx {Hash}", tx.Hash);

            if (tx.Expiration < timestamp)
            {
                var type = CodeType.Expired;
                Log.Information("check tx error {Expired} {Hash}", type, tx.Hash);
                return (type, "Transaction is expired");
            }

            if (!tx.IsValid(this))
            {
                Log.Information("check tx 2 " + tx.Hash);
                return (CodeType.InvalidChain, "Transaction is not meant to be executed on this chain");
            }

            if (tx.Signatures.Length == 0)
            {
                var type = CodeType.UnsignedTx;
                Log.Information("check tx error {UsignedTx} {Hash}", type, tx.Hash);
                return (type, "Transaction is not signed");
            }

            if (Nexus.HasGenesis())
            {
                Address from, target;
                BigInteger gasPrice, gasLimit;

                if (_methodTableForGasExtraction == null)
                {
                    _methodTableForGasExtraction = GenerateMethodTable();
                }

                var methods = DisasmUtils.ExtractMethodCalls(tx.Script, _methodTableForGasExtraction);

                if (!TransactionExtensions.ExtractGasDetailsFromMethods(methods, out from, out target, out gasPrice, out gasLimit, _methodTableForGasExtraction))
                {
                    var type = CodeType.NoUserAddress;
                    Log.Information("check tx error {type} {Hash}", type, tx.Hash);
                    return (type, "AllowGas call not found in transaction script (or wrong number of arguments)");
                }

                /*if (from.IsNull || target.IsNull || gasLimit <= 0 || gasPrice <= 0)
                {
                    var type = CodeType.NoSystemAddress;
                    Log.Information("check tx error {type} {Hash}", type, tx.Hash);
                    return (type, "AllowGas call not found in transaction script");
                }*/
                
                var whitelisted = TransactionExtensions.IsWhitelisted(methods);

                if (!whitelisted)
                {
                    var minFee = Nexus.GetGovernanceValue(Nexus.RootStorage, GovernanceContract.GasMinimumFeeTag);
                    if (gasPrice < minFee)
                    {
                        var type = CodeType.GasFeeTooLow;
                        Log.Information("check tx error {type} {Hash}", type, tx.Hash);
                        return (type, "Gas fee too low");
                    }

                    var minGasRequired = gasPrice * gasLimit;
                    var balance = GetTokenBalance(this.Storage, DomainSettings.FuelTokenSymbol, from);
                    if (balance < minGasRequired)
                    {
                        var type = CodeType.MissingFuel;
                        Log.Information("check tx error {MissingFuel} {Hash}", type, tx.Hash);

                        if (balance == 0)
                        {
                            return (type, $"Missing fuel, {from} has 0 {DomainSettings.FuelTokenSymbol}");
                        }
                        else
                        {
                            return (type, $"Missing fuel, {from} has {UnitConversion.ToDecimal(balance, DomainSettings.FuelTokenDecimals)} {DomainSettings.FuelTokenSymbol} expected at least {UnitConversion.ToDecimal(minGasRequired, DomainSettings.FuelTokenDecimals)} {DomainSettings.FuelTokenSymbol}");
                        }
                    }
                }

            }

            if (tx.Script.Length == 0)
            {
                var type = CodeType.InvalidScript;
                Log.Information("check tx error {type} {Hash}", type, tx.Hash);
                return (type, "Script attached to tx is invalid");
            }

            //if (!VerifyBlockBeforeAdd(this.CurrentBlock))
            //{
            //    throw new BlockGenerationException($"block verification failed, would have overflown, hash:{this.CurrentBlock.Hash}");
            //}

            Log.Information("check tx Successful {Hash}", tx.Hash);
            return (CodeType.Ok, "");
        }

        internal void FlushExtCalls()
        {
            // make it null here to force next txs received to rebuild it
            _methodTableForGasExtraction = null;
        }

        private Dictionary<string, int> GenerateMethodTable()
        {
            var table = DisasmUtils.GetDefaultDisasmTable();

            var contracts = GetContracts(this.Storage);

            foreach (var contract in contracts)
            {
                var nativeKind = contract.Name.FindNativeContractKindByName();
                if (nativeKind != NativeContractKind.Unknown)
                {
                    continue; // we skip native contracts as those are already in the dictionary from GetDefaultDisasmTable()
                }

                table.AddContractToTable(contract);
            }

            var tokens = this.Nexus.GetTokens(Nexus.RootStorage);
            foreach (var symbol in tokens)
            {
                if (Nexus.IsSystemToken(symbol))
                {
                    continue;
                }

                var token = Nexus.GetTokenInfo(Nexus.RootStorage, symbol);
                table.AddTokenToTable(token);
            }

            return table;
        }

        public IEnumerable<T> EndBlock<T>() where T : class
        {
            //if (Height == 1)
            //{
            //    throw new ChainException("genesis transaction failed");
            //}
            
            // TODO currently the managing of the ABI cache is broken so we have to call this at end of the block
            ((Chain)Nexus.RootChain).FlushExtCalls();
            
            // TODO return block events
            if (typeof(T) == typeof(Block))
            {
                this.CurrentBlock.AddOraclesEntries(Nexus.GetOracleReader().Entries);
                //var blocks = new List<Block>() { CurrentBlock };
                //return (List<T>) Convert.ChangeType(blocks, typeof(List<T>));
            }
            
            // TODO validator update
            return new List<T>();
        }

        public TransactionResult DeliverTx(Transaction tx)
        {
            TransactionResult result = new();

            Log.Information("Deliver tx {Hash}", tx);

            try
            {
                if (CurrentTransactions.Any(x => x.Hash == tx.Hash))
                {
                    throw new ChainException("Duplicated transaction hash");
                }

                CurrentTransactions.Add(tx);
                var txIndex = CurrentTransactions.Count - 1;
                var oracle = Nexus.GetOracleReader();

                // create snapshot
                var snapshot = this.CurrentChangeSet.Clone();

                result = ExecuteTransaction(txIndex, tx, tx.Script, this.CurrentBlock.Validator,
                    this.CurrentBlock.Timestamp, snapshot, this.CurrentBlock.Notify, oracle,
                    ChainTask.Null);

                if (result.State == ExecutionState.Halt)
                {
                    if (result.Result != null)
                    {
                        var resultBytes = Serialization.Serialize(result.Result);
                        this.CurrentBlock.SetResultForHash(tx.Hash, resultBytes);
                    }

                    snapshot.Execute();
                }
                else
                {
                    snapshot = null;
                }

                this.CurrentBlock.SetStateForHash(tx.Hash, result.State);
                
            }
            catch (Exception e)
            {
                // log original exception, throwing it again kills the call stack!
                Log.Error("Exception for {Hash} in DeliverTx {Exception}", tx.Hash, e);
                result.Code = 1;
                result.Codespace = e.Message;
                result.State = ExecutionState.Fault;
                this.CurrentBlock.SetStateForHash(tx.Hash, result.State);
            }

            return result;
        }

        public byte[] Commit()
        {
            Log.Information("Committing block {Height}", this.CurrentBlock.Height);
            try
            {
                AddBlock(this.CurrentBlock, this.CurrentTransactions, this.CurrentChangeSet);
            }
            catch (Exception e)
            {
                // Commit cannot throw anything, an error in this phase has to stop the node!
                Log.Error("Critical failure {Error}", e);
                Environment.Exit(-1);
            }

            Block lastBlock = this.CurrentBlock;
            this.CurrentBlock = null;
            this.CurrentTransactions.Clear();

            Log.Information("Committed block {Height}", lastBlock.Height);

            return lastBlock.Hash.ToByteArray();
        }

        public IContract[] GetContracts(StorageContext storage)
        {
            var contractList = new StorageList(GetContractListKey(), storage);
            var addresses = contractList.All<Address>();
            return addresses.Select(x => this.GetContractByAddress(storage, x)).ToArray();
        }

        public override string ToString()
        {
            return $"{Name} ({Address})";
        }

        private bool VerifyBlockBeforeAdd(Block block)
        {
            if (block.TransactionCount >= DomainSettings.MaxTxPerBlock)
            {
                return false;
            }

            /* THOSE DONT WORK because the block is still empty!
            
            if (block.OracleData.Count() >= DomainSettings.MaxOracleEntriesPerBlock)
            {
                return false;
            }

            if (block.Events.Count() >= DomainSettings.MaxEventsPerBlock)
            {
                return false;
            }*/

            return true;
        }

        public void AddBlock(Block block, IEnumerable<Transaction> transactions, StorageChangeSetContext changeSet)
        {
            block.AddAllTransactionHashes(transactions.Select (x => x.Hash).ToArray());

            // from here on, the block is accepted
            changeSet.Execute();

            var hashList = new StorageList(BlockHeightListTag, this.Storage);
            hashList.Add<Hash>(block.Hash);

            // persist genesis hash at height 1
            if (block.Height == 1)
            {
                var genesisHash = block.Hash;
                Nexus.CommitGenesis(genesisHash);
            }

            var blockMap = new StorageMap(BlockHashMapTag, this.Storage);

            var blockBytes = block.ToByteArray(true);

            var blk = Block.Unserialize(blockBytes);
            blockBytes = CompressionUtils.Compress(blockBytes);
            blockMap.Set<Hash, byte[]>(block.Hash, blockBytes);

            var txMap = new StorageMap(TransactionHashMapTag, this.Storage);
            var txBlockMap = new StorageMap(TxBlockHashMapTag, this.Storage);
            foreach (Transaction tx in transactions)
            {
                var txBytes = tx.ToByteArray(true);
                txBytes = CompressionUtils.Compress(txBytes);
                txMap.Set<Hash, byte[]>(tx.Hash, txBytes);
                txBlockMap.Set<Hash, Hash>(tx.Hash, block.Hash);
            }
        

            foreach (var transaction in transactions)
            {
                var addresses = new HashSet<Address>();
                var events = block.GetEventsForTransaction(transaction.Hash);

                foreach (var evt in events)
                {
                    if (evt.Contract == "gas" && (evt.Address.IsSystem || evt.Address == block.Validator))
                    {
                        continue;
                    }

                    addresses.Add(evt.Address);
                }

                var addressTxMap = new StorageMap(AddressTxHashMapTag, this.Storage);
                foreach (var address in addresses)
                {
                    var addressList = addressTxMap.Get<Address, StorageList>(address);
                    addressList.Add<Hash>(transaction.Hash);
                }
            }
        }

        private TransactionResult ExecuteTransaction(int index, Transaction transaction, byte[] script, Address validator, Timestamp time, StorageChangeSetContext changeSet
                , Action<Hash, Event> onNotify, IOracleReader oracle, IChainTask task)
        {
            var result = new TransactionResult();

            result.Hash = transaction.Hash;

            uint offset = 0;

            RuntimeVM runtime;
            runtime = new RuntimeVM(index, script, offset, this, validator, time, transaction, changeSet, oracle, task);
            
            result.State = runtime.Execute();

            result.Events = runtime.Events.ToArray();
            result.GasUsed = (long)runtime.UsedGas;

            foreach (var evt in runtime.Events)
            {
                onNotify(transaction.Hash, evt);
            }
            

            if (result.State != ExecutionState.Halt)
            {
                result.Code = 1;
                result.Codespace = runtime.ExceptionMessage ?? "Execution Unsuccessful";
                return result;
            }

            if (runtime.Stack.Count > 0)
            {
                result.Result = runtime.Stack.Pop();
            }

            // merge transaction oracle data
            oracle.MergeTxData();

            result.Code = 0;
            result.Codespace = "Execution Successful";
            return result;
        }

        // NOTE should never be used directly from a contract, instead use Runtime.GetBalance!
        public BigInteger GetTokenBalance(StorageContext storage, IToken token, Address address)
        {
            if (token.Flags.HasFlag(TokenFlags.Fungible))
            {
                var balances = new BalanceSheet(token);
                return balances.Get(storage, address);
            }
            else
            {
                var ownerships = new OwnershipSheet(token.Symbol);
                var items = ownerships.Get(storage, address);
                return items.Length;
            }
        }

        public BigInteger GetTokenBalance(StorageContext storage, string symbol, Address address)
        {
            var token = Nexus.GetTokenInfo(storage, symbol);
            return GetTokenBalance(storage, token, address);
        }

        public BigInteger GetTokenSupply(StorageContext storage, string symbol)
        {
            var supplies = new SupplySheet(symbol, this, Nexus);
            return supplies.GetTotal(storage);
        }

        // NOTE this lists only nfts owned in this chain
        public BigInteger[] GetOwnedTokens(StorageContext storage, string tokenSymbol, Address address)
        {
            var ownership = new OwnershipSheet(tokenSymbol);
            return ownership.Get(storage, address).ToArray();
        }

        /// <summary>
        /// Deletes all blocks starting at the specified hash.
        /// </summary>
        /*
        public void DeleteBlocks(Hash targetHash)
        {
            var targetBlock = FindBlockByHash(targetHash);
            Throw.IfNull(targetBlock, nameof(targetBlock));

            var currentBlock = this.LastBlock;
            while (true)
            {
                Throw.IfNull(currentBlock, nameof(currentBlock));

                var changeSet = _blockChangeSets[currentBlock.Hash];
                changeSet.Undo();

                _blockChangeSets.Remove(currentBlock.Hash);
                _blockHeightMap.Remove(currentBlock.Height);
                _blocks.Remove(currentBlock.Hash);

                currentBlock = FindBlockByHash(currentBlock.PreviousHash);

                if (currentBlock.PreviousHash == targetHash)
                {
                    break;
                }
            }
        }*/

        public ExecutionContext GetContractContext(StorageContext storage, SmartContract contract)
        {
            if (!IsContractDeployed(storage, contract.Address))
            {
                throw new ChainException($"contract '{contract.Name}' not deployed on '{Name}' chain");
            }

            var context = new ChainExecutionContext(contract);
            return context;
        }

        public VMObject InvokeContractAtTimestamp(StorageContext storage, Timestamp time, NativeContractKind nativeContract, string methodName, params object[] args)
        {
            return InvokeContractAtTimestamp(storage, time, nativeContract.GetContractName(), methodName, args);
        }

        public VMObject InvokeContractAtTimestamp(StorageContext storage, Timestamp time, string contractName, string methodName, params object[] args)
        {
            var script = ScriptUtils.BeginScript().CallContract(contractName, methodName, args).EndScript();

            var result = InvokeScript(storage, script, time);

            if (result == null)
            {
                throw new ChainException($"Invocation of method '{methodName}' of contract '{contractName}' failed");
            }

            return result;
        }

        public VMObject InvokeScript(StorageContext storage, byte[] script, Timestamp time)
        {
            var oracle = Nexus.GetOracleReader();
            var changeSet = new StorageChangeSetContext(storage);
            uint offset = 0;
            var vm = new RuntimeVM(-1, script, offset, this, Address.Null, time, Transaction.Null, changeSet, oracle, ChainTask.Null);

            var state = vm.Execute();

            if (state != ExecutionState.Halt)
            {
                return null;
            }

            if (vm.Stack.Count == 0)
            {
                throw new ChainException($"No result, vm stack is empty");
            }

            var result = vm.Stack.Pop();

            return result;
        }

        // generates incremental ID (unique to this chain)
        public BigInteger GenerateUID(StorageContext storage)
        {
            var key = Encoding.ASCII.GetBytes("_uid");

            var lastID = storage.Has(key) ? storage.Get<BigInteger>(key) : 0;

            lastID++;
            storage.Put<BigInteger>(key, lastID);

            return lastID;
        }

#region FEES
        public BigInteger GetBlockReward(Block block)
        {
            if (block.TransactionCount == 0)
            {
                return 0;
            }

            var lastTxHash = block.TransactionHashes[block.TransactionHashes.Length - 1];
            var evts = block.GetEventsForTransaction(lastTxHash);

            BigInteger total = 0;
            foreach (var evt in evts)
            {
                if (evt.Kind == EventKind.TokenClaim && evt.Contract == "block")
                {
                    var data = evt.GetContent<TokenEventData>();
                    total += data.Value;
                }
            }

            return total;
        }

        public BigInteger GetTransactionFee(Transaction tx)
        {
            Throw.IfNull(tx, nameof(tx));
            return GetTransactionFee(tx.Hash);
        }

        public BigInteger GetTransactionFee(Hash transactionHash)
        {
            Throw.IfNull(transactionHash, nameof(transactionHash));

            BigInteger fee = 0;

            var blockHash = GetBlockHashOfTransaction(transactionHash);
            var block = GetBlockByHash(blockHash);
            Throw.IfNull(block, nameof(block));

            var events = block.GetEventsForTransaction(transactionHash);
            foreach (var evt in events)
            {
                if (evt.Kind == EventKind.GasPayment && evt.Contract == "gas")
                {
                    var info = evt.GetContent<GasEventData>();
                    fee += info.amount * info.price;
                }
            }

            return fee;
        }
#endregion

#region Contracts
        private byte[] GetContractListKey()
        {
            return Encoding.ASCII.GetBytes("contracts.");
        }

        private byte[] GetContractKey(Address contractAddress, string field)
        {
            var bytes = Encoding.ASCII.GetBytes(field);
            var key = ByteArrayUtils.ConcatBytes(bytes, contractAddress.ToByteArray());
            return key;
        }

        public bool IsContractDeployed(StorageContext storage, string name)
        {
            if (ValidationUtils.IsValidTicker(name))
            {
                return Nexus.TokenExists(storage, name);
            }

            return IsContractDeployed(storage, SmartContract.GetAddressFromContractName(name));
        }

        public bool IsContractDeployed(StorageContext storage, Address contractAddress)
        {
            if (contractAddress == SmartContract.GetAddressForNative(NativeContractKind.Gas))
            {
                return true;
            }

            if (contractAddress == SmartContract.GetAddressForNative(NativeContractKind.Block))
            {
                return true;
            }

            if (contractAddress == SmartContract.GetAddressForNative(NativeContractKind.Unknown))
            {
                return false;
            }

            var key = GetContractKey(contractAddress, "script");
            if (storage.Has(key))
            {
                return true;
            }

            var token = Nexus.GetTokenInfo(storage, contractAddress);
            return (token != null);
        }

        public bool DeployContractScript(StorageContext storage, Address contractOwner, string name, Address contractAddress, byte[] script, ContractInterface abi)
        {
            var scriptKey = GetContractKey(contractAddress, "script");
            if (storage.Has(scriptKey))
            {
                return false;
            }

            storage.Put(scriptKey, script);

            var ownerBytes = contractOwner.ToByteArray();
            var ownerKey = GetContractKey(contractAddress, "owner");
            storage.Put(ownerKey, ownerBytes);

            var abiBytes = abi.ToByteArray();
            var abiKey = GetContractKey(contractAddress, "abi");
            storage.Put(abiKey, abiBytes);

            var nameBytes = Encoding.ASCII.GetBytes(name);
            var nameKey = GetContractKey(contractAddress, "name");
            storage.Put(nameKey, nameBytes);

            var contractList = new StorageList(GetContractListKey(), storage);
            contractList.Add<Address>(contractAddress);

            FlushExtCalls();

            return true;
        }

        public SmartContract GetContractByAddress(StorageContext storage, Address contractAddress)
        {
            var nameKey = GetContractKey(contractAddress, "name");

            if (storage.Has(nameKey))
            {
                var nameBytes = storage.Get(nameKey);

                var name = Encoding.ASCII.GetString(nameBytes);
                return GetContractByName(storage, name);
            }

            var symbols = Nexus.GetTokens(storage);
            foreach (var symbol in symbols)
            {
                var tokenAddress = TokenUtils.GetContractAddress(symbol);

                if (tokenAddress == contractAddress)
                {
                    var token = Nexus.GetTokenInfo(storage, symbol);
                    return new CustomContract(token.Symbol, token.Script, token.ABI);
                }
            }

            return NativeContract.GetNativeContractByAddress(contractAddress);
        }

        public SmartContract GetContractByName(StorageContext storage, string name)
        {
            if (Nexus.IsNativeContract(name) || ValidationUtils.IsValidTicker(name))
            {
                return Nexus.GetContractByName(storage, name);
            }

            var address = SmartContract.GetAddressFromContractName(name);
            var scriptKey = GetContractKey(address, "script");
            if (!storage.Has(scriptKey))
            {
                return null;
            }

            var script = storage.Get(scriptKey);

            var abiKey = GetContractKey(address, "abi");
            var abiBytes = storage.Get(abiKey);
            var abi = ContractInterface.FromBytes(abiBytes);

            return new CustomContract(name, script, abi);
        }

        public void UpgradeContract(StorageContext storage, string name, byte[] script, ContractInterface abi)
        {
            if (Nexus.IsNativeContract(name) || ValidationUtils.IsValidTicker(name))
            {
                throw new ChainException($"Cannot upgrade this type of contract: {name}");
            }

            if (!IsContractDeployed(storage, name))
            {
                throw new ChainException($"Cannot upgrade non-existing contract: {name}");
            }

            var address = SmartContract.GetAddressFromContractName(name);

            var scriptKey = GetContractKey(address, "script");
            storage.Put(scriptKey, script);

            var abiKey = GetContractKey(address, "abi");
            var abiBytes = abi.ToByteArray();
            storage.Put(abiKey, abiBytes);

            FlushExtCalls();
        }

        public void KillContract(StorageContext storage, string name)
        {
            if (Nexus.IsNativeContract(name) || ValidationUtils.IsValidTicker(name))
            {
                throw new ChainException($"Cannot kill this type of contract: {name}");
            }

            if (!IsContractDeployed(storage, name))
            {
                throw new ChainException($"Cannot kill non-existing contract: {name}");
            }

            var address = SmartContract.GetAddressFromContractName(name);

            var scriptKey = GetContractKey(address, "script");
            storage.Delete(scriptKey);

            var abiKey = GetContractKey(address, "abi");
            storage.Delete(abiKey);

            // TODO clear other storage used by contract (global variables, maps, lists, etc)
        }

        public Address GetContractOwner(StorageContext storage, Address contractAddress)
        {
            if (contractAddress.IsSystem)
            {
                var ownerKey = GetContractKey(contractAddress, "owner");
                var bytes = storage.Get(ownerKey);
                if (bytes != null)
                {
                    return Address.FromBytes(bytes);
                }

                var token = Nexus.GetTokenInfo(storage, contractAddress);
                if (token != null)
                {
                    return token.Owner;
                }
            }

            return Address.Null;
        }

#endregion

        private BigInteger GetBlockHeight()
        {
            var hashList = new StorageList(BlockHeightListTag, this.Storage);
            return hashList.Count();
        }

        public Hash GetLastBlockHash()
        {
            var lastHeight = GetBlockHeight();
            if (lastHeight <= 0)
            {
                return Hash.Null;
            }

            return GetBlockHashAtHeight(lastHeight);
        }

        public Hash GetBlockHashAtHeight(BigInteger height)
        {
            if (height <= 0)
            {
                throw new ChainException("invalid block height");
            }

            if (height > this.Height)
            {
                return Hash.Null;
            }

            var hashList = new StorageList(BlockHeightListTag, this.Storage);
            // NOTE chain heights start at 1, but list index start at 0
            var hash = hashList.Get<Hash>(height - 1);
            return hash;
        }

        public Block GetBlockByHash(Hash hash)
        {
            if (hash == Hash.Null)
            {
                return null;
            }

            var blockMap = new StorageMap(BlockHashMapTag, this.Storage);

            if (blockMap.ContainsKey<Hash>(hash))
            {
                var bytes = blockMap.Get<Hash, byte[]>(hash);
                bytes = CompressionUtils.Decompress(bytes);
                var block = Block.Unserialize(bytes);

                if (block.Hash != hash)
                {
                    throw new ChainException("data corruption on block: " + hash);
                }

                return block;
            }

            return null;
        }

        public bool ContainsBlockHash(Hash hash)
        {
            return GetBlockByHash(hash) != null;
        }

        public BigInteger GetTransactionCount()
        {
            var txMap = new StorageMap(TransactionHashMapTag, this.Storage);
            return txMap.Count();
        }

        public bool ContainsTransaction(Hash hash)
        {
            var txMap = new StorageMap(TransactionHashMapTag, this.Storage);
            return txMap.ContainsKey(hash);
        }

        public Transaction GetTransactionByHash(Hash hash)
        {
            var txMap = new StorageMap(TransactionHashMapTag, this.Storage);
            if (txMap.ContainsKey<Hash>(hash))
            {
                var bytes = txMap.Get<Hash, byte[]>(hash);
                bytes = CompressionUtils.Decompress(bytes);
                var tx = Transaction.Unserialize(bytes);

                if (tx.Hash != hash)
                {
                    throw new ChainException("data corruption on transaction: " + hash);
                }

                return tx;
            }

            return null;
        }

        public Hash GetBlockHashOfTransaction(Hash transactionHash)
        {
            var txBlockMap = new StorageMap(TxBlockHashMapTag, this.Storage);

            if (txBlockMap.ContainsKey(transactionHash))
            {
                var blockHash = txBlockMap.Get<Hash, Hash>(transactionHash);
                return blockHash;
            }

            return Hash.Null;
        }

        public IEnumerable<Transaction> GetBlockTransactions(Block block)
        {
            return block.TransactionHashes.Select(hash => GetTransactionByHash(hash));
        }

        public Hash[] GetTransactionHashesForAddress(Address address)
        {
            var addressTxMap = new StorageMap(AddressTxHashMapTag, this.Storage);
            var addressList = addressTxMap.Get<Address, StorageList>(address);
            return addressList.All<Hash>();
        }

        public Timestamp GetLastActivityOfAddress(Address address)
        {
            var addressTxMap = new StorageMap(AddressTxHashMapTag, this.Storage);
            var addressList = addressTxMap.Get<Address, StorageList>(address);
            var count = addressList.Count();
            if (count <=0)
            {
                return Timestamp.Null;
            }

            var lastTxHash = addressList.Get<Hash>(count - 1);
            var blockHash = GetBlockHashOfTransaction(lastTxHash);

            var block = GetBlockByHash(blockHash);

            if (block == null) // should never happen
            {
                return Timestamp.Null;
            }

            return block.Timestamp;
        }

#region SWAPS
        private StorageList GetSwapListForAddress(StorageContext storage, Address address)
        {
            var key = ByteArrayUtils.ConcatBytes(Encoding.UTF8.GetBytes(".swapaddr"), address.ToByteArray());
            return new StorageList(key, storage);
        }

        private StorageMap GetSwapMap(StorageContext storage)
        {
            var key = Encoding.UTF8.GetBytes(".swapmap");
            return new StorageMap(key, storage);
        }

        public void RegisterSwap(StorageContext storage, Address from, ChainSwap swap)
        {
            var list = GetSwapListForAddress(storage, from);
            list.Add<Hash>(swap.sourceHash);

            var map = GetSwapMap(storage);
            map.Set<Hash, ChainSwap>(swap.sourceHash, swap);
        }

        public ChainSwap GetSwap(StorageContext storage, Hash sourceHash)
        {
            var map = GetSwapMap(storage);

            if (map.ContainsKey<Hash>(sourceHash))
            {
                return map.Get<Hash, ChainSwap>(sourceHash);
            }

            throw new ChainException("invalid chain swap hash: " + sourceHash);
        }

        public Hash[] GetSwapHashesForAddress(StorageContext storage, Address address)
        {
            var list = GetSwapListForAddress(storage, address);
            return list.All<Hash>();
        }
#endregion

#region TASKS
        private byte[] GetTaskKey(BigInteger taskID, string field)
        {
            var bytes = Encoding.ASCII.GetBytes(field);
            var key = ByteArrayUtils.ConcatBytes(bytes, taskID.ToUnsignedByteArray());
            return key;
        }

        public IChainTask StartTask(StorageContext storage, Address from, string contractName, ContractMethod method, uint frequency, uint delay, TaskFrequencyMode mode, BigInteger gasLimit)
        {
            if (!IsContractDeployed(storage, contractName))
            {
                return null;
            }

            var taskID = GenerateUID(storage);
            var task = new ChainTask(taskID, from, contractName, method.name, frequency, delay, mode, gasLimit, this.Height + 1, true);

            var taskKey = GetTaskKey(taskID, "task_info");

            var taskBytes = task.ToByteArray();

            storage.Put(taskKey, taskBytes);

            var taskList = new StorageList(TaskListTag, this.Storage);
            taskList.Add<BigInteger>(taskID);

            return task;
        }

        public bool StopTask(StorageContext storage, BigInteger taskID)
        {
            var taskKey = GetTaskKey(taskID, "task_info");

            if (this.Storage.Has(taskKey))
            {
                this.Storage.Delete(taskKey);

                taskKey = GetTaskKey(taskID, "task_run");
                if (this.Storage.Has(taskKey))
                {
                    this.Storage.Delete(taskKey);
                }

                var taskList = new StorageList(TaskListTag, this.Storage);
                taskList.Remove<BigInteger>(taskID);

                return true;
            }

            return false;
        }

        public IChainTask GetTask(StorageContext storage, BigInteger taskID)
        {
            var taskKey = GetTaskKey(taskID, "task_info");

            var taskBytes = this.Storage.Get(taskKey);

            var task = ChainTask.FromBytes(taskID, taskBytes);

            return task;

        }

        private IEnumerable<Transaction> ProcessPendingTasks(Block block, IOracleReader oracle, BigInteger minimumFee, StorageChangeSetContext changeSet)
        {
            var taskList = new StorageList(TaskListTag, changeSet);
            var taskCount = taskList.Count();

            List<Transaction> transactions = null;

            int i = 0;
            while (i < taskCount)
            {
                var taskID = taskList.Get<BigInteger>(i);
                var task = GetTask(changeSet, taskID);

                Transaction tx;

                var taskResult = ProcessPendingTask(block, oracle, minimumFee, changeSet, task, out tx);
                if (taskResult == TaskResult.Running)
                {
                    i++;
                }
                else
                {
                    taskList.RemoveAt(i);
                }

                if (tx != null)
                {
                    if (transactions == null)
                    {
                        transactions = new List<Transaction>();
                    }

                    transactions.Add(tx);
                }
            }

            if (transactions != null)
            {
                return transactions;
            }

            return Enumerable.Empty<Transaction>();
        }

        private BigInteger GetTaskTimeFromBlock(TaskFrequencyMode mode, Block block)
        {
            switch (mode)
            {
                case TaskFrequencyMode.Blocks:
                    {
                        return block.Height;
                    }

                case TaskFrequencyMode.Time:
                    {
                        return block.Timestamp.Value;
                    }

                default:
                    throw new ChainException("Unknown task mode: " + mode);
            }
        }

        private TaskResult ProcessPendingTask(Block block, IOracleReader oracle, BigInteger minimumFee,
                StorageChangeSetContext changeSet, IChainTask task, out Transaction transaction)
        {
            transaction = null;

            BigInteger currentRun = GetTaskTimeFromBlock(task.Mode, block);
            var taskKey = GetTaskKey(task.ID, "task_run");

            if (task.Mode != TaskFrequencyMode.Always)
            {
                bool isFirstRun = !changeSet.Has(taskKey);

                if (isFirstRun)
                {
                    var taskBlockHash = GetBlockHashAtHeight(task.Height);
                    var taskBlock = GetBlockByHash(taskBlockHash);

                    BigInteger firstRun = GetTaskTimeFromBlock(task.Mode, taskBlock) + task.Delay;

                    if (currentRun < firstRun)
                    {
                        return TaskResult.Skipped; // skip execution for now
                    }
                }
                else
                {
                    BigInteger lastRun = isFirstRun ? changeSet.Get<BigInteger>(taskKey) : 0;

                    var diff = currentRun - lastRun;
                    if (diff < task.Frequency)
                    {
                        return TaskResult.Skipped; // skip execution for now
                    }
                }
            }
            else
            {
                currentRun = 0;
            }
            
            var taskScript = new ScriptBuilder()
                .AllowGas(task.Owner, Address.Null, minimumFee, task.GasLimit)
                .CallContract(task.ContextName, task.Method)
                .SpendGas(task.Owner)
                .EndScript();

            transaction = new Transaction(this.Nexus.Name, this.Name, taskScript, block.Timestamp.Value + 1, "TASK");

            var txResult = ExecuteTransaction(-1, transaction, transaction.Script, block.Validator, block.Timestamp, changeSet,
                        block.Notify, oracle, task);
            if (txResult.Code == 0)
            {
                var resultBytes = Serialization.Serialize(txResult.Result);
                block.SetResultForHash(transaction.Hash, resultBytes);

                block.SetStateForHash(transaction.Hash, txResult.State);

                // update last_run value in storage
                if (currentRun > 0)
                {
                    changeSet.Put<BigInteger>(taskKey, currentRun);
                }

                var shouldStop = txResult.Result.AsBool();
                return shouldStop ? TaskResult.Halted : TaskResult.Running;
            }

            block.SetStateForHash(transaction.Hash, txResult.State);
            return TaskResult.Crashed;
            
        }
#endregion

#region block validation
        public void CloseBlock(Block block, StorageChangeSetContext storage)
        {
            var rootStorage = this.IsRoot ? storage : Nexus.RootStorage;

            if (block.Height > 1)
            {
                var prevBlock = GetBlockByHash(block.PreviousHash);

                if (prevBlock.Validator != block.Validator)
                {
                    block.Notify(new Event(EventKind.ValidatorSwitch, block.Validator, "block", Serialization.Serialize(prevBlock)));
                }
            }

            var tokenStorage = this.Name == DomainSettings.RootChainName ? storage : Nexus.RootStorage;
            var token = this.Nexus.GetTokenInfo(tokenStorage, DomainSettings.FuelTokenSymbol);
            var balance = new BalanceSheet(token);
            var blockAddress = Address.FromHash("block");
            var totalAvailable = balance.Get(storage, blockAddress);

            var targets = new List<Address>();

            if (Nexus.HasGenesis())
            {
                var validators = Nexus.GetValidators(block.Timestamp);

                var totalValidators = Nexus.GetPrimaryValidatorCount(block.Timestamp);

                for (int i = 0; i < totalValidators; i++)
                {
                    var validator = validators[i];
                    if (validator.type != ValidatorType.Primary)
                    {
                        continue;
                    }

                    targets.Add(validator.address);
                }
            }

            if (targets.Count > 0)
            {
                if (!balance.Subtract(storage, blockAddress, totalAvailable))
                {
                    throw new BlockGenerationException("could not subtract balance from block address");
                }

                var amountPerValidator = totalAvailable / targets.Count;
                var leftOvers = totalAvailable - (amountPerValidator * targets.Count);

                foreach (var address in targets)
                {
                    BigInteger amount = amountPerValidator;

                    if (address == block.Validator)
                    {
                        amount += leftOvers;
                    }

                    // TODO this should use triggers when available...
                    if (!balance.Add(storage, address, amount))
                    {
                        throw new BlockGenerationException($"could not add balance to {address}");
                    }

                    var eventData = Serialization.Serialize(new TokenEventData(DomainSettings.FuelTokenSymbol, amount, this.Name));
                    block.Notify(new Event(EventKind.TokenClaim, address, "block", eventData));
                }
            }
        }
#endregion

        public Address LookUpName(StorageContext storage, string name, Timestamp timestamp)
        {
            if (IsContractDeployed(storage, name))
            {
                return SmartContract.GetAddressFromContractName(name);
            }

            return this.Nexus.LookUpName(storage, name, timestamp);
        }

        public string GetNameFromAddress(StorageContext storage, Address address, Timestamp timestamp)
        {
            if (address.IsNull)
            {
                return ValidationUtils.NULL_NAME;
            }

            if (address.IsSystem)
            {
                if (address == DomainSettings.InfusionAddress)
                {
                    return DomainSettings.InfusionName;
                }

                var contract = this.GetContractByAddress(storage, address);
                if (contract != null)
                {
                    return contract.Name;
                }
                else
                {
                    var tempChain = Nexus.GetChainByAddress(address);
                    if (tempChain != null)
                    {
                        return tempChain.Name;
                    }

                    var org = Nexus.GetOrganizationByAddress(storage, address);
                    if (org != null)
                    {
                        return org.ID;
                    }

                    return ValidationUtils.ANONYMOUS_NAME;
                }
            }

            return Nexus.RootChain.InvokeContractAtTimestamp(storage, timestamp, NativeContractKind.Account, nameof(AccountContract.LookUpAddress), address).AsString();
        }

    }
}
