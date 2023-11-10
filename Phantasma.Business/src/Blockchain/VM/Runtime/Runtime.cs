using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Phantasma.Business.Blockchain.Archives;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.VM;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Enums;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Contract.Interop;
using Phantasma.Core.Domain.Contract.Interop.Structs;
using Phantasma.Core.Domain.Contract.Validator;
using Phantasma.Core.Domain.Contract.Validator.Structs;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Domain.Exceptions;
using Phantasma.Core.Domain.Execution;
using Phantasma.Core.Domain.Execution.Enums;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Oracle;
using Phantasma.Core.Domain.Oracle.Enums;
using Phantasma.Core.Domain.Serializer;
using Phantasma.Core.Domain.Structs;
using Phantasma.Core.Domain.Tasks;
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
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Storage.Context.Structs;
using Phantasma.Core.Types;
using Phantasma.Core.Types.Structs;
using Logger = Serilog.Log;

namespace Phantasma.Business.Blockchain.VM
{
    public partial class RuntimeVM : GasMachine, IRuntime
    {
        public Timestamp Time { get; private set; }
        public Transaction Transaction { get; private set; }
        public IChain Chain { get; private set; }
        public IChain ParentChain { get; private set; }
        public IOracleReader Oracle { get; private set; }
        public INexus Nexus => Chain.Nexus;

        private List<Event> _events = new List<Event>();
        public IEnumerable<Event> Events => _events;

        public BigInteger PaidGas { get; private set; }
        public BigInteger MaxGas { get; private set; }
        public BigInteger GasPrice { get; private set; }

        public int TransactionIndex { get; private set; }
        public Address GasTarget { get; private set; }
        public bool DelayPayment { get; private set; }

        public string ExceptionMessage { get; private set; }

        public bool IsError { get; private set; }

        public BigInteger MinimumFee;

        public Address Validator { get; private set; }

        public IChainTask CurrentTask { get; private set; }

        private readonly StorageChangeSetContext changeSet;

        private int _baseChangeSetCount;
        private BigInteger _randomSeed;

        public StorageContext RootStorage => IsRootChain() ? Storage : Nexus.RootStorage;

        private readonly RuntimeVM _parentMachine;

        public RuntimeVM(int index, byte[] script, uint offset, IChain chain, Address validator, Timestamp time,
            Transaction transaction, StorageChangeSetContext changeSet, IOracleReader oracle, IChainTask currentTask,
            bool delayPayment = false, string contextName = null, RuntimeVM parentMachine = null)
            : base(script, offset, contextName)
        {
            Core.Throw.IfNull(chain, nameof(chain));
            Core.Throw.IfNull(changeSet, nameof(changeSet));

            _baseChangeSetCount = (int)changeSet.Count();

            // NOTE: block and transaction can be null, required for Chain.InvokeContract
            //Throw.IfNull(block, nameof(block));
            //Throw.IfNull(transaction, nameof(transaction));

            TransactionIndex = index;
            GasPrice = 0;
            PaidGas = 0;
            GasTarget = Address.Null;
            CurrentTask = currentTask;
            DelayPayment = delayPayment;
            Validator = validator;
            _parentMachine = parentMachine;

            Time = time;
            Chain = chain;
            Transaction = transaction;
            Oracle = oracle;
            this.changeSet = changeSet;
            ExceptionMessage = null;
            IsError = false;

            if (Chain != null && !Chain.IsRoot)
            {
                var parentName = Chain.Nexus.GetParentChainByName(chain.Name);
                ParentChain = Chain.Nexus.GetChainByName(parentName);
            }
            else
            {
                ParentChain = null;
            }

            ProtocolVersion = Nexus.GetProtocolVersion(RootStorage);
            MinimumFee = GetGovernanceValue(GovernanceContract.GasMinimumFeeTag);

            MaxGas = 600; // a minimum amount required for allowing calls to Gas contract etc

            ExtCalls.RegisterWithRuntime(ProtocolVersion, this);
        }

        public bool IsTrigger => DelayPayment;

