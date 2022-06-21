using System.Collections.Generic;
using System.Threading.Tasks;

namespace Phantasma.Core
{
    public interface IVirtualMachine
    {
        Stack<VMObject> Stack { get; }

        byte[] EntryScript { get; }

        Address EntryAddress { get; set; }

        ExecutionContext CurrentContext { get; set; }

        ExecutionContext PreviousContext { get; set; }

        ExecutionFrame CurrentFrame { get; set; }

        Stack<ExecutionFrame> Frames { get; }

        void RegisterContext(string contextName, ExecutionContext context);

        Task<ExecutionState> ExecuteInterop(string method);

        abstract ExecutionContext LoadContext(string contextName);

        Task<ExecutionState> Execute();

        void PushFrame(ExecutionContext context, uint instructionPointer,  int registerCount);

        uint PopFrame();

        ExecutionFrame PeekFrame();

        void SetCurrentContext(ExecutionContext context);

        ExecutionContext FindContext(string contextName);

        ExecutionState ValidateOpcode(Opcode opcode);

        Task<ExecutionState> SwitchContext(ExecutionContext context, uint instructionPointer);

        string GetDumpFileName();

        void DumpData(List<string> lines);

        void Expect(bool condition, string description);

    }
}
