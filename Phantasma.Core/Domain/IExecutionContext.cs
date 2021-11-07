using System.Collections.Generic;

namespace Phantasma.Core
{
    public enum ExecutionState
    {
        Running,
        Break,
        Fault,
        Halt
    }

    public interface IExecutionContext
    {
        public string Name { get; }

        public Address Address { get; }

        public abstract ExecutionState Execute(IExecutionFrame frame, Stack<VMObject> stack);
    }
}
