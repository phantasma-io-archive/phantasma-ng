using System;
using System.Collections.Generic;
using System.Text;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.VM;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Exceptions;
using Phantasma.Core.Domain.Execution.Enums;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Triggers.Enums;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Storage.Context.Structs;

namespace Phantasma.Business.Blockchain.VM;

public partial class RuntimeVM: GasMachine, IRuntime
{
     private HashSet<string> _triggerGuards = new HashSet<string>();

     /// <summary>
     /// Validate Trigger Guard
     /// </summary>
     /// <param name="triggerName"></param>
     /// <exception cref="ChainException"></exception>
    internal void ValidateTriggerGuard(string triggerName)
    {
        ExpectNameLength(triggerName, nameof(triggerName));

        if (ProtocolVersion <= DomainSettings.Phantasma30Protocol)
        {
            if (_triggerGuards.Contains(triggerName))
            {
                throw new ChainException("trigger loop detected: " + triggerName);
            }
        }
        else if (_triggerGuards.Count >= DomainSettings.MaxTriggerLoop)
        {
            throw new ChainException("trigger loop detected: " + triggerName);
        }


        _triggerGuards.Add(triggerName);
    }

    /// <summary>
    /// Invoke Trigger on Contract
    /// </summary>
    /// <param name="allowThrow"></param>
    /// <param name="address"></param>
    /// <param name="trigger"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public TriggerResult InvokeTriggerOnContract(bool allowThrow, Address address, ContractTrigger trigger,
        params object[] args)
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
            return InvokeTrigger(allowThrow, accountScript, address.Text, accountABI, triggerName, args);
        }

        if (address.IsSystem)
        {
            var contract = Chain.GetContractByAddress(Storage, address);
            if (contract != null)
            {
                if (contract.ABI.HasMethod(triggerName))
                {
                    var customContract = contract as CustomContract;
                    if (customContract != null)
                    {
                        ValidateTriggerGuard($"{contract.Name}.{triggerName}");
                        return InvokeTrigger(allowThrow, customContract.Script, contract.Name, contract.ABI,
                            triggerName, args);
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

    /// <summary>
    /// Invoke Trigger on Token
    /// </summary>
    /// <param name="allowThrow"></param>
    /// <param name="token"></param>
    /// <param name="trigger"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public TriggerResult InvokeTriggerOnToken(bool allowThrow, IToken token, TokenTrigger trigger,
        params object[] args)
    {
        ExpectArgsLength(args, nameof(args));
        ExpectValidToken(token);
        ExpectEnumIsDefined(trigger, nameof(trigger));
        ExpectArgsLength(args, nameof(args));

        return InvokeTrigger(allowThrow, token.Script, token.Symbol, token.ABI, trigger.ToString(), args);
    }

    /// <summary>
    /// Invoke Trigger
    /// </summary>
    /// <param name="allowThrow"></param>
    /// <param name="script"></param>
    /// <param name="contextName"></param>
    /// <param name="abi"></param>
    /// <param name="triggerName"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public TriggerResult InvokeTrigger(bool allowThrow, byte[] script, string contextName, ContractInterface abi,
        string triggerName, params object[] args)
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

        var runtime = new RuntimeVM(-1, script, (uint)method.offset, Chain, Validator, Time, Transaction, changeSet,
            Oracle, ChainTask.Null, true, contextName, this);

        for (int i = args.Length - 1; i >= 0; i--)
        {
            var obj = VMObject.FromObject(args[i]);
            runtime.Stack.Push(obj);
        }

        ExecutionState state;
        try
        {
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
                Notify(evt.Kind, evt.Address, evt.Data, evt.Contract);
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
}
