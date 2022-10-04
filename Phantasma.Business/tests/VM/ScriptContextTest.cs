using System;
using System.Collections.Generic;
using System.Linq;
using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using Moq;
using Phantasma.Business.Blockchain;
using Phantasma.Business.VM;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Domain;
using Phantasma.Core.Types;
using Shouldly;
using Xunit;
using ExecutionContext = Phantasma.Core.Domain.ExecutionContext;

namespace Phantasma.Business.Tests.VM;

using static ScriptContextConstants;

public class ScriptContextTest
{
    [Fact]
    public void Name_should_match_provided_value()
    {
        // Arrange
        var sut = new ScriptContext("test", Array.Empty<byte>(), 0);

        // Assert
        sut.Name.ShouldNotBeNull();
        sut.Name.ShouldBe("test");
    }

    [Fact]
    public void EmptyScript_should_be_opcode_RET()
    {
        // Assert
        ScriptContext.EmptyScript.ShouldBe(new[] { (byte)Opcode.RET });
    }

    [Theory]
    [ScriptContextAutoData]
    public void Step_should_iterate_frames_without_exception([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object,
            ScriptUtils.BeginScript().EmitLoad(0, Timestamp.Now).EmitPush(0).EndScript());
        var stack = new Stack<VMObject>();

        // Act & Assert

        // LOAD
        Should.NotThrow(() => sut.Step(ref frame, stack));
        stack.Count.ShouldBe(0);

        // PUSH
        Should.NotThrow(() => sut.Step(ref frame, stack));
        stack.Count.ShouldBe(1);
        stack.First().Type.ShouldBe(VMType.Timestamp);

        // RET
        Should.NotThrow(() => sut.Step(ref frame, stack));
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_transfer_nft([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        vmMock.Setup(machine => machine.SwitchContext(It.IsAny<ExecutionContext>(), It.IsAny<uint>()))
            .Returns(ExecutionState.Halt);
        vmMock.Setup(machine => machine.PopFrame()).Returns(0u);
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, TransferNftScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_custom_contract([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        vmMock.Setup(machine => machine.SwitchContext(It.IsAny<ExecutionContext>(), It.IsAny<uint>()))
            .Returns(ExecutionState.Halt);
        vmMock.Setup(machine => machine.PopFrame()).Returns(0u);
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, CustomContractScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_migrate_contract([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        vmMock.Setup(machine => machine.SwitchContext(It.IsAny<ExecutionContext>(), It.IsAny<uint>()))
            .Returns(ExecutionState.Halt);
        vmMock.Setup(machine => machine.PopFrame()).Returns(0u);
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, MigrateContractScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_settle_transaction([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        vmMock.Setup(machine => machine.SwitchContext(It.IsAny<ExecutionContext>(), It.IsAny<uint>()))
            .Returns(ExecutionState.Halt);
        vmMock.Setup(machine => machine.PopFrame()).Returns(0u);
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, SettleTransactionScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_alias([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, AliasScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_event_notify_1([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, EventNotify1Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_event_notify_2([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        vmMock.Setup(machine => machine.SwitchContext(It.IsAny<ExecutionContext>(), It.IsAny<uint>()))
            .Returns(ExecutionState.Halt);
        vmMock.Setup(machine => machine.PopFrame()).Returns(0u);
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, EventNotify2Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_token_triggers([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, TokenTriggersScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_token_triggers_event([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, TokenTriggersEventScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_account_triggers_allowance([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, AccountTriggersAllowanceScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_account_triggers_event_propagation([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, AccountTriggersEventPropagationScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_move([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        vmMock.Setup(machine => machine.ExecuteInterop(It.IsAny<string>())).Returns(ExecutionState.Running);
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, MoveScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_copy([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        vmMock.Setup(machine => machine.ExecuteInterop(It.IsAny<string>())).Returns(ExecutionState.Running);
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, CopyScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_load([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, LoadScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_pop([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, PopScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_swap([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, SwapScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_call([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, CallScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_ext_call([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        vmMock.Setup(machine => machine.ExecuteInterop(It.IsAny<string>())).Returns(ExecutionState.Running);
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, ExtCallScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_when_ext_call_interop_halts([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        vmMock.Setup(machine => machine.ExecuteInterop(It.IsAny<string>())).Returns(ExecutionState.Halt);
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, ExtCallScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("VM extcall failed: Upper");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_jmp([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, JmpScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_jmp_conditional([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, JmpConditionalScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_throw([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, ThrowScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("test throw exception");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_local_ops_1([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, LocalOps1Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_when_invalid_cast_occurs([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, LocalOps2Script);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Invalid cast: expected bool, got String");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_cast([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, CastScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_count_1([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Count1Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_count_2([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Count2Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_clear([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, ClearScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_and_1([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, And1Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_and_2([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, And2Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_and_3([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, And3Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_and([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, AndExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_or_1([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Or1Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_but_return_faulted_state_for_or2([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Or2Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Fault);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_or_3([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Or3Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_or_4([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Or4Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_or([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, OrExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("logical op unsupported for type String");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_xor_1([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Xor1Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_but_return_faulted_state_for_xor2([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Xor2Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Fault);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_xor_3([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Xor3Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_xor([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, XorExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("logical op unsupported for type String");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_equals_1([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Equals1Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_equals_2([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Equals2Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_less_than([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, LessThanScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_less_than([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, LessThanExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_greater_than([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, GreaterThanScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_greater_than([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, GreaterThanExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_less_than_or_equals_1([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, LessThanOrEquals1Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_less_than_or_equals_2([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, LessThanOrEquals2Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_less_than_or_equals([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, LessThanOrEqualsExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_greater_than_or_equals_1([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, GreaterThanOrEquals1Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_greater_than_or_equals_2([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, GreaterThanOrEquals2Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_greater_than_or_equals([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, GreaterThanOrEqualsExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_increment([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, IncrementScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_increment([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, IncrementExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Cannot convert String 'hello' to BigInteger.");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_decrement([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, DecrementScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_decrement([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, DecrementExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Cannot convert String 'hello' to BigInteger.");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_sign_1([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Sign1Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_sign_2([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Sign2Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_sign([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, SignExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_negate([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, NegateScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_negate([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, NegateExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_abs([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, AbsScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_abs([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, AbsExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_add_1([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Add1Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_add_2([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Add2Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_add_1([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, AddException1Script);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_add_2([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, AddException2Script);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Script execution failed: invalid string as right operand @ ADD : 16");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_sub([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, SubScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_sub([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, SubExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_mul([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, MulScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_mul([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, MulExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_div([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, DivScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_div([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, DivExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_mod([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, ModScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_mod([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, ModExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_shift_left([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, ShiftLeftScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_shift_left([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, ShiftLeftExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_shift_right([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, ShiftRightScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_shift_right([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, ShiftRightExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_min([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, MinScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_min([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, MinExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_max([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, MaxScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_max([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, MaxExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_pow([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, PowScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_pow([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, PowExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Cannot convert String 'abc' to BigInteger.");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_context_switching([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        vmMock.Setup(machine => machine.SwitchContext(It.IsAny<ExecutionContext>(), It.IsAny<uint>()))
            .Returns(ExecutionState.Halt);
        vmMock.Setup(machine => machine.PopFrame()).Returns(0u);
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, ContextSwitchingScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_context_switching_when_execution_state_is_faulted(
        [Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        vmMock.Setup(machine => machine.SwitchContext(It.IsAny<ExecutionContext>(), It.IsAny<uint>()))
            .Returns(ExecutionState.Fault);
        vmMock.Setup(machine => machine.PopFrame()).Returns(0u);
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, ContextSwitchingScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("VM switch instruction failed: execution state did not halt");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_context_switching_when_context_is_null(
        [Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        vmMock.Setup(machine => machine.FindContext(It.IsAny<string>())).Returns((ExecutionContext)null);
        vmMock.Setup(machine => machine.PopFrame()).Returns(0u);
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, ContextSwitchingScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("VM ctx instruction failed: could not find context with name 'test'");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_put_get([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, PutGetScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_struct_interop([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, StructInteropScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_array_interop([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, ArrayInteropScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_cat_1([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Cat1Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_cat_2([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Cat2Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_cat_3([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Cat3Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_cat_4([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Cat4Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_throw_VMException_for_cat([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, CatExceptionScript);

        // Act
        var result = Should.Throw<VMException>(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.Message.ShouldBe("Invalid cast during concat opcode");
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_range([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, RangeScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_left([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, LeftScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_right([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, RightScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_size_1([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Size1Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_size_2([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Size2Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_size_3([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Size3Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_size_4([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Size4Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_size_5([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Size5Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_size_6([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, Size6Script);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    [Theory]
    [ScriptContextAutoData]
    public void Execute_should_not_throw_for_unpack([Frozen] Mock<TestGasMachine> vmMock)
    {
        // Arrange
        var (sut, frame) = ArrangeScriptContextTest(vmMock.Object, UnpackScript);

        // Act
        var result = Should.NotThrow(() => sut.Execute(frame, new Stack<VMObject>()));

        // Assert
        result.ShouldBe(ExecutionState.Halt);
    }

    private static (ScriptContext, ExecutionFrame) ArrangeScriptContextTest(IVirtualMachine vm, byte[] script,
        int registerCount = VirtualMachine.DefaultRegisterCount)
    {
        var sut = new ScriptContext("test", script, 0);
        var frame = new ExecutionFrame(vm, 0, sut, registerCount);

        return (sut, frame);
    }

    public class TestGasMachine : GasMachine
    {
        public TestGasMachine() : base(Array.Empty<byte>(), 0)
        {
        }

        public override void DumpData(List<string> lines)
        {
        }
    }

    private class ScriptContextAutoDataAttribute : AutoDataAttribute
    {
        public ScriptContextAutoDataAttribute() : base(() =>
        {
            var fixture = new Fixture().Customize(new ScriptContextCustomization());

            return fixture;
        })
        {
        }
    }

    private class ScriptContextCustomization : CompositeCustomization
    {
        public ScriptContextCustomization() : base(new AutoMoqCustomization())
        {
        }
    }
}