        IChain IRuntime.Chain => Chain;

        Transaction IRuntime.Transaction => Transaction;

        private Dictionary<string, int> _registedCallArgCounts = new Dictionary<string, int>();

        public StorageContext Storage => changeSet;

        public override string ToString()
        {
            return $"Runtime.Context={CurrentContext}";
        }

        internal void RegisterMethod(string name, int argCount, ExtcallDelegate handler)
        {
            _registedCallArgCounts[name] = argCount;
            _handlers[name] = handler;
        }

        internal int GetArgumentCountForMethod(string name)
        {
            if (_registedCallArgCounts.ContainsKey(name))
            {
                return _registedCallArgCounts[name];
            }

            return -1;
        }

        public IEnumerable<string> RegisteredMethodNames => _handlers.Keys;

        private Dictionary<string, ExtcallDelegate> _handlers =
            new Dictionary<string, ExtcallDelegate>(StringComparer.OrdinalIgnoreCase);

        public override ExecutionState ExecuteInterop(string method)
        {
            var result = base.ExecuteInterop(method);

            if (result == ExecutionState.Running)
            {
                if (_handlers.ContainsKey(method))
                {
                    var argCount = GetArgumentCountForMethod(method);
                    Expect(argCount >= 0, "invalid arg count for method: " + method);
                    this.ExpectStackSize(argCount);

                    return _handlers[method](this);
                }
            }

            return ExecutionState.Fault;
        }

        public VMObject CallInterop(string methodName, params object[] args)
        {
            ExpectNameLength(methodName, nameof(methodName));
            ExpectArgsLength(args, nameof(args));

            PushArgsIntoStack(args);
            if (ExecuteInterop(methodName) == ExecutionState.Running)
            {
                if (Stack.Count == 0)
                {
                    return null;
                }

                return Stack.Pop();
            }

            return null;
        }
        
        public void Notify(EventKind kind, Address address, byte[] bytes) =>
            Notify(kind, address, bytes, CurrentContext.Name, kind.ToString());

        public void Notify(EventKind kind, Address address, byte[] bytes, string name = "") =>
            Notify(kind, address, bytes, CurrentContext.Name, name);

