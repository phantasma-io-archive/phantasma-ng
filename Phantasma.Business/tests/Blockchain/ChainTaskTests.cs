using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Tasks;

namespace Phantasma.Business.Tests.Blockchain;

using Xunit;

using Phantasma.Business.Blockchain;

public class ChainTaskTests
{
    [Fact]
    public void TestChainTask()
    {
        // ID, payer, contractName, method, frequency, delay,  mode, gasLimit,  height,  state
        var keys = PhantasmaKeys.Generate();
        var task = new ChainTask(0, keys.Address, "contract", "method", 1, 0, TaskFrequencyMode.Time, 1000, 0, false);

        var taskBytes = task.ToByteArray();
        var task2 = ChainTask.FromBytes(0, taskBytes);
        Assert.Equal(task.Delay, task2.Delay);
        Assert.Equal(task.Frequency, task2.Frequency);
        Assert.Equal(task.GasLimit, task2.GasLimit);
        Assert.Equal(task.Height, task2.Height);
        Assert.Equal(task.State, task2.State);
        Assert.Equal(task.Method, task2.Method);
        Assert.Equal(task.Owner, task2.Owner);
        Assert.Equal(task.ContextName, task2.ContextName);
    }
}
