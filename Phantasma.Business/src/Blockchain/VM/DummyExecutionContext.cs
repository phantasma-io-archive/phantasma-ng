using System.Collections.Generic;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Execution;
using Phantasma.Core.Domain.VM;

namespace Phantasma.Business.Blockchain.VM;

internal class DummyExecutionContext : ExecutionContext
{
    public override string Name => _name;

    private string _name;

    public DummyExecutionContext(string name)
    {
        _name = name;
    }

    public override ExecutionState Execute(ExecutionFrame frame, Stack<VMObject> stack)
    {
        return ExecutionState.Halt;
    }
}
