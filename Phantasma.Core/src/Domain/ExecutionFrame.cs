using System;

namespace Phantasma.Core
{
    public class ExecutionFrame
    {
        public VMObject[] Registers { get; }

        public uint Offset { get; } // current instruction pointer **before** the frame was entered
        public ExecutionContext Context { get; }
        public IVirtualMachine VM { get; }

        public ExecutionFrame(IVirtualMachine VM, uint offset, ExecutionContext context, int registerCount)
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
