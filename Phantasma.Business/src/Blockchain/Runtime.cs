using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Storage;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.VM;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;
using Logger = Serilog.Log;

namespace Phantasma.Business.Blockchain
{
    public class RuntimeVM : GasMachine, IRuntime
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

        public StorageContext RootStorage => this.IsRootChain() ? this.Storage : Nexus.RootStorage;

        private readonly RuntimeVM _parentMachine;

        public RuntimeVM(int index, byte[] script, uint offset, IChain chain, Address validator, Timestamp time,
                Transaction transaction, StorageChangeSetContext changeSet, IOracleReader oracle, IChainTask currentTask,
                bool delayPayment = false, string contextName = null, RuntimeVM parentMachine = null)
            : base(script, offset, contextName)
        {
            Core.Throw.IfNull(chain, nameof(chain));
            Core.Throw.IfNull(changeSet, nameof(changeSet));

            _baseChangeSetCount = changeSet.Count();

            // NOTE: block and transaction can be null, required for Chain.InvokeContract
            //Throw.IfNull(block, nameof(block));
            //Throw.IfNull(transaction, nameof(transaction));

            this.TransactionIndex = index;
            this.GasPrice = 0;
            this.PaidGas = 0;
            this.GasTarget = Address.Null;
            this.CurrentTask = currentTask;
            this.DelayPayment = delayPayment;
            this.Validator = validator;
            this._parentMachine = parentMachine;

            this.Time = time;
            this.Chain = chain;
            this.Transaction = transaction;
            this.Oracle = oracle;
            this.changeSet = changeSet;
            this.ExceptionMessage = null;
            this.IsError = false;

            if (this.Chain != null && !Chain.IsRoot)
            {
                var parentName = this.Chain.Nexus.GetParentChainByName(chain.Name);
                this.ParentChain = this.Chain.Nexus.GetChainByName(parentName);
            }
            else
            {
                this.ParentChain = null;
            }

            this.ProtocolVersion = Nexus.GetProtocolVersion(this.RootStorage);
            this.MinimumFee = GetGovernanceValue(GovernanceContract.GasMinimumFeeTag);

            this.MaxGas = 600;  // a minimum amount required for allowing calls to Gas contract etc

            ExtCalls.RegisterWithRuntime(this);
        }

        public bool IsTrigger => DelayPayment;

        IChain IRuntime.Chain => this.Chain;

        Transaction IRuntime.Transaction => this.Transaction;

        private Dictionary<string, int> _registedCallArgCounts = new Dictionary<string, int>();

        public StorageContext Storage => this.changeSet;

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

        private Dictionary<string, ExtcallDelegate> _handlers = new Dictionary<string, ExtcallDelegate>(StringComparer.OrdinalIgnoreCase);

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

        public override ExecutionState Execute()
        {
            ExecutionState result = ExecutionState.Fault;
            try
            {
                result = base.Execute();
            }
            catch (Exception ex)
            {
                if (DelayPayment)
                {
                    throw ex;
                }

                var usedGasUntilError = this.UsedGas;
                this.ExceptionMessage = ex.Message;
                this.IsError = true;

                Logger.Error($"Transaction {Transaction?.Hash} failed with {ex.Message}, gas used: {UsedGas}");

                if (!this.IsReadOnlyMode())
                {
                    this.Notify(EventKind.ExecutionFailure, CurrentContext.Address, this.ExceptionMessage);

                    if (!EnforceGasSpending())
                    {
                        throw ex; // should never happen
                    }

                    this.UsedGas = usedGasUntilError;
                }
            }

            if (this.IsReadOnlyMode())
            {
                if (changeSet.Count() != _baseChangeSetCount)
                {
                    throw new VMException(this, "VM changeset modified in read-only mode");
                }
            }
            else if (!IsError && PaidGas < UsedGas && !DelayPayment && Nexus.HasGenesis())
            {
                if (!EnforceGasSpending())
                {
                    throw new VMException(this, "Could not enforce spendGas"); // should never happen
                }
            }

            return result;
        }

        private bool EnforceGasSpending()
        {
            Address from, target;
            BigInteger gasPrice, gasLimit;

            if (!TransactionExtensions.ExtractGasDetailsFromScript(this.EntryScript, out from, out target, out gasPrice, out gasLimit))
            {
                return false;
            }

            // set the current context to entry context
            this.CurrentContext = FindContext(VirtualMachine.EntryContextName);

            // this is required, otherwise we get stuck in infinite loop
            this.DelayPayment = true;

            var allowance = this.CallNativeContext(NativeContractKind.Gas, nameof(GasContract.AllowedGas), from).AsNumber();
            if (allowance <= 0)
            {
                // if no allowance is given, create one
                this.CallNativeContext(NativeContractKind.Gas, nameof(GasContract.AllowGas), from, target, gasPrice, gasLimit);
            }

            this.CallNativeContext(NativeContractKind.Gas, nameof(GasContract.SpendGas), from);

            this.DelayPayment = false;

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

        public override ExecutionContext LoadContext(string contextName)
        {
            ExpectNameLength(contextName, nameof(contextName));

            if (contextName.Contains("#"))
            {
                var split = contextName.Split('#');
                if (split.Length != 2)
                {
                    return null;
                }

                var symbol = split[0];
                BigInteger seriesID;

                if (!BigInteger.TryParse(split[1], out seriesID))
                {
                    return null;
                }

                var series = Nexus.GetTokenSeries(this.RootStorage, symbol, seriesID);
                if (series == null)
                {
                    throw new VMException(this, $"Could not find {symbol} series #{seriesID}");
                }

                var contract = new CustomContract(contextName, series.Script, series.ABI);
                var context = new ChainExecutionContext(contract);
                return context;
            }
            else
            {
                var contract = this.Chain.GetContractByName(this.Storage, contextName);
                if (contract != null)
                {
                    return Chain.GetContractContext(this.changeSet, contract);
                }

                return null;
            }
        }

        private void PushArgsIntoStack(object[] args)
        {
            for (int i = args.Length - 1; i >= 0; i--)
            {
                var obj = VMObject.FromObject(args[i]);
                this.Stack.Push(obj);
            }
        }

        public VMObject CallContext(string contextName, uint jumpOffset, string methodName, params object[] args)
        {
            ExpectNameLength(contextName, nameof(contextName));
            ExpectNameLength(methodName, nameof(methodName));
            ExpectArgsLength(args, nameof(args));

            var tempContext = PreviousContext;
            PreviousContext = CurrentContext;

            var context = LoadContext(contextName);
            Expect(context != null, "could not call context: " + contextName);

            PushArgsIntoStack(args);

            Stack.Push(VMObject.FromObject(methodName));

            SetCurrentContext(context);

            PushFrame(context, jumpOffset, VirtualMachine.DefaultRegisterCount);

            ActiveAddresses.Push(context.Address);

            var temp = context.Execute(this.CurrentFrame, this.Stack);
            Expect(temp == ExecutionState.Halt, "expected call success");

            PopFrame();

            var temp2 = ActiveAddresses.Pop();
            if (temp2 != context.Address)
            {
                throw new VMException(this, "runtimeVM implementation bug detected: address stack");
            }

            PreviousContext = tempContext;

            if (Stack.Count > 0)
            {
                var result = Stack.Pop();
                return result;
            }
            else
            {
                return new VMObject();
            }
        }

        public void Notify(EventKind kind, Address address, byte[] bytes) => Notify(kind, address, bytes, CurrentContext.Name);

        public void Notify(EventKind kind, Address address, byte[] bytes, string contract)
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
                        Expect(nativeContract == NativeContractKind.Gas, $"event kind only in {NativeContractKind.Gas} contract");

                        var gasInfo = Serialization.Unserialize<GasEventData>(bytes);
                        Expect(gasInfo.price >= this.MinimumFee, $"gas fee is too low {gasInfo.price} >= {this.MinimumFee}");
                        MaxGas = gasInfo.amount;
                        GasPrice = gasInfo.price;
                        GasTarget = gasInfo.address;
                        break;
                    }

