using System.Collections.Generic;
using System.Reflection.Emit;
using System.Threading;

namespace Phantasma.Core
{
    public interface IVirtualMachine
    {

        Stack<VMObject> Stack { get; }

        byte[] EntryScript { get; }

        Address EntryAddress { get; set; }

        IExecutionContext CurrentContext { get; set; }

        IExecutionContext PreviousContext { get; set; }

        IExecutionFrame CurrentFrame { get; set; }

        Stack<IExecutionFrame> Frames { get; }

        public void RegisterContext(string contextName, IExecutionContext context);

        public ExecutionState ExecuteInterop(string method);

        public abstract IExecutionContext LoadContext(string contextName);

        public ExecutionState Execute();

        public void PushFrame(IExecutionContext context, uint instructionPointer,  int registerCount);

        public uint PopFrame();

        public IExecutionFrame PeekFrame();

        public void SetCurrentContext(IExecutionContext context);

        public IExecutionContext FindContext(string contextName);

        public ExecutionState ValidateOpcode(Opcode opcode);

        public ExecutionState SwitchContext(IExecutionContext context, uint instructionPointer);

        public string GetDumpFileName();

        public void DumpData(List<string> lines);

        public void Expect(bool condition, string description);

    }
}