        public void Notify(EventKind kind, Address address, byte[] bytes, string contract, string name)
        {
            ExpectEnumIsDefined(kind, nameof(kind));
            ExpectAddressSize(address, nameof(address));
            ExpectScriptLength(bytes, nameof(bytes));
            ExpectNameLength(contract, nameof(contract));

            var nativeContract = contract.FindNativeContractKindByName();

            switch (kind)
            {
                case EventKind.GasEscrow:
                {
                    Expect(nativeContract == NativeContractKind.Gas,
                        $"event kind only in {NativeContractKind.Gas} contract");

                    var gasInfo = Serialization.Unserialize<GasEventData>(bytes);
                    Expect(gasInfo.price >= MinimumFee, $"gas fee is too low {gasInfo.price} >= {MinimumFee}");
                    MaxGas = gasInfo.amount;
                    GasPrice = gasInfo.price;
                    GasTarget = gasInfo.address;
                    break;
                }

                case EventKind.GasPayment:
                {
                    Expect(nativeContract == NativeContractKind.Gas,
                        $"event kind only in {NativeContractKind.Gas} contract");

                    Expect(!address.IsNull, "invalid gas payment address");
                    var gasInfo = Serialization.Unserialize<GasEventData>(bytes);
                    PaidGas += gasInfo.amount;

                    break;
                }

                case EventKind.ValidatorSwitch:
                    Expect(nativeContract == NativeContractKind.Block,
                        $"event kind only in {NativeContractKind.Block} contract");
                    break;

                case EventKind.PollCreated:
                case EventKind.PollClosed:
                case EventKind.PollVote:
                    Expect(nativeContract == NativeContractKind.Consensus,
                        $"event kind only in {NativeContractKind.Consensus} contract");
                    break;

                case EventKind.ChainCreate:
                case EventKind.TokenCreate:
                case EventKind.FeedCreate:
                    Expect(IsRootChain(), $"event kind only in root chain");
                    break;

                case EventKind.FileCreate:
                case EventKind.FileDelete:
                    Expect(nativeContract == NativeContractKind.Storage,
                        $"event kind only in {NativeContractKind.Storage} contract");
                    break;

                case EventKind.ValidatorPropose:
                case EventKind.ValidatorElect:
                case EventKind.ValidatorRemove:
                    Expect(nativeContract == NativeContractKind.Validator,
                        $"event kind only in {NativeContractKind.Validator} contract");
                    break;

                case EventKind.ValueCreate:
                case EventKind.ValueUpdate:
                    Expect(nativeContract == NativeContractKind.Governance,
                        $"event kind only in {NativeContractKind.Governance} contract");
                    break;

                case EventKind.Inflation:

                    var inflationSymbol = Serialization.Unserialize<string>(bytes);

                    if (inflationSymbol == DomainSettings.StakingTokenSymbol)
                    {
                        Expect(nativeContract == NativeContractKind.Gas,
                            $"event kind only in {NativeContractKind.Gas} contract");
                    }
                    else
                    {
                        Expect(inflationSymbol != DomainSettings.FuelTokenSymbol,
                            $"{inflationSymbol} cannot have inflation event");
                    }

                    break;

                case EventKind.CrownRewards:
                    Expect(nativeContract == NativeContractKind.Gas,
                        $"event kind only in {NativeContractKind.Gas} contract");
                    break;
            }

            Event evt;
            if ((int)kind <= (int)EventKind.Custom)
            {
                evt = new Event(kind, address, contract, bytes, kind.ToString());
            }
            else
            {
                evt = new Event(kind, address, contract, bytes, name);
            }
            
            _events.Add(evt);
        }

        #region GAS

        public new ExecutionState ValidateOpcode(Opcode opcode)
        {
            ExpectEnumIsDefined(opcode, nameof(opcode));

            // required for allowing transactions to occur pre-minting of native token
            if (!HasGenesis)
            {
                return ExecutionState.Running;
            }

            return base.ValidateOpcode(opcode);
        }

        public override ExecutionState ConsumeGas(BigInteger gasCost)
        {
            if (_parentMachine != null)
            {
                if (_parentMachine.CurrentContext.Name == "gas")
                {
                    return ExecutionState.Running;
                }

                return _parentMachine.ConsumeGas(gasCost);
            }

            if (gasCost == 0)
            {
                return ExecutionState.Running;
            }

            if (gasCost < 0)
            {
                Core.Throw.If(gasCost < 0, "invalid gas amount");
            }

            // required for allowing transactions to occur pre-minting of native token
            if (!HasGenesis)
            {
                return ExecutionState.Running;
            }

            var result = base.ConsumeGas(gasCost);

            if (UsedGas > MaxGas && !DelayPayment)
            {
                throw new VMException(this, $"VM gas limit exceeded ({MaxGas})/({UsedGas})");
            }

            return result;
        }