                case EventKind.GasPayment:
                    {
                        Expect(nativeContract == NativeContractKind.Gas, $"event kind only in {NativeContractKind.Gas} contract");

                        Expect(!address.IsNull, "invalid gas payment address");
                        var gasInfo = Serialization.Unserialize<GasEventData>(bytes);
                        PaidGas += gasInfo.amount;

                        break;
                    }

                case EventKind.ValidatorSwitch:
                    Expect(nativeContract == NativeContractKind.Block, $"event kind only in {NativeContractKind.Block} contract");
                    break;

                case EventKind.PollCreated:
                case EventKind.PollClosed:
                case EventKind.PollVote:
                    Expect(nativeContract == NativeContractKind.Consensus, $"event kind only in {NativeContractKind.Consensus} contract");
                    break;

                case EventKind.ChainCreate:
                case EventKind.TokenCreate:
                case EventKind.FeedCreate:
                    Expect(this.IsRootChain(), $"event kind only in root chain");
                    break;

                case EventKind.FileCreate:
                case EventKind.FileDelete:
                    Expect(nativeContract == NativeContractKind.Storage, $"event kind only in {NativeContractKind.Storage} contract");
                    break;

                case EventKind.ValidatorPropose:
                case EventKind.ValidatorElect:
                case EventKind.ValidatorRemove:
                    Expect(nativeContract == NativeContractKind.Validator, $"event kind only in {NativeContractKind.Validator} contract");
                    break;

                case EventKind.ValueCreate:
                case EventKind.ValueUpdate:
                    Expect(nativeContract == NativeContractKind.Governance, $"event kind only in {NativeContractKind.Governance} contract");
                    break;

                case EventKind.Inflation:

                    var inflationSymbol = Serialization.Unserialize<string>(bytes);

                    if (inflationSymbol == DomainSettings.StakingTokenSymbol)
                    {
                        Expect(nativeContract == NativeContractKind.Gas, $"event kind only in {NativeContractKind.Gas} contract");
                    }
                    else
                    {
                        Expect(inflationSymbol != DomainSettings.FuelTokenSymbol, $"{inflationSymbol} cannot have inflation event");
                    }

                    break;

