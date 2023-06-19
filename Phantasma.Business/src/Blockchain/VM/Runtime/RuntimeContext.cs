using System;
using System.Numerics;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.VM;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Domain.Execution;
using Phantasma.Core.Domain.Execution.Enums;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.VM;
using Logger = Serilog.Log;

namespace Phantasma.Business.Blockchain.VM;

public partial class RuntimeVM: GasMachine, IRuntime
{
    public override ExecutionContext FindContext(string contextName)
    {
        if (contextName.StartsWith(EVMContext.ContextName))
        {
            var rawTx = contextName.Substring(EVMContext.ContextName.Length + 1);
            return new EVMContext(rawTx, this);
        }

        return base.FindContext(contextName);
    }

    public bool IsEntryContext(ExecutionContext context)
    {
        Core.Throw.IfNull(context, nameof(context));

        if (IsTrigger)
        {
            return false;
        }

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

                var usedGasUntilError = UsedGas;
                ExceptionMessage = ex.Message;
                IsError = true;

                Logger.Error($"Transaction {Transaction?.Hash} failed with {ex.Message}, gas used: {UsedGas}");

                if (!this.IsReadOnlyMode())
                {
                    this.Notify(EventKind.ExecutionFailure, CurrentContext.Address, ExceptionMessage);

                    if (!EnforceGasSpending())
                    {
                        throw ex; // should never happen
                    }

                    UsedGas = usedGasUntilError;
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

                var series = Nexus.GetTokenSeries(RootStorage, symbol, seriesID);
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
                var contract = Chain.GetContractByName(Storage, contextName);
                if (contract != null)
                {
                    return Chain.GetContractContext(changeSet, contract);
                }

                return null;
            }
        }

        private void PushArgsIntoStack(object[] args)
        {
            for (int i = args.Length - 1; i >= 0; i--)
            {
                var obj = VMObject.FromObject(args[i]);
                Stack.Push(obj);
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

            PushFrame(context, jumpOffset, DefaultRegisterCount);

            ActiveAddresses.Push(context.Address);

            var temp = context.Execute(CurrentFrame, Stack);
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
}
