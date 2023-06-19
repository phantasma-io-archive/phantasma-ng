using System.Numerics;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Tasks;
using Phantasma.Core.Domain.Tasks.Enum;
using Phantasma.Core.Domain.Validation;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Domain.VM.Enums;

namespace Phantasma.Business.Blockchain.VM;

public partial class RuntimeVM : GasMachine, IRuntime
{
    #region TASKS

    public IChainTask StartTask(Address from, string contractName, ContractMethod method, uint frequency, uint delay,
        TaskFrequencyMode mode, BigInteger gasLimit)
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

        this.Notify(EventKind.TaskStart, from, result.ID.ToByteArray());

        return result;
    }

    public void StopTask(IChainTask task)
    {
        ExpectValidChainTask(task);

        Expect(IsWitness(task.Owner), "invalid witness");
        Expect(Chain.StopTask(Storage, task.ID), "failed to stop task");

        this.Notify(EventKind.TaskStop, task.Owner, task.ID.ToByteArray());
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
}
