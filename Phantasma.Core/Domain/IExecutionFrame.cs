using System;
using System.IO;
using System.Numerics;
using System.Collections.Generic;
using System.Security.Policy;

namespace Phantasma.Core
{
    public interface IExecutionFrame
    {
        public VMObject[] Registers { get; }

        uint Offset { get; } // current instruction pointer **before** the frame was entered
        IExecutionContext Context { get; }
        IVirtualMachine VM { get; }

        VMObject GetRegister(int index);
    }
}
