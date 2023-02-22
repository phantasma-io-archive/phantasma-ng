using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Numerics;
using System.Text;
using Google.Protobuf;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Storage;
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
using Serilog.Core;
using Transaction = Phantasma.Core.Domain.Transaction;

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

        private List<ITransaction> CurrentTransactions = new();

        private Dictionary<string, int> _methodTableForGasExtraction = null;

#region PUBLIC
        public static readonly uint InitialHeight = 1;

        public INexus Nexus { get; private set; }

        public string Name { get; private set; }
        public Address Address { get; private set; }

        public Block CurrentBlock{ get; private set; }
        public IEnumerable<ITransaction> Transactions => CurrentTransactions;
        public string CurrentProposer { get; private set; }

        public StorageChangeSetContext CurrentChangeSet { get; private set; }

        public PhantasmaKeys ValidatorKeys { get; set; }
        public Address ValidatorAddress => ValidatorKeys != null ? ValidatorKeys.Address : Address.Null;

        public BigInteger Height => GetBlockHeight();

        public StorageContext Storage => StorageCollection.MainStorage;
        public StorageCollection StorageCollection { get; private set; }
      

        public bool IsRoot => this.Name == DomainSettings.RootChainName;
#endregion

        public Chain(INexus nexus, string name)
        {
            Throw.IfNull(nexus, "nexus required");

            this.Name = name;
            this.Nexus = nexus;
            this.ValidatorKeys = null;

            this.Address = Address.FromHash(this.Name);

            StorageCollection = new StorageCollection(this.Nexus, this.Name);
        }

        public Chain(INexus nexus, string name, PhantasmaKeys keys)
        {
            Throw.IfNull(nexus, "nexus required");

            this.Name = name;
            this.Nexus = nexus;
            this.ValidatorKeys = keys;

            this.Address = Address.FromHash(this.Name);

            StorageCollection = new StorageCollection(this.Nexus, this.Name);
        }
        
