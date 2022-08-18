using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class ExecutionFrameTests
{
    [Fact]
    public void GetRegister_should_return_register_at_index()
    {
        // Arrange
        var sut = new ExecutionFrame(new TestVm(), 0, new TestExecutionContext(), 5);

        // Act
        var result = sut.GetRegister(4);

        // Assert
        result.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void GetRegister_should_return_throw_when_invalid_index_is_provided(int index)
    {
        // Arrange
        var sut = new ExecutionFrame(new TestVm(), 0, new TestExecutionContext(), 5);

        // Act
        var result = Should.Throw<ArgumentException>(() => sut.GetRegister(index));

        // Assert
        result.Message.ShouldBe("Invalid index");
    }

    [ExcludeFromCodeCoverage]
    private class TestVm : IVirtualMachine
    {
        public Stack<VMObject> Stack { get; } = new();
        public byte[] EntryScript { get; } = { 0 };
        public Address EntryAddress { get; set; }
        public ExecutionContext CurrentContext { get; set; }
        public ExecutionContext PreviousContext { get; set; }
        public ExecutionFrame CurrentFrame { get; set; }
        public Stack<ExecutionFrame> Frames { get; }

        public void RegisterContext(string contextName, ExecutionContext context)
        {
            // Testing
        }

        public ExecutionState ExecuteInterop(string method)
        {
            return ExecutionState.Running;
        }

        public ExecutionContext LoadContext(string contextName)
        {
            throw new NotImplementedException();
        }

        public ExecutionState Execute()
        {
            return ExecutionState.Running;
        }

        public void PushFrame(ExecutionContext context, uint instructionPointer, int registerCount)
        {
            // Testing
        }

        public uint PopFrame()
        {
            return 0u;
        }

        public ExecutionFrame PeekFrame()
        {
            throw new NotImplementedException();
        }

        public void SetCurrentContext(ExecutionContext context)
        {
            // Testing
        }

        public ExecutionContext FindContext(string contextName)
        {
            throw new NotImplementedException();
        }

        public ExecutionState ValidateOpcode(Opcode opcode)
        {
            return ExecutionState.Running;
        }

        public ExecutionState SwitchContext(ExecutionContext context, uint instructionPointer)
        {
            return ExecutionState.Running;
        }

        public string GetDumpFileName()
        {
            return "dump";
        }

        public void DumpData(List<string> lines)
        {
            // Testing
        }

        public void Expect(bool condition, string description)
        {
            // Testing
        }
    }

    [ExcludeFromCodeCoverage]
    private class TestExecutionContext : ExecutionContext
    {
        public override string Name => "TestExeutionContext";

        public override ExecutionState Execute(ExecutionFrame frame, Stack<VMObject> stack)
        {
            return ExecutionState.Running;
        }
    }
}
