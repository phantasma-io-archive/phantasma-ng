using System.Collections.Generic;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Execution;
using Phantasma.Core.Domain.Execution.Enums;
using Phantasma.Core.Domain.VM;

namespace Phantasma.Core.Domain.Interfaces
{
    public interface IExecutionContext
    {
        public string Name { get; }

        public Address Address { get; }

        public abstract ExecutionState Execute(IExecutionFrame frame, Stack<VMObject> stack);
    }
}