#region Chain methods
        /// <summary>
        /// Method called when a new block is being created
        /// </summary>
        /// <param name="proposerAddress"></param>
        /// <param name="height"></param>
        /// <param name="minimumFee"></param>
        /// <param name="timestamp"></param>
        /// <param name="availableValidators"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public IEnumerable<ITransaction> BeginBlock(string proposerAddress, BigInteger height, BigInteger minimumFee, Timestamp timestamp, IEnumerable<Address> availableValidators)
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
            uint protocol = DomainSettings.Phantasma30Protocol;
            try
            {
                if (lastBlockHash != Hash.Null)
                    protocol = Nexus.GetProtocolVersion(this.StorageCollection.ContractsStorage);
            }
            catch (Exception e)
            {
                Log.Information("Error getting info {Exception}", e);
            }
            
            this.CurrentProposer = proposerAddress;
            var validator = Nexus.GetValidator(this.StorageCollection.MainStorage, this.CurrentProposer);

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
            this.CurrentChangeSet = new StorageChangeSetContext(this.StorageCollection.ContractsStorage);
            List<ITransaction> systemTransactions = new ();
            var oracle = Nexus.GetOracleReader();

            if (this.IsRoot)
            {
                bool inflationReady = false;
                if ( protocol <= 12)
                    inflationReady = Filter.Enabled ? false : NativeContract.LoadFieldFromStorage<bool>(this.CurrentChangeSet, NativeContractKind.Gas, nameof(GasContract._inflationReady));
                else
                    inflationReady = NativeContract.LoadFieldFromStorage<bool>(this.CurrentChangeSet, NativeContractKind.Gas, nameof(GasContract._inflationReady));

                if (inflationReady)
                {
                    var senderAddress = this.CurrentBlock.Validator;

                    // NOTE inflation is a expensive transaction so it requires a larger gas limit compared to other transactions
                    int requiredGasLimit = Transaction.DefaultGasLimit * 50;
                    if ( Nexus.GetGovernanceValue(this.StorageCollection.ContractsStorage,  Phantasma.Business.Blockchain.Nexus.NexusProtocolVersionTag) <= 8)
                        requiredGasLimit = Transaction.DefaultGasLimit * 4;
                    

                    var script = new ScriptBuilder()
                        .AllowGas(senderAddress, Address.Null, minimumFee, requiredGasLimit)
                        .CallContract(NativeContractKind.Gas, nameof(GasContract.ApplyInflation), this.CurrentBlock.Validator)
                        .SpendGas(senderAddress)
                        .EndScript();

                    ITransaction transaction;
                    if (protocol >= 13)
                    {
                        transaction = new Transaction(
                            this.Nexus.Name,
                            this.Name,
                            script,
                            this.CurrentBlock.Timestamp.Value + 1,
                            senderAddress,
                            Address.Null,
                            minimumFee,
                            requiredGasLimit,
                            "SYSTEM");
                    }
                    else
                    {
                        transaction = new Transaction(
                            this.Nexus.Name,
                            this.Name,
                            script,
                            this.CurrentBlock.Timestamp.Value + 1,
                            "SYSTEM");
                    }
                    
                    transaction.Sign(this.ValidatorKeys);
                    systemTransactions.Add(transaction);
                }
            }

            systemTransactions.AddRange(ProcessPendingTasks(this.CurrentBlock, oracle, minimumFee, this.CurrentChangeSet));

            // returns eventual system transactions that need to be broadcasted to tenderm int to be included into the current block
            return systemTransactions;
        }

        /// <summary>
        /// Method called when a new block is being created
        /// </summary>
        /// <param name="tx"></param>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        public (CodeType, string) CheckTx(ITransaction tx, Timestamp timestamp)
        {
            uint protocolVersion = Nexus.GetProtocolVersion(this.StorageCollection.ContractsStorage);
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

            if (protocolVersion >= 13)
            {
                if (tx.Script.Length > DomainSettings.ArchiveMaxSize)
                {
                    var type = CodeType.Error;
                    Log.Information("check tx error {ScriptTooBig} {Hash}", type, tx.Hash);
                    return (type, "Transaction script is too big");
                }

                if (this.ContainsTransaction(tx.Hash))
                {
                    var type = CodeType.Error;
                    Log.Information("check tx error {Error} {Hash}", type, tx.Hash);
                    return (type, "Transaction already exists in chain");
                }
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
                
                if ( protocolVersion >= 13)
                {
                    var transaction = tx as Transaction;
                   
                    if (transaction.TransactionGas != TransactionGas.Null)
                    {
                        from = transaction.TransactionGas.GasPayer;
                        target = transaction.TransactionGas.GasTarget;
                        gasPrice = transaction.TransactionGas.GasPrice;
                        gasLimit = transaction.TransactionGas.GasLimit;
                    }
                    else
                    {
                        var result = this.ExtractGasInformation(tx, out from, out target, out gasPrice,
                            out gasLimit, methods, _methodTableForGasExtraction);

                        if (result.Item1 != CodeType.Ok)
                        {
                            return (result.Item1, result.Item2);
                        }
                    }
                    
                }
                else
                {
                    var result = this.ExtractGasInformation(tx, out from, out target, out gasPrice, out gasLimit, methods, _methodTableForGasExtraction);

                    if (result.Item1 != CodeType.Ok)
                    {
                        return (result.Item1, result.Item2);
                    }
                }

                if (protocolVersion >= 13)
                {
                    if (from.IsNull  || gasPrice <= 0 || gasLimit <= 0)
                    {
                        var type = CodeType.NoSystemAddress;
                        Log.Information("check tx error {type} {Hash}", type, tx.Hash);
                        return (type, "AllowGas or GasPayer / GasTarget / GasPrice / GasLimit call not found in transaction script");
                    }

                    if (!tx.IsSignedBy(from))
                    {
                        var type = CodeType.Error;
                        Log.Information("check tx error {Error} {Hash}", type, tx.Hash);
                        return (type, "Transaction was not signed by the caller address");
                    }
                }
                
                var whitelisted = TransactionExtensions.IsWhitelisted(methods);
                if (whitelisted)
                {
                    if (methods.Any(x => x.MethodName.Equals(nameof(SwapContract.SwapFee)) || x.MethodName.Equals(nameof(ExchangeContract.SwapFee))))
                    {
                        var existsLPToken = Nexus.TokenExists(this.StorageCollection.ContractsStorage, DomainSettings.LiquidityTokenSymbol);
                        var exchangeVersion = this.InvokeContractAtTimestamp(this.StorageCollection.ContractsStorage, CurrentBlock.Timestamp, NativeContractKind.Exchange, nameof(ExchangeContract.GetDexVerion)).AsNumber();
                        if (existsLPToken && exchangeVersion >= 1) // Check for the Exchange contract
                        {
                            var exchangePot = GetTokenBalance(this.StorageCollection.AddressBalancesStorage, DomainSettings.FuelTokenSymbol, SmartContract.GetAddressForNative(NativeContractKind.Exchange));
                            if (exchangePot < UnitConversion.GetUnitValue(DomainSettings.FuelTokenDecimals)) {
                                return (CodeType.Error, $"Empty pot Exchange");
                            }
                        }
                        else
                        {
                            // Run the Swap contract
                            var pot = GetTokenBalance(this.StorageCollection.AddressBalancesStorage, DomainSettings.FuelTokenSymbol, SmartContract.GetAddressForNative(NativeContractKind.Swap));
                            if (pot < UnitConversion.GetUnitValue(DomainSettings.FuelTokenDecimals)) {
                                return (CodeType.Error, $"Empty pot Swap");
                            }
                        }
                    }
                }
                else
                {
                    var minFee = Nexus.GetGovernanceValue(this.StorageCollection.ContractsStorage, GovernanceContract.GasMinimumFeeTag);
                    if (gasPrice < minFee)
                    {
                        var type = CodeType.GasFeeTooLow;
                        Log.Information("check tx error {type} {Hash}", type, tx.Hash);
                        return (type, "Gas fee too low");
                    }

                    var minGasRequired = gasPrice * gasLimit;
                    var balance = GetTokenBalance(this.StorageCollection.AddressBalancesStorage, DomainSettings.FuelTokenSymbol, from);
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
        
        /// <summary>
        /// Method called Run the transaction
        /// </summary>
        /// <param name="tx"></param>
        /// <returns></returns>
        /// <exception cref="ChainException"></exception>
        public TransactionResult DeliverTx(ITransaction tx)
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
                var collectionChangeSet = new StorageCollectionChangeSet(this.StorageCollection);

                result = ExecuteTransaction(txIndex, tx, tx.Script, this.CurrentBlock.Validator,
                    this.CurrentBlock.Timestamp, collectionChangeSet, this.CurrentBlock.Notify, oracle,
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

                ProcessFilteredExceptions(e.Message);
            }

            return result;
        }
        
        /// <summary>
        /// Method called at the end of a block to process the block events
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
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
        
        /// <summary>
        /// Method called to commit the block to the chain
        /// </summary>
        /// <returns></returns>
        public byte[] Commit()
        {
            Log.Information("Committing block {Height}", this.CurrentBlock.Height);
            if (!this.CurrentBlock.IsSigned)
            {
                if ( this.CurrentBlock.Validator == ValidatorKeys.Address)
                {
                    this.CurrentBlock.Sign(ValidatorKeys);
                }
            }
            Block lastBlock = this.CurrentBlock;
            
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

            this.CurrentBlock = null;
            this.CurrentTransactions.Clear();
            return lastBlock.Hash.ToByteArray();
        }
        
        /// <summary>
        /// Method called to add a block to the chain
        /// </summary>
        /// <param name="block"></param>
        /// <param name="transactions"></param>
        /// <param name="changeSet"></param>
        public void AddBlock(Block block, IEnumerable<ITransaction> transactions, StorageChangeSetContext changeSet)
        {
            block.AddAllTransactionHashes(transactions.Select (x => x.Hash).ToArray());
            
            this.SetBlock(block, transactions, changeSet);
        }

        /// <summary>
        /// Method called to set a block to the chain
        /// </summary>
        /// <param name="block"></param>
        /// <param name="transactions"></param>
        /// <param name="changeSet"></param>
        /// <returns></returns>
        /// <exception cref="ChainException"></exception>
        public byte[] SetBlock(Block block, IEnumerable<ITransaction> transactions, StorageChangeSetContext changeSet)
        {

            // Validate block 
            if (!VerifyBlockBeforeAdd(block))
            {
                throw new ChainException("Invalid block");
            }
            
            if (!block.IsSigned)
            {
                throw new ChainException("Block is not signed");
            }
                
            if ( block.PreviousHash != this.CurrentBlock.PreviousHash)
            {
                throw new ChainException("Block previous hash is not the same as the current block");
            }
                
            if ( block.Height != this.CurrentBlock.Height)
            {
                throw new ChainException("Block height is not the same as the current block");
            }

            if (block.Timestamp != this.CurrentBlock.Timestamp)
            {
                throw new ChainException("Block timestamp is not the same as the current block");
            }

            if (block.ChainAddress != this.CurrentBlock.ChainAddress)
            {
                throw new ChainException("Block chain address is not the same as the current block");
            }
                
            if ( block.Events.Count() != this.CurrentBlock.Events.Count())
            {
                throw new ChainException("Block events are not the same as the current block");
            }
            
            if ( block.Events.Except(this.CurrentBlock.Events).Count() != 0 && this.CurrentBlock.Events.Except(block.Events).Count() != 0 )
            {
                var blockEvents = block.Events.ToArray();
                var currentBlockEvents = this.CurrentBlock.Events.ToArray();
                
                for(int i = 0; i < blockEvents.Length; i++)
                {
                    if (!blockEvents[i].Equals(currentBlockEvents[i]))
                    {
                        throw new ChainException($"Block events are not the same as the current block\n {blockEvents[i]}\n {currentBlockEvents[i]}");
                    }
                }
            }
                
            if ( block.Protocol != this.CurrentBlock.Protocol)
            {
                throw new ChainException("Block protocol is not the same as the current block");
            }
            
            if ( !Nexus.IsPrimaryValidator(block.Validator, Timestamp.Now) )
            {
                throw new ChainException("Block validator is not a valid validator");
            }

            var transactionHashs = transactions.Select(x => x.Hash).ToArray();
            if ( block.TransactionHashes.Count() != transactionHashs.Count())
            {
                throw new ChainException("Block transaction hashes are not the same as the current block");
            }
            
            if ( this.CurrentBlock.TransactionCount == 0)
                this.CurrentBlock.AddAllTransactionHashes(transactionHashs);
            
            if ( block.TransactionHashes.Except(transactionHashs).Count() != 0 && transactionHashs.Except(block.TransactionHashes).Count() != 0)
            {
                var blockTransactionHashes = block.TransactionHashes.ToArray();
                var currentBlockTransactionHashes = transactionHashs.ToArray();
                
                for(int i = 0; i < blockTransactionHashes.Length; i++)
                {
                    if (!blockTransactionHashes[i].Equals(currentBlockTransactionHashes[i]))
                    {
                        throw new ChainException($"Block transaction hashes are not the same as the current block\n {blockTransactionHashes[i]}\n {currentBlockTransactionHashes[i]}");
                    }
                }
            }
            
            if ( transactions.Select(tx => !this.ContainsTransaction(tx.Hash)).All(valid => !valid))
            {
                throw new ChainException("Block transactions are not valid");
            }

            if (transactions.Select(tx => tx.IsValid(this)).All(valid => !valid))
            {
                throw new ChainException("Block transactions are not valid");
            }
        
            if ( transactions.Count() != this.Transactions.Count())
            {
                throw new ChainException($"Block transactions are not the same as the current block, {transactions.Count()} != {this.Transactions.Count()} | {this.CurrentBlock.TransactionCount}");
            }

            if (transactions.Except(this.Transactions).Count() != 0 && this.Transactions.Except(transactions).Count() != 0)
            {
                var blockTransactions = transactions.ToArray();
                var currentBlockTransactions = this.Transactions.ToArray();
                
                for(int i = 0; i < blockTransactions.Length; i++)
                {
                    if (!blockTransactions[i].Equals(currentBlockTransactions[i]))
                    {
                        throw new ChainException($"Block transactions are not the same as the current block\n {blockTransactions[i]}\n {currentBlockTransactions[i]}");
                    }
                }
            }
            
            // from here on, the block is accepted
            changeSet.Execute();
            
            var hashList = new StorageList(BlockHeightListTag, this.StorageCollection.BlocksStorage);
            hashList.Add<Hash>(block.Hash);
            
            // persist genesis hash at height 1
            if (block.Height == 1)
            {
                var genesisHash = block.Hash;
                Nexus.CommitGenesis(genesisHash);
            }
            
            var blockMap = new StorageMap(BlockHashMapTag, this.StorageCollection.BlocksStorage);
            
            var blockBytes = block.ToByteArray(true);

            var blk = Block.Unserialize(blockBytes);
            blockBytes = CompressionUtils.Compress(blockBytes);
            blockMap.Set<Hash, byte[]>(block.Hash, blockBytes);

            var txMap = new StorageMap(TransactionHashMapTag, this.StorageCollection.TransactionsStorage);
            var txBlockMap = new StorageMap(TxBlockHashMapTag, this.StorageCollection.BlocksTransactionsStorage);

            foreach (ITransaction tx in transactions)
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

                var addressTxMap = new StorageMap(AddressTxHashMapTag, this.StorageCollection.AddressTransactionsStorage);
                foreach (var address in addresses)
                {
                    var addressList = addressTxMap.Get<Address, StorageList>(address);
                    addressList.Add<Hash>(transaction.Hash);
                }
            }
            
            Block lastBlock = this.CurrentBlock;

            this.CurrentBlock = null;
            this.CurrentTransactions.Clear();
            
            Log.Information("Committed block {Height}", lastBlock.Height);

            return lastBlock.Hash.ToByteArray();
        }
#endregion
        
        /// <summary>
        /// Flush the method table for gas extraction
        /// </summary>
        internal void FlushExtCalls()
        {
            // make it null here to force next txs received to rebuild it
            _methodTableForGasExtraction = null;
        }
        
        /// <summary>
        /// Generate the method table for gas extraction
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, int> GenerateMethodTable()
        {
            var table = DisasmUtils.GetDefaultDisasmTable();

            var contracts = GetContracts(this.StorageCollection.ContractsStorage);

            foreach (var contract in contracts)
            {
                var nativeKind = contract.Name.FindNativeContractKindByName();
                if (nativeKind != NativeContractKind.Unknown)
                {
                    continue; // we skip native contracts as those are already in the dictionary from GetDefaultDisasmTable()
                }

                table.AddContractToTable(contract);
            }

            var tokens = this.Nexus.GetTokens(this.StorageCollection.ContractsStorage);
            foreach (var symbol in tokens)
            {
                if (Nexus.IsSystemToken(symbol) && symbol != DomainSettings.LiquidityTokenSymbol)
                {
                    continue;
                }

                var token = Nexus.GetTokenInfo(this.StorageCollection.ContractsStorage, symbol);
                table.AddTokenToTable(token);
            }

            return table;
        }
        
        /// <summary>
        /// Method called to process the filtered exceptions
        /// </summary>
        /// <param name="exceptionMessage"></param>
        private void ProcessFilteredExceptions(string exceptionMessage)
        {
            var filteredAddress = Filter.ExtractFilteredAddress(exceptionMessage);

            if (!filteredAddress.IsNull)
            {
                Filter.AddRedFilteredAddress(this.StorageCollection.MainStorage, filteredAddress);
            }
        }

        public override string ToString()
        {
            return $"{Name} ({Address})";
        }

        /// <summary>
        /// Method called to verify the block before adding it to the chain.
        /// </summary>
        /// <param name="block"></param>
        /// <returns></returns>
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
        
        private TransactionResult ExecuteTransaction(int index, ITransaction transaction, byte[] script, Address validator, Timestamp time, StorageChangeSetContext changeSet
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
                ProcessFilteredExceptions(result.Codespace);
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
        
        private TransactionResult ExecuteTransaction(int index, ITransaction transaction, byte[] script, Address validator, Timestamp time, StorageCollectionChangeSet collectionChangeSet
            , Action<Hash, Event> onNotify, IOracleReader oracle, IChainTask task)
        {
            var result = new TransactionResult();

            result.Hash = transaction.Hash;

            uint offset = 0;

            RuntimeVM runtime;
            runtime = new RuntimeVM(index, script, offset, this, validator, time, transaction, collectionChangeSet, oracle, task);
            
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
                ProcessFilteredExceptions(result.Codespace);
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
            var token = Nexus.GetTokenInfo(this.StorageCollection.ContractsStorage, symbol);
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
            var key = Encoding.ASCII.GetBytes(".uid");

            var lastID = storage.Has(key) ? storage.Get<BigInteger>(key) : 0;

            lastID++;
            storage.Put<BigInteger>(key, lastID);

            return lastID;
        }

#region FEES
        /// <summary>
        /// Returns the total fee paid for a block.
        /// </summary>
        /// <param name="block"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Returns the total fee paid for a transaction.
        /// </summary>
        /// <param name="tx"></param>
        /// <returns></returns>
        public BigInteger GetTransactionFee(ITransaction tx)
        {
            Throw.IfNull(tx, nameof(tx));
            return GetTransactionFee(tx.Hash);
        }

        /// <summary>
        /// Returns the total fee paid for a transaction.
        /// </summary>
        /// <param name="transactionHash"></param>
        /// <returns></returns>
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
        /// <summary>
        /// Returns all deployed contracts on this chain.
        /// </summary>
        /// <param name="storage"></param>
        /// <returns></returns>
        public IContract[] GetContracts(StorageContext storage)
        {
            var contractList = new StorageList(SmartContractSheet.GetContractListKey(), storage);
            var addresses = contractList.All<Address>();
            return addresses.Select(x => this.GetContractByAddress(storage, x)).ToArray();
        }

        /// <summary>
        /// Returns the Execution Context for the specified contract.
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="contract"></param>
        /// <returns></returns>
        /// <exception cref="ChainException"></exception>
        public ExecutionContext GetContractContext(StorageContext storage, SmartContract contract)
        {
            if (!IsContractDeployed(storage, contract.Address))
            {
                throw new ChainException($"contract '{contract.Name}' not deployed on '{Name}' chain");
            }

            var context = new ChainExecutionContext(contract);
            return context;
        }

        /// <summary>
        /// Returns the true if the contract is deployed on this chain.
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool IsContractDeployed(StorageContext storage, string name)
        {
            if (ValidationUtils.IsValidTicker(name))
            {
                return Nexus.TokenExists(storage, name);
            }

            return IsContractDeployed(storage, SmartContract.GetAddressFromContractName(name));
        }

        /// <summary>
        /// Returns true if the contract is deployed on this chain.
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="contractAddress"></param>
        /// <returns></returns>
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

            var contract = new SmartContractSheet(contractAddress);
            if ( contract.HasScript(storage) )
            {
                return true;
            }

            var token = Nexus.GetTokenInfo(storage, contractAddress);
            return (token != null);
        }

        /// <summary>
        /// Deploy a smart contract to the chain.
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="contractOwner"></param>
        /// <param name="name"></param>
        /// <param name="contractAddress"></param>
        /// <param name="script"></param>
        /// <param name="abi"></param>
        /// <returns></returns>
        public bool DeployContractScript(StorageContext storage, Address contractOwner, string name, Address contractAddress, byte[] script, ContractInterface abi)
        {
            var contract = new SmartContractSheet(name, contractAddress);
            if (contract.HasScript(storage))
            {
                return false;
            }

            contract.PutScript(storage, script);

            var ownerBytes = contractOwner.ToByteArray();
            contract.PutOwner(storage, ownerBytes);

            var abiBytes = abi.ToByteArray();
            contract.PutABI(storage, abiBytes);
            
            var nameBytes = Encoding.ASCII.GetBytes(name);
            contract.PutName(storage, nameBytes);
            contract.Add(storage, contractAddress);

            FlushExtCalls();

            return true;
        }

        /// <summary>
        /// Get the contract by Address.
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="contractAddress"></param>
        /// <returns></returns>
        public SmartContract GetContractByAddress(StorageContext storage, Address contractAddress)
        {
            var contract = new SmartContractSheet(contractAddress);

            if (contract.HasName(this.StorageCollection.ContractsStorage))
            {
                var nameBytes = contract.GetName(storage);

                var name = Encoding.ASCII.GetString(nameBytes);
                return GetContractByName(storage, name);
            }

            var symbols = Nexus.GetTokens(this.StorageCollection.ContractsStorage);
            foreach (var symbol in symbols)
            {
                var tokenAddress = TokenUtils.GetContractAddress(symbol);

                if (tokenAddress == contractAddress)
                {
                    var token = Nexus.GetTokenInfo(this.StorageCollection.ContractsStorage, symbol);
                    return new CustomContract(token.Symbol, token.Script, token.ABI);
                }
            }

            return NativeContract.GetNativeContractByAddress(contractAddress);
        }

        /// <summary>
        /// Get Contract by Name.
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public SmartContract GetContractByName(StorageContext storage, string name)
        {
            if (Blockchain.Nexus.IsNativeContract(name) || ValidationUtils.IsValidTicker(name))
            {
                return Nexus.GetContractByName(storage, name);
            }

            var address = SmartContract.GetAddressFromContractName(name);
            var contract = new SmartContractSheet(address);
            if (!contract.HasScript(storage))
            {
                return null;
            }

            var script = contract.GetScript(storage);
            var abiBytes = contract.GetABI(storage);
            var abi = ContractInterface.FromBytes(abiBytes);

            return new CustomContract(name, script, abi);
        }

        /// <summary>
        /// Upgrade a smart contract on the chain.
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="name"></param>
        /// <param name="script"></param>
        /// <param name="abi"></param>
        /// <exception cref="ChainException"></exception>
        public void UpgradeContract(StorageContext storage, string name, byte[] script, ContractInterface abi)
        {
            if (Blockchain.Nexus.IsNativeContract(name) || ValidationUtils.IsValidTicker(name))
            {
                throw new ChainException($"Cannot upgrade this type of contract: {name}");
            }

            if (!IsContractDeployed(storage, name))
            {
                throw new ChainException($"Cannot upgrade non-existing contract: {name}");
            }

            var address = SmartContract.GetAddressFromContractName(name);
            var abiBytes = abi.ToByteArray();
            var contract = new SmartContractSheet(name, address);
            contract.PutScript(storage, script);
            contract.PutABI(storage, abiBytes);

            FlushExtCalls();
        }

        /// <summary>
        /// Kill a smart contract on the chain. (remove it from the list of deployed contracts)
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="name"></param>
        /// <exception cref="ChainException"></exception>
        public void KillContract(StorageContext storage, string name)
        {
            if (Blockchain.Nexus.IsNativeContract(name) || ValidationUtils.IsValidTicker(name))
            {
                throw new ChainException($"Cannot kill this type of contract: {name}");
            }

            if (!IsContractDeployed(storage, name))
            {
                throw new ChainException($"Cannot kill non-existing contract: {name}");
            }

            var address = SmartContract.GetAddressFromContractName(name);
            
            var contract = new SmartContractSheet(address);
            contract.DeleteOwner(storage);
            contract.DeleteName(storage);
            contract.DeleteScript(storage);
            contract.DeleteABI(storage);
            
            // TODO clear other storage used by contract (global variables, maps, lists, etc)
            //contract.DeleteContract(storage);
        }

        /// <summary>
        /// Get the contract owner.
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="contractAddress"></param>
        /// <returns></returns>
        public Address GetContractOwner(StorageContext storage, Address contractAddress)
        {
            if (contractAddress.IsSystem)
            {
                var contract = new SmartContractSheet(contractAddress);
                var owner = contract.GetOwner(storage);
                if (owner != Address.Null)
                {
                    return owner;
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

#region Blocks
        /// <summary>
        /// Returns the current block height
        /// </summary>
        /// <returns></returns>
        private BigInteger GetBlockHeight()
        {
            var hashList = new StorageList(BlockHeightListTag, this.StorageCollection.BlocksStorage);
            return hashList.Count();
        }

        /// <summary>
        /// Returns the last block hash
        /// </summary>
        /// <returns></returns>
        public Hash GetLastBlockHash()
        {
            var lastHeight = GetBlockHeight();
            if (lastHeight <= 0)
            {
                return Hash.Null;
            }

            return GetBlockHashAtHeight(lastHeight);
        }

        /// <summary>
        /// Returns the block at the given height
        /// </summary>
        /// <param name="height"></param>
        /// <returns></returns>
        /// <exception cref="ChainException"></exception>
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

            var hashList = new StorageList(BlockHeightListTag, this.StorageCollection.BlocksStorage);
            // NOTE chain heights start at 1, but list index start at 0
            var hash = hashList.Get<Hash>(height - 1);
            return hash;
        }

        /// <summary>
        /// Returns the block at the given height
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        /// <exception cref="ChainException"></exception>
        public Block GetBlockByHash(Hash hash)
        {
            if (hash == Hash.Null)
            {
                return null;
            }

            var blockMap = new StorageMap(BlockHashMapTag, this.StorageCollection.BlocksStorage);

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

        /// <summary>
        /// Returns if a block hash is present in the chain
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public bool ContainsBlockHash(Hash hash)
        {
            return GetBlockByHash(hash) != null;
        }
        
        /// <summary>
        /// Returns the block hash of a transaction
        /// </summary>
        /// <param name="transactionHash"></param>
        /// <returns></returns>
        public Hash GetBlockHashOfTransaction(Hash transactionHash)
        {
            var txBlockMap = new StorageMap(TxBlockHashMapTag, this.StorageCollection.BlocksTransactionsStorage);

            if (txBlockMap.ContainsKey(transactionHash))
            {
                var blockHash = txBlockMap.Get<Hash, Hash>(transactionHash);
                return blockHash;
            }

            return Hash.Null;
        }
#endregion
        
#region Transactions
        /// <summary>
        /// Returns the current transaction count
        /// </summary>
        /// <returns></returns>
        public BigInteger GetTransactionCount()
        {
            var txMap = new StorageMap(TransactionHashMapTag, this.StorageCollection.TransactionsStorage);
            return txMap.Count();
        }

        /// <summary>
        /// Returns if a transaction hash is present in the chain
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public bool ContainsTransaction(Hash hash)
        {
            var txMap = new StorageMap(TransactionHashMapTag, this.StorageCollection.TransactionsStorage);
            return txMap.ContainsKey(hash);
        }

        /// <summary>
        /// Returns the transaction by the given hash
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        /// <exception cref="ChainException"></exception>
        public ITransaction GetTransactionByHash(Hash hash)
        {
            var txMap = new StorageMap(TransactionHashMapTag, this.StorageCollection.TransactionsStorage);
            if (txMap.ContainsKey<Hash>(hash))
            {
                var protocolVersion = Nexus.GetProtocolVersion(this.StorageCollection.ContractsStorage);
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
        
        /// <summary>
        /// Returns the transactions of a block
        /// </summary>
        /// <param name="block"></param>
        /// <returns></returns>
        public IEnumerable<ITransaction> GetBlockTransactions(Block block)
        {
            return block.TransactionHashes.Select(hash => GetTransactionByHash(hash));
        }
        
        /// <summary>
        /// Returns the transaction for a given address
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public Hash[] GetTransactionHashesForAddress(Address address)
        {
            var addressTxMap = new StorageMap(AddressTxHashMapTag, this.StorageCollection.AddressTransactionsStorage);
            var addressList = addressTxMap.Get<Address, StorageList>(address);
            return addressList.All<Hash>();
        }
#endregion

#region SWAPS
        /// <summary>
        /// Returns the swap list for the given address
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        private StorageList GetSwapListForAddress(StorageContext storage, Address address)
        {
            var key = ByteArrayUtils.ConcatBytes(Encoding.UTF8.GetBytes(".swapaddr"), address.ToByteArray());
            return new StorageList(key, storage);
        }

        /// <summary>
        /// Returns the swap map
        /// </summary>
        /// <param name="storage"></param>
        /// <returns></returns>
        private StorageMap GetSwapMap(StorageContext storage)
        {
            var key = Encoding.UTF8.GetBytes(".swapmap");
            return new StorageMap(key, storage);
        }
        
        /// <summary>
        /// Registers a swap
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="from"></param>
        /// <param name="swap"></param>
        public void RegisterSwap(StorageContext storage, Address from, ChainSwap swap)
        {
            var list = GetSwapListForAddress(storage, from);
            list.Add<Hash>(swap.sourceHash);

            var map = GetSwapMap(storage);
            map.Set<Hash, ChainSwap>(swap.sourceHash, swap);
        }

        /// <summary>
        /// Returns the swap for the given hash
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="sourceHash"></param>
        /// <returns></returns>
        /// <exception cref="ChainException"></exception>
        public ChainSwap GetSwap(StorageContext storage, Hash sourceHash)
        {
            var map = GetSwapMap(storage);

            if (map.ContainsKey<Hash>(sourceHash))
            {
                return map.Get<Hash, ChainSwap>(sourceHash);
            }

            throw new ChainException("invalid chain swap hash: " + sourceHash);
        }

        /// <summary>
        /// Returns the swap hashes for the given address
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="address"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Stars a task on the chain and returns the task ID
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="from"></param>
        /// <param name="contractName"></param>
        /// <param name="method"></param>
        /// <param name="frequency"></param>
        /// <param name="delay"></param>
        /// <param name="mode"></param>
        /// <param name="gasLimit"></param>
        /// <returns></returns>
        public IChainTask StartTask(StorageContext storage, Address from, string contractName, ContractMethod method, uint frequency, uint delay, TaskFrequencyMode mode, BigInteger gasLimit)
        {
            if (!IsContractDeployed(storage, contractName))
            {
                return null;
            }
            
            var taskID = GenerateUID(storage);
            var task = new ChainTask(taskID, from, contractName, method.name, frequency, delay, mode, gasLimit, this.Height + 1, true);

            var taskSheet = new TaskSheet(taskID);
            var taskBytes = task.ToByteArray();
            taskSheet.PutTaskInfo(storage, taskBytes);
            
            taskSheet.Add(storage, taskID);
            return task;
        }

        /// <summary>
        /// Stops a task on the chain
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="taskID"></param>
        /// <returns></returns>
        public bool StopTask(StorageContext storage, BigInteger taskID)
        {
            var taskSheet = new TaskSheet(taskID);

            if (taskSheet.HasTaskInfo(storage))
            {
                taskSheet.DeleteInfo(storage);

                if (taskSheet.HasTaskRun(storage))
                {
                    taskSheet.DeleteRun(storage);
                }

                taskSheet.Remove(storage, taskID);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the task for the given ID
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="taskID"></param>
        /// <returns></returns>
        public IChainTask GetTask(StorageContext storage, BigInteger taskID)
        {
            var taskSheet = new TaskSheet(taskID);

            var taskBytes = taskSheet.GetTaskInfo(storage);

            var task = ChainTask.FromBytes(taskID, taskBytes);

            return task;
        }
        
        /// <summary>
        /// Processes the pending tasks
        /// </summary>
        /// <param name="block"></param>
        /// <param name="oracle"></param>
        /// <param name="minimumFee"></param>
        /// <param name="changeSet"></param>
        /// <returns></returns>
        private IEnumerable<Transaction> ProcessPendingTasks(Block block, IOracleReader oracle, BigInteger minimumFee, StorageChangeSetContext changeSet)
        {
            // TODO: Reformulate this
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

        /// <summary>
        /// Get the current time for the task
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="block"></param>
        /// <returns></returns>
        /// <exception cref="ChainException"></exception>
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

        /// <summary>
        /// Process a pending task
        /// </summary>
        /// <param name="block"></param>
        /// <param name="oracle"></param>
        /// <param name="minimumFee"></param>
        /// <param name="changeSet"></param>
        /// <param name="task"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        private TaskResult ProcessPendingTask(Block block, IOracleReader oracle, BigInteger minimumFee,
                StorageChangeSetContext changeSet, IChainTask task, out Transaction transaction)
        {
            // TODO : Reformulate this
            transaction = null;

            BigInteger currentRun = GetTaskTimeFromBlock(task.Mode, block);
            var taskSheet = new TaskSheet(task.ID);
            var taskKey = taskSheet.GetTaskRunKey();

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

            TransactionResult txResult = null;
            if (Nexus.GetProtocolVersion(this.StorageCollection.ContractsStorage) >= 13)
            {
                txResult = ExecuteTransaction(-1, transaction, transaction.Script, block.Validator, block.Timestamp, changeSet,
                    block.Notify, oracle, task);
            }
            else
            {
                txResult = ExecuteTransaction(-1, transaction, transaction.Script, block.Validator, block.Timestamp, changeSet,
                    block.Notify, oracle, task);
            }
            
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
            var rootStorage = this.IsRoot ? storage : this.StorageCollection.MainStorage;

            if (block.Height > 1)
            {
                var prevBlock = GetBlockByHash(block.PreviousHash);

                if (prevBlock.Validator != block.Validator)
                {
                    block.Notify(new Event(EventKind.ValidatorSwitch, block.Validator, "block", Serialization.Serialize(prevBlock)));
                }
            }

            var tokenStorage = this.Name == DomainSettings.RootChainName ? storage : this.StorageCollection.MainStorage;
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

        /// <summary>
        /// Returns the last activity of an address in the chain.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public Timestamp GetLastActivityOfAddress(Address address)
        {
            var addressTxMap = new StorageMap(AddressTxHashMapTag, this.StorageCollection.AddressTransactionsStorage);
            var addressList = addressTxMap.Get<Address, StorageList>(address);
            var count = addressList.Count();
            if (count <= 0)
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

        /// <summary>
        /// Looks up the name in the current chain, and if not found, in the parent chain.
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="name"></param>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        public Address LookUpName(StorageContext storage, string name, Timestamp timestamp)
        {
            if (IsContractDeployed(storage, name))
            {
                return SmartContract.GetAddressFromContractName(name);
            }

            return this.Nexus.LookUpName(storage, name, timestamp);
        }

        /// <summary>
        /// Gets the name of an address, if it exists in the current chain, or in the parent chain.
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="address"></param>
        /// <param name="timestamp"></param>
        /// <returns></returns>
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

                var contract = this.GetContractByAddress(this.StorageCollection.ContractsStorage, address);
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

                    var org = Nexus.GetOrganizationByAddress(this.StorageCollection.OrganizationStorage, address);
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