                case EventKind.CrownRewards:
                    Expect(nativeContract == NativeContractKind.Gas, $"event kind only in {NativeContractKind.Gas} contract");
                    break;
            }

            var evt = new Event(kind, address, contract, bytes);
            _events.Add(evt);
        }

        public bool IsSystemToken(string symbol)
        {
            return Nexus.IsSystemToken(symbol);
        }

        public bool IsMintingAddress(Address address, string symbol)
        {
            ExpectAddressSize(address, nameof(address));
            ExpectNameLength(symbol, nameof(symbol));

            if (TokenExists(symbol))
            {
                var info = GetToken(symbol);

                if (address == info.Owner)
                {
                    return true;
                }
            }

            return false;
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

            if (UsedGas > this.MaxGas && !DelayPayment)
            {
                throw new VMException(this, $"VM gas limit exceeded ({this.MaxGas})/({UsedGas})");
            }

            return result;
        }
        #endregion

        #region ORACLES
        // returns value in FIAT token
        public BigInteger GetTokenPrice(string symbol)
        {
            ExpectNameLength(symbol, nameof(symbol));

            if (symbol == DomainSettings.FiatTokenSymbol)
            {
                return UnitConversion.GetUnitValue(DomainSettings.FiatTokenDecimals);
            }

            Core.Throw.If(!Nexus.TokenExists(RootStorage, symbol), "cannot read price for invalid token");
            var token = GetToken(symbol);

            Core.Throw.If(Oracle == null, "cannot read price from null oracle");

            var value = Oracle.ReadPrice(this.Time, symbol);

            Expect(value >= 0, "token price not available for " + symbol);

            return value;
        }
        #endregion

        #region RANDOM NUMBERS
        public static readonly uint RND_A = 16807;
        public static readonly uint RND_M = 2147483647;


        public BigInteger GenerateRandomNumber()
        {
            // Consider implenting this -> var x = RandomNumberGenerator.Create();
            if (_randomSeed == 0 && Transaction != null)
            {
                SetRandomSeed(Transaction.Hash);
            }

            _randomSeed = ((RND_A * _randomSeed) % RND_M);
            return _randomSeed;
        }


        public void SetRandomSeed(BigInteger seed)
        {
            // calculates first initial pseudo random number seed
            byte[] bytes = seed.ToSignedByteArray();


            for (int i = 0; i < EntryScript.Length; i++)
            {
                var index = i % bytes.Length;
                bytes[index] ^= EntryScript[i];
            }

            var time = System.BitConverter.GetBytes(Time.Value);

            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] ^= time[i % time.Length];
            }

            _randomSeed = new BigInteger(bytes, true);
        }
        #endregion

        // fetches a chain-governed value
        public BigInteger GetGovernanceValue(string name)
        {
            ExpectNameLength(name, nameof(name));

            var value = Nexus.GetGovernanceValue(RootStorage, name);
            return value;
        }

        private HashSet<string> _triggerGuards = new HashSet<string>();

        internal void ValidateTriggerGuard(string triggerName)
        {
            ExpectNameLength(triggerName, nameof(triggerName));

            if (_triggerGuards.Contains(triggerName))
            {
                throw new ChainException("trigger loop detected: " + triggerName);
            }

            _triggerGuards.Add(triggerName);
        }

        #region TRIGGERS
        public TriggerResult InvokeTriggerOnAccount(bool allowThrow, Address address, AccountTrigger trigger, params object[] args)
        {
            ExpectAddressSize(address, nameof(address));
            ExpectEnumIsDefined(trigger, nameof(trigger));
            ExpectArgsLength(args, nameof(args));

            if (address.IsNull)
            {
                return TriggerResult.Failure;
            }

            if (!IsTrigger)
            {
                _triggerGuards.Clear();
            }

            var triggerName = trigger.ToString();

            if (address.IsUser)
            {

                //var accountScript = Nexus.LookUpAddressScript(RootStorage, address);
                var accountABI = OptimizedAddressABILookup(address);
                var accountScript = accountABI != null ? OptimizedAddressScriptLookup(address) : null;

                //Expect(accountScript.SequenceEqual(accountScript2), "different account scripts");

                ValidateTriggerGuard($"{address.Text}.{triggerName}");
                return this.InvokeTrigger(allowThrow, accountScript, address.Text, accountABI, triggerName, args);
            }

            if (address.IsSystem)
            {
                var contract = Chain.GetContractByAddress(this.Storage, address);
                if (contract != null)
                {
                    if (contract.ABI.HasMethod(triggerName))
                    {
                        var customContract = contract as CustomContract;
                        if (customContract != null)
                        {
                            ValidateTriggerGuard($"{contract.Name}.{triggerName}");
                            return InvokeTrigger(allowThrow, customContract.Script, contract.Name, contract.ABI, triggerName, args);
                        }

                        var native = contract as NativeContract;
                        if (native != null)
                        {
                            ValidateTriggerGuard($"{contract.Name}.{triggerName}");

                            try
                            {
                                this.CallNativeContext(native.Kind, triggerName, args);
                                return TriggerResult.Success;
                            }
                            catch
                            {
                                if (allowThrow)
                                {
                                    // just throw to preserve original stack trace
                                    throw;
                                }

                                return TriggerResult.Failure;
                            }
                        }

                    }
                }
                else
                {
                    return TriggerResult.Missing;
                }
            }

            return TriggerResult.Missing;
        }

        private static byte[] _optimizedScriptMapKey = null;
        private static byte[] _optimizedABIMapKey = null;

        private static void PrepareOptimizedScriptMapKey()
        {
            if (_optimizedScriptMapKey == null)
            {
                var accountContractName = NativeContractKind.Account.GetContractName();
                _optimizedScriptMapKey = Encoding.UTF8.GetBytes($".{accountContractName}._scriptMap");
            }
        }

        private byte[] OptimizedAddressScriptLookup(Address target)
        {
            PrepareOptimizedScriptMapKey();

            var scriptMap = new StorageMap(_optimizedScriptMapKey, RootStorage);

            if (scriptMap.ContainsKey(target))
                return scriptMap.Get<Address, byte[]>(target);
            else
                return new byte[0];

        }

        private ContractInterface OptimizedAddressABILookup(Address target)
        {
            if (_optimizedABIMapKey == null)
            {
                var accountContractName = NativeContractKind.Account.GetContractName();
                _optimizedABIMapKey = Encoding.UTF8.GetBytes($".{accountContractName}._abiMap");
            }

            var abiMap = new StorageMap(_optimizedABIMapKey, RootStorage);

            if (abiMap.ContainsKey(target))
            {
                var bytes = abiMap.Get<Address, byte[]>(target);
                return ContractInterface.FromBytes(bytes);
            }
            else
                return null;

        }

        public TriggerResult InvokeTriggerOnToken(bool allowThrow, IToken token, TokenTrigger trigger, params object[] args)
        {
            ExpectArgsLength(args, nameof(args));
            ExpectValidToken(token);
            ExpectEnumIsDefined(trigger, nameof(trigger));
            ExpectArgsLength(args, nameof(args));

            return InvokeTrigger(allowThrow, token.Script, token.Symbol, token.ABI, trigger.ToString(), args);
        }

        public TriggerResult InvokeTrigger(bool allowThrow, byte[] script, string contextName, ContractInterface abi, string triggerName, params object[] args)
        {
            ExpectNameLength(contextName, nameof(contextName));
            ExpectNameLength(triggerName, nameof(triggerName));
            ExpectArgsLength(args, nameof(args));

            if (script == null || script.Length == 0 || abi == null)
            {
                return TriggerResult.Missing;
            }

            var method = abi.FindMethod(triggerName);
            if (method == null || method.offset < 0)
            {
                return TriggerResult.Missing;
            }

            var runtime = new RuntimeVM(-1, script, (uint)method.offset, this.Chain, this.Validator, this.Time, this.Transaction, this.changeSet, this.Oracle, ChainTask.Null, true, contextName, this);

            for (int i = args.Length - 1; i >= 0; i--)
            {
                var obj = VMObject.FromObject(args[i]);
                runtime.Stack.Push(obj);
            }

            ExecutionState state;
            try {
                state = runtime.Execute();
                // TODO catch VM exceptions?
            }
            catch (VMException)
            {
                if (allowThrow)
                {
                    throw;
                }

                state = ExecutionState.Fault;
            }

            if (state == ExecutionState.Halt)
            {
                // propagate events to the other runtime
                foreach (var evt in runtime.Events)
                {
                    this.Notify(evt.Kind, evt.Address, evt.Data, evt.Contract);
                }

                return TriggerResult.Success;
            }
            else
            {
                if (allowThrow)
                {
                    var vmException = runtime.Stack.Pop().AsString();
                    throw new Exception($"{triggerName} trigger failed: {vmException}");
                }

                return TriggerResult.Failure;
            }
        }
        #endregion

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

            if (address == this.Chain.Address /*|| address == this.Address*/)
            {
                return false;
            }

            if (address.IsSystem)
            {
                foreach (var activeAddress in this.ActiveAddresses)
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

            bool accountResult;

            if (address == Validator && TransactionIndex < 0)
            {
                accountResult = true;
            }
            else if (address.IsUser && HasGenesis && OptimizedHasAddressScript(RootStorage, address))
            {
                TriggerResult triggerResult;
                triggerResult = InvokeTriggerOnAccount(false, address, AccountTrigger.OnWitness, address);

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
                else
                if (CurrentTask != null)
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

        public IContract GetContract(string name)
        {
            ExpectNameLength(name, nameof(name));

            throw new NotImplementedException();
        }

        public bool TokenExists(string symbol)
        {
            ExpectNameLength(symbol, nameof(symbol));
            return Nexus.TokenExists(RootStorage, symbol);
        }

        public bool NFTExists(string symbol, BigInteger tokenID)
        {
            ExpectNameLength(symbol, nameof(symbol));

            return Nexus.HasNFT(RootStorage, symbol, tokenID);
        }

        public bool TokenExists(string symbol, string platform)
        {
            ExpectNameLength(symbol, nameof(symbol));
            ExpectNameLength(platform, nameof(platform));

            return Nexus.TokenExistsOnPlatform(symbol, platform, RootStorage);
        }

        public bool FeedExists(string name)
        {
            ExpectNameLength(name, nameof(name));
            return Nexus.FeedExists(RootStorage, name);
        }

        public bool PlatformExists(string name)
        {
            ExpectNameLength(name, nameof(name));
            return Nexus.PlatformExists(RootStorage, name);
        }

        public bool ContractExists(string name)
        {
            ExpectNameLength(name, nameof(name));
            return Nexus.ContractExists(RootStorage, name);
        }

        public bool ContractDeployed(string name)
        {
            ExpectNameLength(name, nameof(name));
            return Chain.IsContractDeployed(Storage, name);
        }

        public bool ArchiveExists(Hash hash)
        {
            ExpectHashSize(hash, nameof(hash));
            return Nexus.ArchiveExists(RootStorage, hash);
        }

        public IArchive GetArchive(Hash hash)
        {
            ExpectHashSize(hash, nameof(hash));
            return Nexus.GetArchive(RootStorage, hash);
        }

        public bool DeleteArchive(Hash hash)
        {
            ExpectHashSize(hash, nameof(hash));

            var archive = Nexus.GetArchive(RootStorage, hash);
            if (archive == null)
            {
                return false;
            }
            return Nexus.DeleteArchive(RootStorage, archive);
        }

        public bool AddOwnerToArchive(Hash hash, Address address)
        {
            ExpectHashSize(hash, nameof(hash));
            ExpectAddressSize(address, nameof(address));

            var archive = Nexus.GetArchive(RootStorage, hash);
            if (archive == null)
            {
                return false;
            }

            Nexus.AddOwnerToArchive(RootStorage, archive, address);

            this.Notify(EventKind.OwnerAdded, address, hash);

            return true;
        }

        public bool RemoveOwnerFromArchive(Hash hash, Address address)
        {
            ExpectHashSize(hash, nameof(hash));
            ExpectAddressSize(address, nameof(address));

            var archive = Nexus.GetArchive(this.RootStorage, hash);
            if (archive == null)
            {
                return false;
            }

            Nexus.RemoveOwnerFromArchive(this.RootStorage, archive, address);

            if (archive.OwnerCount == 0)
            {
                this.Notify(EventKind.FileDelete, address, hash);
            }
            else
            {
                this.Notify(EventKind.OwnerRemoved, address, hash);
            }

            return true;
        }

        public bool ChainExists(string name)
        {
            ExpectNameLength(name, nameof(name));
            return Nexus.ChainExists(RootStorage, name);
        }

        public int GetIndexOfChain(string name)
        {
            ExpectNameLength(name, nameof(name));
            return Nexus.GetIndexOfChain(name);
        }

        public IChain GetChainParent(string name)
        {
            ExpectNameLength(name, nameof(name));
            var parentName = Nexus.GetParentChainByName(name);
            return GetChainByName(parentName);
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
            return this.Chain.GenerateUID(Storage);
        }

        public BigInteger GetBalance(string symbol, Address address)
        {
            ExpectNameLength(symbol, nameof(symbol));
            ExpectAddressSize(address, nameof(address));
            ExpectTokenExists(symbol);
            var token = GetToken(symbol);
            return Chain.GetTokenBalance(Storage, token, address);
        }

        public BigInteger[] GetOwnerships(string symbol, Address address)
        {
            ExpectNameLength(symbol, nameof(symbol));
            ExpectAddressSize(address, nameof(address));
            ExpectTokenExists(symbol);
            return Chain.GetOwnedTokens(Storage, symbol, address);
        }

        public BigInteger GetTokenSupply(string symbol)
        {
            ExpectNameLength(symbol, nameof(symbol));
            ExpectTokenExists(symbol);
            return Chain.GetTokenSupply(Storage, symbol);
        }

        /*
        public void SetPlatformTokenHash(string symbol, string platform, Hash hash)
        {
            ExpectNameLength(symbol, nameof(symbol));
            ExpectNameLength(platform, nameof(platform));
            ExpectHashSize(hash, nameof(hash));

            Expect(this.IsRootChain(), "must be root chain");

            Expect(IsWitness(GenesisAddress), "invalid witness, must be genesis");

            Expect(platform != DomainSettings.PlatformName, "external token chain required");
            Expect(hash != Hash.Null, "hash cannot be null");

            var pow = Transaction.Hash.GetDifficulty();
            Expect(pow >= (int)ProofOfWork.Minimal, "expected proof of work");

            Expect(PlatformExists(platform), "platform not found");

            Expect(!string.IsNullOrEmpty(symbol), "token symbol required");
            Expect(ValidationUtils.IsValidTicker(symbol), "invalid symbol");
            //Expect(!TokenExists(symbol, platform), $"token {symbol}/{platform} already exists");

            Expect(!string.IsNullOrEmpty(platform), "chain name required");

            Nexus.SetPlatformTokenHash(symbol, platform, hash, RootStorage);
        }*/

        public void CreateToken(Address owner, string symbol, string name, BigInteger maxSupply, int decimals, TokenFlags flags, byte[] script, ContractInterface abi)
        {
            ExpectAddressSize(owner, nameof(owner));
            ExpectNameLength(symbol, nameof(symbol));
            ExpectNameLength(name, nameof(name));
            ExpectScriptLength(script, nameof(script));

            Expect(this.IsRootChain(), "must be root chain");

            Expect(owner.IsUser, "owner address must be user address");

            Expect(IsStakeMaster(owner), "needs to be master");
            Expect(IsWitness(owner), "invalid witness");

            var pow = Transaction.Hash.GetDifficulty();
            Expect(pow >= (int)ProofOfWork.Minimal, "expected proof of work");

            Expect(!string.IsNullOrEmpty(symbol), "token symbol required");
            Expect(!string.IsNullOrEmpty(name), "token name required");

            Expect(ValidationUtils.IsValidTicker(symbol), "invalid symbol");
            Expect(!TokenExists(symbol), "token already exists");

            Expect(maxSupply >= 0, "token supply cant be negative");
            Expect(decimals >= 0, "token decimals cant be negative");
            Expect(decimals <= DomainSettings.MAX_TOKEN_DECIMALS, $"token decimals cant exceed {DomainSettings.MAX_TOKEN_DECIMALS}");

            if (symbol == DomainSettings.FuelTokenSymbol)
            {
                Expect(flags.HasFlag(TokenFlags.Fuel), "token should be native");
            }
            else
            {
                Expect(!flags.HasFlag(TokenFlags.Fuel), "token can't be native");
            }

            if (symbol == DomainSettings.StakingTokenSymbol)
            {
                Expect(flags.HasFlag(TokenFlags.Stakable), "token should be stakable");
            }

            if (symbol == DomainSettings.FiatTokenSymbol)
            {
                Expect(flags.HasFlag(TokenFlags.Fiat), "token should be fiat");
            }

            if (!flags.HasFlag(TokenFlags.Fungible))
            {
                Expect(!flags.HasFlag(TokenFlags.Divisible), "non-fungible token must be indivisible");
            }

            if (flags.HasFlag(TokenFlags.Divisible))
            {
                Expect(decimals > 0, "divisible token must have decimals");
            }
            else
            {
                Expect(decimals == 0, "indivisible token can't have decimals");
            }

            var token = Nexus.CreateToken(RootStorage, symbol, name, owner, maxSupply, decimals, flags, script, abi);

            var constructor = abi.FindMethod(SmartContract.ConstructorName);

            if (constructor != null)
            {
                this.CallContext(symbol, constructor, owner);
            }

            var rootChain = (Chain)this.GetRootChain();
            var currentOwner = owner;
            TokenUtils.FetchProperty(RootStorage, rootChain, "getOwner", script, abi, (prop, value) =>
            {
                currentOwner = value.AsAddress();
            });

            Expect(!currentOwner.IsNull, "missing or invalid token owner");
            Expect(currentOwner == owner, "token owner constructor failure");

            var fuelCost = GetGovernanceValue(DomainSettings.FuelPerTokenDeployTag);
            // governance value is in usd fiat, here convert from fiat to fuel amount
            fuelCost = this.GetTokenQuote(DomainSettings.FiatTokenSymbol, DomainSettings.FuelTokenSymbol, fuelCost);

            var fuelBalance = this.GetBalance(DomainSettings.FuelTokenSymbol, owner);
            Expect(fuelBalance >= fuelCost, $"{UnitConversion.ToDecimal(fuelCost, DomainSettings.FuelTokenDecimals)} {DomainSettings.FuelTokenSymbol} required to create a token but {owner} has only {UnitConversion.ToDecimal(fuelBalance, DomainSettings.FuelTokenDecimals)} {DomainSettings.FuelTokenSymbol}");

            // burn the "cost" tokens
            BurnTokens(DomainSettings.FuelTokenSymbol, owner, fuelCost);

            this.Notify(EventKind.TokenCreate, owner, symbol);
        }

        public void CreateChain(Address creator, string organization, string name, string parentChain)
        {
            ExpectAddressSize(creator, nameof(creator));
            ExpectNameLength(organization, nameof(organization));
            ExpectNameLength(name, nameof(name));
            ExpectNameLength(parentChain, nameof(parentChain));

            Expect(this.IsRootChain(), "must be root chain");
            
            var pow = Transaction.Hash.GetDifficulty();
            Expect(pow >= (int)ProofOfWork.Minimal, "expected proof of work");

            Expect(!string.IsNullOrEmpty(name), "name required");
            Expect(!string.IsNullOrEmpty(parentChain), "parent chain required");

            Expect(OrganizationExists(organization), "invalid organization");
            var org = GetOrganization(organization);
            Expect(org.IsMember(creator), "creator does not belong to organization");

            Expect(creator.IsUser, "owner address must be user address");
            Expect(IsStakeMaster(creator), "needs to be master");
            Expect(IsWitness(creator), "invalid witness");

            name = name.ToLowerInvariant();

            Expect(!name.Equals(parentChain, StringComparison.OrdinalIgnoreCase), "same name as parent");

            Nexus.CreateChain(RootStorage, organization, name, parentChain);
            this.Notify(EventKind.ChainCreate, creator, name);
        }

        public void CreateFeed(Address owner, string name, FeedMode mode)
        {
            ExpectAddressSize(owner, nameof(owner));
            ExpectNameLength(name, nameof(name));
            ExpectEnumIsDefined(mode, nameof(mode));

            Expect(this.IsRootChain(), "must be root chain");

            var pow = Transaction.Hash.GetDifficulty();
            Expect(pow >= (int)ProofOfWork.Minimal, "expected proof of work");

            Expect(!string.IsNullOrEmpty(name), "name required");

            Expect(owner.IsUser, "owner address must be user address");
            Expect(IsStakeMaster(owner), "needs to be master");
            Expect(IsWitness(owner), "invalid witness");

            Expect(Nexus.CreateFeed(RootStorage, owner, name, mode), "feed creation failed");

            this.Notify(EventKind.FeedCreate, owner, name);
        }

        /*public BigInteger CreatePlatform(Address from, string name, string externalAddress, Address interopAddress, string fuelSymbol)
        {
            ExpectAddressSize(from, nameof(from));
            ExpectNameLength(name, nameof(name));
            ExpectUrlLength(externalAddress, nameof(externalAddress));
            ExpectAddressSize(interopAddress, nameof(interopAddress));
            ExpectNameLength(fuelSymbol, nameof(fuelSymbol));

            Expect(this.IsRootChain(), "must be root chain");

            Expect(from == GenesisAddress, "(CreatePlatform) must be genesis");
            Expect(IsWitness(from), "invalid witness");

            Expect(ValidationUtils.IsValidIdentifier(name), "invalid platform name");

            var platformID = Nexus.CreatePlatform(RootStorage, externalAddress, interopAddress, name, fuelSymbol);
            Expect(platformID > 0, $"creation of platform with id {platformID} failed");

            this.Notify(EventKind.PlatformCreate, from, name);
            return platformID;
        }*/

        public void CreateOrganization(Address from, string ID, string name, byte[] script)
        {
            ExpectAddressSize(from, nameof(from));
            ExpectNameLength(ID, nameof(ID));
            ExpectNameLength(name, nameof(name));
            ExpectScriptLength(script, nameof(script));

            Expect(this.IsRootChain(), "must be root chain");

            Expect(IsWitness(from), "invalid witness");

            Expect(ValidationUtils.IsValidIdentifier(ID), "invalid organization name");

            Expect(!Nexus.OrganizationExists(RootStorage, ID), "organization already exists");

            Nexus.CreateOrganization(RootStorage, ID, name, script);

            // TODO org cost
            /*var fuelCost = GetGovernanceValue(DomainSettings.FuelPerOrganizationDeployTag);
            // governance value is in usd fiat, here convert from fiat to fuel amount
            fuelCost = this.GetTokenQuote(DomainSettings.FiatTokenSymbol, DomainSettings.FuelTokenSymbol, fuelCost);
            // burn the "cost" tokens
            BurnTokens(DomainSettings.FuelTokenSymbol, from, fuelCost);*/


            this.Notify(EventKind.OrganizationCreate, from, ID);
        }

        public IArchive CreateArchive(MerkleTree merkleTree, Address owner, string name, BigInteger size, Timestamp time, IArchiveEncryption encryption)
        {
            //TODO check valid values of merkleTree, encryption
            ExpectAddressSize(owner, nameof(owner));
            ExpectNameLength(name, nameof(name));

            // TODO validation
            var archive = Nexus.CreateArchive(this.RootStorage, merkleTree, owner, name, size, time, encryption);

            this.Notify(EventKind.FileCreate, owner, archive.Hash);

            return archive;
        }

        public bool WriteArchive(IArchive archive, int blockIndex, byte[] data)
        {
            //TODO: archive validation
            ExpectArchiveLength(data, nameof(data));

            if (archive == null)
            {
                return false;
            }

            var blockCount = (int)archive.GetBlockCount();
            if (blockIndex < 0 || blockIndex >= blockCount)
            {
                return false;
            }

            Nexus.WriteArchiveBlock((Archive)archive, blockIndex, data);
            return true;
        }

        public bool IsAddressOfParentChain(Address address)
        {
            if (this.IsRootChain())
            {
                return false;
            }

            ExpectAddressSize(address, nameof(address));
            var parentName = Nexus.GetParentChainByName(Chain.Name);
            var target = Nexus.GetChainByAddress(address);
            return target.Name == parentName;
        }

        public bool IsAddressOfChildChain(Address address)
        {
            ExpectAddressSize(address, nameof(address));
            var parentName = Nexus.GetParentChainByAddress(address);
            return Chain.Name == parentName;
        }

        public bool IsNameOfParentChain(string name)
        {
            if (this.IsRootChain())
            {
                return false;
            }

            ExpectNameLength(name, nameof(name));
            var parentName = Nexus.GetParentChainByName(Chain.Name);
            return name == parentName;
        }

        public bool IsNameOfChildChain(string name)
        {
            ExpectNameLength(name, nameof(name));
            var parentName = Nexus.GetParentChainByName(name);
            return Chain.Name == parentName;
        }

        public void MintTokens(string symbol, Address from, Address target, BigInteger amount)
        {
            ExpectNameLength(symbol, nameof(symbol));
            ExpectAddressSize(from, nameof(from));
            ExpectAddressSize(target, nameof(target));

            if (HasGenesis)
            {
                if (IsSystemToken(symbol))
                {
                    var ctxName = CurrentContext.Name;
                    Expect(
                            ctxName == VirtualMachine.StakeContextName ||
                            ctxName == VirtualMachine.GasContextName ||
                            ctxName == VirtualMachine.ExchangeContextName,
                            $"Minting system tokens only allowed in a specific context, current {ctxName}");
                }
            }

            Expect(IsWitness(from), "must be from a valid witness");

            Expect(amount > 0, "amount must be positive and greater than zero");

            Expect(TokenExists(symbol), "invalid token");
            IToken token; 
            token = GetToken(symbol);
            Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Expect(!token.Flags.HasFlag(TokenFlags.Fiat), "token can't be fiat");
            
            Nexus.MintTokens(this, token, from, target, Chain.Name, amount);
        }

        public BigInteger MintToken(string symbol, Address from, Address target, byte[] rom, byte[] ram, BigInteger seriesID)
        {
            ExpectNameLength(symbol, nameof(symbol));
            ExpectAddressSize(from, nameof(from));
            ExpectAddressSize(target, nameof(target));

            if (IsSystemToken(symbol))
            {
                var ctxName = CurrentContext.Name;
                Expect(ctxName == "gas" || ctxName == "stake" || ctxName == "exchange", "Minting system tokens only allowed in a specific context");
            }

            Expect(TokenExists(symbol), "invalid token");
            IToken token;
            token = GetToken(symbol);
            Expect(!token.IsFungible(), "token must be non-fungible");

            // TODO should not be necessary, verified by trigger
            //Expect(IsWitness(target), "invalid witness");

            Expect(IsWitness(from), "must be from a valid witness");
            Expect(this.IsRootChain(), "can only mint nft in root chain");

            Expect(rom.Length <= TokenContent.MaxROMSize, "ROM size exceeds maximum allowed, received: " + rom.Length + ", maximum: " + TokenContent.MaxROMSize);
            Expect(ram.Length <= TokenContent.MaxRAMSize, "RAM size exceeds maximum allowed, received: " + ram.Length + ", maximum: " + TokenContent.MaxRAMSize);

            Address creator = from;

            BigInteger tokenID;
            tokenID = Nexus.GenerateNFT(this, symbol, Chain.Name, creator, rom, ram, seriesID);
            Expect(tokenID > 0, "invalid tokenID");

            Nexus.MintToken(this, token, from, target, Chain.Name, tokenID);

            return tokenID;
        }

        public void BurnTokens(string symbol, Address target, BigInteger amount)
        {
            ExpectNameLength(symbol, nameof(symbol));
            ExpectAddressSize(target, nameof(target));

            Expect(amount > 0, "amount must be positive and greater than zero");
            Expect(TokenExists(symbol), "invalid token");

            var token = GetToken(symbol);
            Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Expect(token.IsBurnable(), "token must be burnable");
            Expect(!token.Flags.HasFlag(TokenFlags.Fiat), "token can't be fiat");

            Nexus.BurnTokens(this, token, target, target, Chain.Name, amount);
        }

        public void BurnToken(string symbol, Address target, BigInteger tokenID)
        {
            ExpectNameLength(symbol, nameof(symbol));
            ExpectAddressSize(target, nameof(target));

            Expect(IsWitness(target), "invalid witness");
            Expect(this.IsRootChain(), "must be root chain");
            Expect(TokenExists(symbol), "invalid token");

            var token = GetToken(symbol);
            Expect(!token.IsFungible(), "token must be non-fungible");
            Expect(token.IsBurnable(), "token must be burnable");

            Nexus.BurnToken(this, token, target, target, Chain.Name, tokenID);
        }

        public void InfuseToken(string symbol, Address from, BigInteger tokenID, string infuseSymbol, BigInteger value)
        {
            ExpectNameLength(symbol, nameof(symbol));
            ExpectAddressSize(from, nameof(from));
            ExpectNameLength(infuseSymbol, nameof(infuseSymbol));

            Expect(IsWitness(from), "invalid witness");
            Expect(this.IsRootChain(), "must be root chain");
            Expect(TokenExists(symbol), "invalid token");

            var token = GetToken(symbol);
            Expect(!token.IsFungible(), "token must be non-fungible");
            Expect(token.IsBurnable(), "token must be burnable");

            var infuseToken = GetToken(infuseSymbol);

            Nexus.InfuseToken(this, token, from, tokenID, infuseToken, value);
        }

        public ITokenSeries GetTokenSeries(string symbol, BigInteger seriesID)
        {
            ExpectNameLength(symbol, nameof(symbol));
            return Nexus.GetTokenSeries(this.RootStorage, symbol, seriesID);
        }

        public ITokenSeries CreateTokenSeries(string symbol, Address from, BigInteger seriesID, BigInteger maxSupply, TokenSeriesMode mode, byte[] script, ContractInterface abi)
        {
            ExpectNameLength(symbol, nameof(symbol));
            ExpectAddressSize(from, nameof(from));
            ExpectEnumIsDefined(mode, nameof(mode));
            ExpectScriptLength(script, nameof(script));
            ExpectValidContractInterface(abi);

            Expect(seriesID >= 0, "invalid series ID");
            Expect(this.IsRootChain(), "must be root chain");
            Expect(TokenExists(symbol), "invalid token");

            var token = GetToken(symbol);
            Expect(!token.IsFungible(), "token must be non-fungible");

            Expect(IsWitness(from), "invalid witness");
            Expect(InvokeTriggerOnToken(false, token, TokenTrigger.OnSeries, from) != TriggerResult.Failure, $"trigger {TokenTrigger.OnSeries} on token {symbol} failed for {from}");

            return Nexus.CreateSeries(RootStorage, token, seriesID, maxSupply, mode, script, abi);
        }

        public void TransferTokens(string symbol, Address source, Address destination, BigInteger amount)
        {
            ExpectNameLength(symbol, nameof(symbol));
            ExpectAddressSize(source, nameof(source));
            ExpectAddressSize(destination, nameof(destination));

            Expect(!source.IsNull, "invalid source");

            if (source == destination || amount == 0)
            {
                return;
            }

            Expect(TokenExists(symbol), "invalid token");
            var token = GetToken(symbol);

            Expect(amount > 0, "amount must be greater than zero");

            if (destination.IsInterop)
            {
                Expect(Chain.IsRoot, "interop transfers only allowed in main chain");
                this.CallNativeContext(NativeContractKind.Interop, nameof(InteropContract.WithdrawTokens), source, destination, symbol, amount);
                return;
            }

            Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");

            Nexus.TransferTokens(this, token, source, destination, amount);
        }

        public void TransferToken(string symbol, Address source, Address destination, BigInteger tokenID)
        {
            ExpectNameLength(symbol, nameof(symbol));
            ExpectAddressSize(source, nameof(source));
            ExpectAddressSize(destination, nameof(destination));

            Expect(IsWitness(source), "invalid witness");
            Expect(source != destination, "source and destination must be different");
            Expect(TokenExists(symbol), "invalid token");

            var token = GetToken(symbol);
            Expect(!token.IsFungible(), "token must be non-fungible");

            Nexus.TransferToken(this, token, source, destination, tokenID);
        }

        public void SwapTokens(string sourceChain, Address from, string targetChain, Address to, string symbol, BigInteger value)
        {
            ExpectNameLength(sourceChain, nameof(sourceChain));
            ExpectAddressSize(from, nameof(from));
            ExpectNameLength(targetChain, nameof(targetChain));
            ExpectAddressSize(to, nameof(to));
            ExpectNameLength(symbol, nameof(symbol));

            Expect(sourceChain != targetChain, "source chain and target chain must be different");
            Expect(TokenExists(symbol), "invalid token");

            var token = GetToken(symbol);
            Expect(token.Flags.HasFlag(TokenFlags.Transferable), "must be transferable token");

            if (PlatformExists(sourceChain))
            {
                Expect(sourceChain != DomainSettings.PlatformName, "invalid platform as source chain");

                if (token.IsFungible())
                {
                    Nexus.MintTokens(this, token, from, to, sourceChain, value);
                }
                else
                {
                    Nexus.MintToken(this, token, from, to, sourceChain, value);
                }
            }
            else if (PlatformExists(targetChain))
            {
                Expect(targetChain != DomainSettings.PlatformName, "invalid platform as target chain");
                Nexus.BurnTokens(this, token, from, to, targetChain, value);

                var swap = new ChainSwap(DomainSettings.PlatformName, sourceChain, Transaction.Hash, targetChain, targetChain, Hash.Null);
                Chain.RegisterSwap(Storage, to, swap);
            }
            else
            if (sourceChain == Chain.Name)
            {
                Expect(IsNameOfParentChain(targetChain) || IsNameOfChildChain(targetChain), "target must be parent or child chain");
                Expect(to.IsUser, "destination must be user address");
                Expect(IsWitness(from), "invalid witness");

                /*if (tokenInfo.IsCapped())
                {
                    var sourceSupplies = new SupplySheet(symbol, this.Chain, Nexus);
                    var targetSupplies = new SupplySheet(symbol, targetChain, Nexus);

                    if (IsAddressOfParentChain(targetChainAddress))
                    {
                        Expect(sourceSupplies.MoveToParent(this.Storage, amount), "source supply check failed");
                    }
                    else // child chain
                    {
                        Expect(sourceSupplies.MoveToChild(this.Storage, targetChain.Name, amount), "source supply check failed");
                    }
                }*/

                if (token.IsFungible())
                {
                    Nexus.BurnTokens(this, token, from, to, targetChain, value);
                }
                else
                {
                    Nexus.BurnToken(this, token, from, to, targetChain, value);
                }

                var swap = new ChainSwap(DomainSettings.PlatformName, sourceChain, Transaction.Hash, DomainSettings.PlatformName, targetChain, Hash.Null);
                Chain.RegisterSwap(Storage, to, swap);
            }
            else
            if (targetChain == Chain.Name)
            {
                Expect(IsNameOfParentChain(sourceChain) || IsNameOfChildChain(sourceChain), "source must be parent or child chain");
                Expect(!to.IsInterop, "destination cannot be interop address");
                Expect(IsWitness(to), "invalid witness");

                if (token.IsFungible())
                {
                    Nexus.MintTokens(this, token, from, to, sourceChain, value);
                }
                else
                {
                    Nexus.MintToken(this, token, from, to, sourceChain, value);
                }
            }
            else
            {
                throw new ChainException("invalid swap chain source and destinations");
            }
        }

        public void WriteToken(Address from, string tokenSymbol, BigInteger tokenID, byte[] ram)
        {
            ExpectAddressSize(from, nameof(from));
            ExpectNameLength(tokenSymbol, nameof(tokenSymbol));
            ExpectRamLength(ram, nameof(ram));

            var nft = ReadToken(tokenSymbol, tokenID);
            var token = GetToken(tokenSymbol);

            // If trigger is missing the code will be executed
            Expect(InvokeTriggerOnToken(true, token, TokenTrigger.OnWrite, from, ram, tokenID) != TriggerResult.Failure, "token write trigger failed");

            Nexus.WriteNFT(this, tokenSymbol, tokenID, nft.CurrentChain, nft.Creator, nft.CurrentOwner, nft.ROM, ram,
                    nft.SeriesID, nft.Timestamp, nft.Infusion, true);
        }

        public TokenContent ReadToken(string tokenSymbol, BigInteger tokenID)
        {
            ExpectNameLength(tokenSymbol, nameof(tokenSymbol));
            return Nexus.ReadNFT(this, tokenSymbol, tokenID);
        }

        public bool IsPlatformAddress(Address address)
        {
            ExpectAddressSize(address, nameof(address));
            return Nexus.IsPlatformAddress(RootStorage, address);
        }

        public void RegisterPlatformAddress(string platform, Address localAddress, string externalAddress)
        {
            ExpectNameLength(platform, nameof(platform));
            ExpectAddressSize(localAddress, nameof(localAddress));
            ExpectUrlLength(externalAddress, nameof(externalAddress));
            Expect(Chain.Name == DomainSettings.RootChainName, "must be in root chain");
            Nexus.RegisterPlatformAddress(RootStorage, platform, localAddress, externalAddress);
        }

        public byte[] ReadOracle(string URL)
        {
            ExpectUrlLength(URL, nameof(URL));
            return Oracle.Read<byte[]>(Time, URL);
        }

        public Hash GetTokenPlatformHash(string symbol, IPlatform platform)
        {
            ExpectNameLength(symbol, nameof(symbol));
            ExpectValidPlatform(platform);

            if (platform == null)
            {
                return Hash.Null;
            }

            return Nexus.GetTokenPlatformHash(symbol, platform.Name, RootStorage);
        }

        public IToken GetToken(string symbol)
        {
            ExpectNameLength(symbol, nameof(symbol));
            return Nexus.GetTokenInfo(RootStorage, symbol);
        }

        public IFeed GetFeed(string name)
        {
            ExpectNameLength(name, nameof(name));
            return Nexus.GetFeedInfo(RootStorage, name);
        }

        public IPlatform GetPlatformByName(string name)
        {
            ExpectNameLength(name, nameof(name));
            return Nexus.GetPlatformInfo(RootStorage, name);
        }

        public IPlatform GetPlatformByIndex(int index)
        {
            index--;
            var platforms = GetPlatforms();
            if (index < 0 || index >= platforms.Length)
            {
                return null;
            }

            var name = platforms[index];
            return GetPlatformByName(name);
        }

        public IChain GetChainByAddress(Address address)
        {
            ExpectAddressSize(address, nameof(address));
            return Nexus.GetChainByAddress(address);
        }

        public IChain GetChainByName(string name)
        {
            ExpectNameLength(name, nameof(name));
            return Nexus.GetChainByName(name);
        }

        public string[] GetTokens()
        {
            return Nexus.GetTokens(RootStorage);
        }

        public IContract[] GetContracts()
        {
            return Chain.GetContracts(RootStorage);
        }

        public string[] GetChains()
        {
            return Nexus.GetChains(RootStorage);
        }

        public string[] GetPlatforms()
        {
            return Nexus.GetPlatforms(RootStorage);
        }

        public string[] GetFeeds()
        {
            return Nexus.GetFeeds(RootStorage);
        }

        public string[] GetOrganizations()
        {
            return Nexus.GetOrganizations(RootStorage);
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
                lines.Add("Payload: " + (Transaction.Payload != null && Transaction.Payload.Length > 0 ? Base16.Encode(Transaction.Payload) : "None"));
                var bytes = Transaction.ToByteArray(true);
                lines.Add(VMException.Header("RAWTX"));
                lines.Add(Base16.Encode(bytes));
            }
        }

        public bool OrganizationExists(string name)
        {
            ExpectNameLength(name, nameof(name));
            return Nexus.OrganizationExists(RootStorage, name);
        }

        public IOrganization GetOrganization(string name)
        {
            ExpectNameLength(name, nameof(name));
            return Nexus.GetOrganizationByName(RootStorage, name);
        }

        public bool AddMember(string organization, Address admin, Address target)
        {
            ExpectNameLength(organization, nameof(organization));
            ExpectAddressSize(admin, nameof(admin));
            ExpectAddressSize(target, nameof(target));

            var org = Nexus.GetOrganizationByName(RootStorage, organization);
            return org.AddMember(this, admin, target);
        }

        public bool RemoveMember(string organization, Address admin, Address target)
        {
            ExpectNameLength(organization, nameof(organization));
            ExpectAddressSize(admin, nameof(admin));
            ExpectAddressSize(target, nameof(target));

            var org = Nexus.GetOrganizationByName(RootStorage, organization);
            return org.RemoveMember(this, admin, target);
        }

        public void MigrateToken(Address from, Address to)
        {
            ExpectAddressSize(from, nameof(from));
            ExpectAddressSize(to, nameof(to));

            this.Nexus.MigrateTokenOwner(this.RootStorage, from, to);
        }


        public void MigrateMember(string organization, Address admin, Address source, Address destination)
        {
            ExpectNameLength(organization, nameof(organization));
            ExpectAddressSize(admin, nameof(admin));
            ExpectAddressSize(source, nameof(source));
            ExpectAddressSize(destination, nameof(destination));

            var org = Nexus.GetOrganizationByName(RootStorage, organization);
            org.MigrateMember(this, admin, source, destination);
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

        public Address GetContractOwner(Address address)
        {
            ExpectAddressSize(address, nameof(address));
            return Chain.GetContractOwner(Storage, address);
        }

        #region TASKS
        public IChainTask StartTask(Address from, string contractName, ContractMethod method, uint frequency, uint delay, TaskFrequencyMode mode, BigInteger gasLimit)
        {
            ExpectAddressSize(from, nameof(from));
            ExpectNameLength(contractName, nameof(contractName));
            ExpectValidContractMethod(method);
            ExpectEnumIsDefined(mode, nameof(mode));
            
            Expect(gasLimit >= 999, "invalid gas limit");

            Expect(ValidationUtils.IsValidIdentifier(contractName), "invalid contract name");
            Expect(method.offset >= 0, "invalid method offset");

            Expect(method.returnType == VMType.Bool, "method used in task must have bool as return type");

            var contract = Chain.GetContractByName(Storage, contractName);
            Expect(contract != null, "contract not found: " + contractName);

            Expect(contract is CustomContract, "contract used for task must be custom");
            Expect(contract.ABI.Implements(method), "contract abi does not implement method: " + method.name);

            if (mode != TaskFrequencyMode.Always)
            {
                Expect(frequency > 0, "invalid frequency");
            }
            else
            {
                Expect(frequency == 0, "invalid frequency");
            }

            Expect(IsWitness(from), "invalid witness");

            var result = Chain.StartTask(Storage, from, contractName, method, frequency, delay, mode, gasLimit);
            Expect(result != null, "could not start task");

            this.Notify(EventKind.TaskStart, from, result.ID);

            return result;
        }

        public void StopTask(IChainTask task)
        {
            ExpectValidChainTask(task);

            Expect(IsWitness(task.Owner), "invalid witness");
            Expect(Chain.StopTask(Storage, task.ID), "failed to stop task");

            this.Notify(EventKind.TaskStop, task.Owner, task.ID);
        }

        public IChainTask GetTask(BigInteger taskID)
        {
            if (taskID <= 0)
            {
                return null;
            }

            if (CurrentTask != null && CurrentTask.ID == taskID)
            {
                return CurrentTask;
            }

            return Chain.GetTask(Storage, taskID);
        }
        #endregion

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

        #region Expect
        private void ExpectAddressSize(Address address, string name, string prefix = "") =>
            Expect(address.GetSize() <= DomainSettings.AddressMaxSize, $"{prefix}{name} exceeds max address size");

        private void ExpectArchiveLength(byte[] content, string name, string prefix = "") =>
            Expect(content.Length <= DomainSettings.ArchiveMaxSize, $"{prefix}{name} exceeds maximum length");

        private void ExpectArgsLength(object[] args, string name, string prefix = "") =>
            ExpectArgsLength(args.Length, name, prefix);

        private void ExpectArgsLength(int length, string name, string prefix = "") =>
            Expect(length <= DomainSettings.ArgsMax, $"{prefix}{name} exceeds max number of arguments");

        private void ExpectHashSize(Hash hash, string name, string prefix = "") =>
            Expect(hash.Size == Hash.Length, $"{prefix}{name} is an incorrect size");

        private void ExpectEnumIsDefined<TEnum>(TEnum value, string name, string prefix = "") where TEnum : struct, Enum =>
            Expect(Enum.IsDefined(value), $"{prefix}{name} is not defined");

        private void ExpectNameLength(string value, string name, string prefix = "") =>
            Expect(value.Length <= DomainSettings.NameMaxLength, $"{prefix}{name} exceeds max length");

        private void ExpectNotNull<T>(T value, string name, string prefix = "") where T : class =>
            Expect(value != null, $"{prefix}{name} should not be null");

        private void ExpectUrlLength(string value, string name, string prefix = "") =>
            Expect(value.Length <= DomainSettings.UrlMaxLength, $"{prefix}{name} exceeds max length");

        private void ExpectRamLength(byte[] ram, string name, string prefix = "") =>
            Expect(ram.Length <= TokenContent.MaxRAMSize, $"{prefix}RAM size exceeds maximum allowed, name: {name}, received: {ram.Length}, maximum: {TokenContent.MaxRAMSize}");

        private void ExpectRomLength(byte[] rom, string name, string prefix = "") =>
            Expect(rom.Length <= TokenContent.MaxROMSize, $"{prefix}ROM size exceeds maximum allowed, name: {name}, received: {rom.Length}, maximum:{TokenContent.MaxROMSize}");
        
        private void ExpectScriptLength(byte[] value, string name, string prefix = "") =>
            Expect(value != null ? value.Length <= DomainSettings.ScriptMaxSize : true, $"{prefix}{name} exceeds max length");

        private void ExpectTokenExists(string symbol, string prefix = "") =>
            Expect(TokenExists(symbol), $"{prefix}Token does not exist ({symbol})");

        private void ExpectValidToken(IToken token)
        {
            const string prefix = "invalid token: ";
            ExpectNameLength(token.Name, nameof(token.Name), prefix);
            ExpectNameLength(token.Symbol, nameof(token.Symbol), prefix);
            ExpectAddressSize(token.Owner, nameof(token.Owner), prefix);
            ExpectScriptLength(token.Script, nameof(token.Owner), prefix);
            //TODO: Guard against bad ABI?
        }

        private void ExpectValidChainTask(IChainTask task)
        {
            const string prefix = "invalid chain task: ";
            ExpectNotNull(task, nameof(task), prefix);
            ExpectNameLength(task.ContextName, nameof(task.ContextName), prefix);
            ExpectNameLength(task.Method, nameof(task.Method), prefix);
            ExpectAddressSize(task.Owner, nameof(task.Owner), prefix);
            ExpectEnumIsDefined(task.Mode, nameof(task.Mode), prefix);
        }

        private void ExpectValidContractEvent(ContractEvent evt)
        {
            const string prefix = "invalid contract event: ";
            ExpectNameLength(evt.name, nameof(evt.name), prefix);
            ExpectEnumIsDefined(evt.returnType, nameof(evt.returnType), prefix);
            
            //TODO: Is the max length of the description byte array different than a script byte array?
            ExpectScriptLength(evt.description, nameof(evt.description), prefix);
        }

        private void ExpectValidContractMethod(ContractMethod method)
        {
            const string prefix = "invalid contract method: ";
            ExpectNameLength(method.name, nameof(method.name), prefix);
            ExpectEnumIsDefined(method.returnType, nameof(method.returnType), prefix);
            ExpectArgsLength(method.parameters.Length, nameof(method.parameters));

            foreach (var parameter in method.parameters) 
                ExpectValidContractParameter(parameter);
        }

        private void ExpectValidContractInterface(ContractInterface contractInterface)
        {
            ExpectArgsLength(contractInterface.MethodCount, nameof(contractInterface.MethodCount));
            ExpectArgsLength(contractInterface.EventCount, nameof(contractInterface.EventCount));

            foreach (var method in contractInterface.Methods)
                ExpectValidContractMethod(method);
            foreach (var evt in contractInterface.Events)
                ExpectValidContractEvent(evt);
        }

        private void ExpectValidContractParameter(ContractParameter parameter)
        {
            const string prefix = "invalid contract method parameter: ";
            ExpectNameLength(parameter.name, nameof(parameter.name), prefix);
            ExpectEnumIsDefined(parameter.type, nameof(parameter.type), prefix);
        }

        private void ExpectValidPlatformSwapAddress(PlatformSwapAddress swap)
        {
            const string prefix = "invalid platform swap address: ";
            ExpectUrlLength(swap.ExternalAddress, nameof(swap.ExternalAddress), prefix);
            ExpectAddressSize(swap.LocalAddress, nameof(swap.LocalAddress), prefix);    
        }

        private void ExpectValidPlatform(IPlatform platform)
        {
            const string prefix = "invalid platform: ";
            ExpectNameLength(platform.Name, nameof(platform.Name), prefix);
            ExpectNameLength(platform.Symbol, nameof(platform.Symbol), prefix);
            ExpectArgsLength(platform.InteropAddresses.Length, nameof(platform.InteropAddresses), prefix);

            foreach (var address in platform.InteropAddresses)
                ExpectValidPlatformSwapAddress(address);
        }
        #endregion

        public bool IsEntryContext(ExecutionContext context)
        {
            Core.Throw.IfNull(context, nameof(context));

            return EntryContext.Address == context.Address;
        }

        // This method is broken, causes infinite recursion
        /*public bool IsCurrentContext(string contextName)
        {
            var context = FindContext(contextName);
            return IsCurrentContext(context);
        }*/

        public bool IsCurrentContext(ExecutionContext context)
        {
            Core.Throw.IfNull(context, nameof(context));

            return CurrentContext.Address == context.Address;
        }

        public bool HasGenesis => Nexus.HasGenesis(); // TODO cache this, as it does not change during a Runtime execution
        
        public IChain GetRootChain()
        {
            return GetChainByName(DomainSettings.RootChainName);
        }

        public bool IsRootChain()
        {
            var rootChain = GetRootChain();
            return Chain.Address == rootChain.Address;
        }

        public VMObject InvokeContractAtTimestamp(NativeContractKind nativeContract, string methodName, params object[] args)
        {
            return Chain.InvokeContractAtTimestamp(Storage, Time, nativeContract, methodName, args);
        }

        public VMObject InvokeContractAtTimestamp(string contractName, string methodName, params object[] args)
        {
            return Chain.InvokeContractAtTimestamp(Storage, Time, contractName, methodName, args);
        }

        public string NexusName => Nexus.Name;
        public uint ProtocolVersion { get; private set; }
        public Hash GenesisHash => Nexus.GetGenesisHash(RootStorage);       
    }
}