        private bool EnforceGasSpending()
        {
            Address from, target;
            BigInteger gasPrice, gasLimit;

            if (!TransactionExtensions.ExtractGasDetailsFromScript(EntryScript, ProtocolVersion, out from, out target,
                    out gasPrice, out gasLimit))
            {
                return false;
            }

            // set the current context to entry context
            CurrentContext = FindContext(EntryContextName);

            // this is required, otherwise we get stuck in infinite loop
            DelayPayment = true;

            var allowance = this.CallNativeContext(NativeContractKind.Gas, nameof(GasContract.AllowedGas), from)
                .AsNumber();

            var _methodTableForGasExtraction = (Chain as Chain).GenerateMethodTable();

            IEnumerable<DisasmMethodCall> methods = new List<DisasmMethodCall>();
            try
            {
                methods = DisasmUtils.ExtractMethodCalls(EntryScript, ProtocolVersion, _methodTableForGasExtraction,
                    detectAndUseJumps: true);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            bool isWhitelisted = TransactionExtensions.IsWhitelisted(methods);
            bool hasSpendGas = TransactionExtensions.HasSpendGas(methods);

            if (allowance <= 0)
            {
                // if no allowance is given, create one
                this.CallNativeContext(NativeContractKind.Gas, nameof(GasContract.AllowGas), from, target, gasPrice,
                    gasLimit);
            }

            if (!hasSpendGas)
                this.CallNativeContext(NativeContractKind.Gas, nameof(GasContract.SpendGas), from);

            DelayPayment = false;

            return true;

            /*
            if (allowance >= UsedGas)
            {
                // if we have an allowance but no spend gas call was part of the script, call spend gas anyway
                this.CallNativeContext(NativeContractKind.Gas, nameof(GasContract.SpendGas));
            }
            else
            {
                // if we don't have an allowance, allow gas
                this.CallNativeContext(NativeContractKind.Gas, nameof(GasContract.AllowGas));

                // and call spend gas
                this.CallNativeContext(NativeContractKind.Gas, nameof(GasContract.SpendGas));
            }*/
        }

        #endregion

        // fetches a chain-governed value
        public BigInteger GetGovernanceValue(string name)
        {
            ExpectNameLength(name, nameof(name));

            var value = Nexus.GetGovernanceValue(RootStorage, name);
            return value;
        }

        public bool IsWitness(Address address)
        {
            ExpectAddressSize(address, nameof(address));

            if (address.IsInterop)
            {
                return false;
            }

            if (address.IsNull)
            {
                return false;
            }

            if (address == Chain.Address /*|| address == this.Address*/)
            {
                return false;
            }

            if (address.IsSystem)
            {
                foreach (var activeAddress in ActiveAddresses)
                {
                    if (activeAddress == address)
                    {
                        return true;
                    }
                }

                var org = Nexus.GetOrganizationByAddress(RootStorage, address);
                if (org != null)
                {
                    ConsumeGas(10000);
                    return org.IsWitness(Transaction);
                }
                else
                {
                    var owner = GetContractOwner(address);
                    if (!owner.IsNull && owner != address)
                    {
                        return IsWitness(owner);
                    }

                    return address == CurrentContext.Address;
                }
            }

            if (Transaction == null)
            {
                return false;
            }

            if (CurrentContext.Name.Equals(EVMContext.ContextName) && _evmWitnesses.Contains(address))
            {
                return true;
            }

            bool accountResult;

            if (address == Validator && TransactionIndex < 0)
            {
                accountResult = true;
            }
            else if (address.IsUser && HasGenesis && OptimizedHasAddressScript(RootStorage, address))
            {
                TriggerResult triggerResult;
                triggerResult = InvokeTriggerOnContract(false, address, ContractTrigger.OnWitness, address);

                if (triggerResult == TriggerResult.Missing)
                {
                    accountResult = Transaction.IsSignedBy(address);
                }
                else
                {
                    accountResult = triggerResult == TriggerResult.Success;
                }
            }
            else
            {
                if (Transaction != null)
                {
                    accountResult = Transaction.IsSignedBy(address);
                }
                else if (CurrentTask != null)
                {
                    accountResult = address == CurrentTask.Owner && CurrentContext.Name == CurrentTask.ContextName;
                }
                else
                {
                    throw new ChainException("IsWitness is being called from some weird context, possible bug?");
                }
            }

            return accountResult;
        }

        private bool OptimizedHasAddressScript(StorageContext context, Address address)
        {
            PrepareOptimizedScriptMapKey();
            var scriptMap = new StorageMap(_optimizedScriptMapKey, context);

            if (address.IsUser)
            {
                return scriptMap.ContainsKey(address);
            }

            return false;
        }

        public Block GetBlockByHash(Hash hash)
        {
            ExpectHashSize(hash, nameof(hash));
            return Chain.GetBlockByHash(hash);
        }

        public Block GetBlockByHeight(BigInteger height)
        {
            var hash = Chain.GetBlockHashAtHeight(height);
            return GetBlockByHash(hash);
        }

        public Transaction GetTransaction(Hash hash)
        {
            ExpectHashSize(hash, nameof(hash));
            return Chain.GetTransactionByHash(hash);
        }

        public Address LookUpName(string name)
        {
            ExpectNameLength(name, nameof(name));
            return Chain.LookUpName(RootStorage, name, Time);
        }

        public bool HasAddressScript(Address from)
        {
            ExpectAddressSize(from, nameof(from));
            return Nexus.HasAddressScript(RootStorage, from, Time);
        }

        public byte[] GetAddressScript(Address from)
        {
            ExpectAddressSize(from, nameof(from));
            return Nexus.LookUpAddressScript(RootStorage, from, Time);
        }

        public string GetAddressName(Address from)
        {
            ExpectAddressSize(from, nameof(from));
            return Chain.GetNameFromAddress(RootStorage, from, Time);
        }

        public Event[] GetTransactionEvents(Hash transactionHash)
        {
            ExpectHashSize(transactionHash, nameof(transactionHash));

            var blockHash = Chain.GetBlockHashOfTransaction(transactionHash);
            var block = Chain.GetBlockByHash(blockHash);
            Expect(block != null, "block not found for this transaction");
            return block.GetEventsForTransaction(transactionHash);
        }

        public Hash[] GetTransactionHashesForAddress(Address address)
        {
            ExpectAddressSize(address, nameof(address));
            return Chain.GetTransactionHashesForAddress(address);
        }

        public ValidatorEntry GetValidatorByIndex(int index)
        {
            return Nexus.GetValidatorByIndex(index, Time);
        }

        public ValidatorEntry[] GetValidators()
        {
            return Nexus.GetValidators(Time);
        }

        public bool IsPrimaryValidator(Address address)
        {
            ExpectAddressSize(address, nameof(address));
            return Nexus.IsPrimaryValidator(address, Time);
        }

        public bool IsSecondaryValidator(Address address)
        {
            ExpectAddressSize(address, nameof(address));
            return Nexus.IsSecondaryValidator(address, Time);
        }

        public int GetPrimaryValidatorCount()
        {
            return Nexus.GetPrimaryValidatorCount(Time);
        }

        public int GetSecondaryValidatorCount()
        {
            return Nexus.GetSecondaryValidatorCount(Time);
        }

        public bool IsKnownValidator(Address address)
        {
            ExpectAddressSize(address, nameof(address));
            return Nexus.IsKnownValidator(address, Time);
        }

        public bool IsStakeMaster(Address address)
        {
            ExpectAddressSize(address, nameof(address));
            return Nexus.IsStakeMaster(RootStorage, address, Time);
        }

        public BigInteger GetStake(Address address)
        {
            ExpectAddressSize(address, nameof(address));
            return Nexus.GetStakeFromAddress(RootStorage, address, Time);
        }

        public BigInteger GenerateUID()
        {
            return Chain.GenerateUID(Storage);
        }
        
        public void Log(string description)
        {
            Expect(!string.IsNullOrEmpty(description), "invalid log string");
            Expect(description.Length <= 256, "log string too large");
            ConsumeGas(1000);
            this.Notify(EventKind.Log, EntryAddress, description);
        }

        public void Throw(string description)
        {
            Expect(description.Length <= 256, "description string too large");
            throw new VMException(this, description);
        }

        public override string GetDumpFileName()
        {
            if (Transaction != null)
            {
                return Transaction.Hash.ToString() + ".txt";
            }

            return base.GetDumpFileName();
        }

        public override void DumpData(List<string> lines)
        {
            //TODO: valid range for lines
            lines.Add(VMException.Header("RUNTIME"));
            lines.Add("Time: " + Time.Value);
            lines.Add("Nexus: " + NexusName);
            lines.Add("Chain: " + Chain.Name);
            lines.Add("TxHash: " + (Transaction != null ? Transaction.Hash.ToString() : "None"));
            if (Transaction != null)
            {
                lines.Add("Payload: " + (Transaction.Payload != null && Transaction.Payload.Length > 0
                    ? Transaction.Payload.Encode()
                    : "None"));
                var bytes = Transaction.ToByteArray(true);
                lines.Add(VMException.Header("RAWTX"));
                lines.Add(bytes.Encode());
            }
        }

        public Timestamp GetGenesisTime()
        {
            if (HasGenesis)
            {
                var genesisBlock = Nexus.RootChain.GetBlockByHash(GenesisHash);
                return genesisBlock.Timestamp;
            }

            return Time;
        }
        
        /*
        #region ALLOWANCE
        public struct AllowanceEntry
        {
            public readonly string Symbol;
            public readonly BigInteger Amount;

            public AllowanceEntry(string symbol, BigInteger amount)
            {
                Symbol = symbol;
                Amount = amount;
            }
        }

        // TODO make this a Dictionary<Address, List<AllowanceEntry>> in order to support multiple allowances per address at once
        private Dictionary<Address, AllowanceEntry> _allowances = new Dictionary<Address, AllowanceEntry>();

        public void AddAllowance(Address destination, string symbol, BigInteger amount)
        {
            ExpectAddressSize(destination, nameof(destination));
            ExpectNameLength(symbol, nameof(symbol));

            if (amount < 0)
            {
                throw new ChainException("Invalid negative allowance");
            }

            if (_parentMachine != null)
            {
                _parentMachine.AddAllowance(destination, symbol, amount);
                return;
            }

            if (_allowances.ContainsKey(destination))
            {
                var prev = _allowances[destination];
                if (prev.Symbol != symbol)
                {
                    throw new ChainException($"multiple allowances not allowed yet: {prev.Symbol} + {symbol}");
                }

                _allowances[destination] = new AllowanceEntry(symbol, amount + prev.Amount);

            }
            else
            {
                _allowances[destination] = new AllowanceEntry(symbol, amount);
            }
        }

        public void RemoveAllowance(Address destination, string symbol)
        {
            ExpectAddressSize(destination, nameof(destination));
            ExpectNameLength(symbol, nameof(symbol));

            if (_parentMachine != null)
            {
                _parentMachine.RemoveAllowance(destination, symbol);
                return;
            }

            if (_allowances.ContainsKey(destination))
            {
                var prev = _allowances[destination];
                if (prev.Symbol == symbol)
                {
                    _allowances.Remove(destination);
                }
            }
        }

        public bool SubtractAllowance(Address destination, string symbol, BigInteger amount)
        {
            ExpectAddressSize(destination, nameof(destination));
            ExpectNameLength(symbol, nameof(symbol));

            if (amount < 0)
            {
                return false;
            }

            if (_parentMachine != null)
            {
                return _parentMachine.SubtractAllowance(destination, symbol, amount);
            }

            if (_allowances.ContainsKey(destination))
            {
                var prev = _allowances[destination];
                if (prev.Symbol != symbol)
                {
                    return false;
                }

                if (prev.Amount < amount)
                {
                    return false;
                }

                if (prev.Amount == amount)
                {
                    _allowances.Remove(destination);
                }
                else
                {
                    _allowances[destination] = new AllowanceEntry(symbol, prev.Amount - amount);
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion
        */

        public bool HasGenesis =>
            Nexus.HasGenesis(); // TODO cache this, as it does not change during a Runtime execution

        private HashSet<Address> _evmWitnesses = new HashSet<Address>();

        public void EVM_Block(Address source, Action callback)
        {
            Expect(source.IsUser, "only user address can be EVM witness");
            Expect(CurrentContext.Name == EVMContext.ContextName, "must be running in EVM context");
            _evmWitnesses.Add(source);

            callback();

            _evmWitnesses.Remove(source);
        }

        public string NexusName => Nexus.Name;
        public uint ProtocolVersion { get; private set; }
        public Hash GenesisHash => Nexus.GetGenesisHash(RootStorage);
    }
}
