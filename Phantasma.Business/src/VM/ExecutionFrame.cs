using System;
using Phantasma.Core;

namespace Phantasma.Business
{
    public class ExecutionFrame : IExecutionFrame
    {
        public VMObject[] Registers { get; }

        public uint Offset { get; } // current instruction pointer **before** the frame was entered
        public IExecutionContext Context { get; }
        public IVirtualMachine VM { get; }

        public ExecutionFrame(IVirtualMachine VM, uint offset, IExecutionContext context, int registerCount)
        {
            this.VM = VM;
            this.Offset = offset;
            this.Context = context;

            Registers = new VMObject[registerCount];

            for (int i = 0; i < registerCount; i++)
            {
                Registers[i] = new VMObject();
            }
        }

        public VMObject GetRegister(int index)
        {
            if (index < 0 || index >= Registers.Length)
            {
                throw new ArgumentException("Invalid index");
            }

            return Registers[index];
        }
    }
}
