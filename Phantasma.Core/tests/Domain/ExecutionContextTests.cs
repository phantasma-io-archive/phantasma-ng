using System;
using System.Collections.Generic;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class ExecutionContextTests
{
    public class ConcreteExecutionContext : ExecutionContext
    {
        private readonly string _name;

        public ConcreteExecutionContext(string name)
        {
            _name = name;
        }

        public override string Name => _name;
        
        public override ExecutionState Execute(ExecutionFrame frame, Stack<VMObject> stack)
        {
            throw new NotImplementedException();
        }
    }
    
    [Fact]
    public void Name_ShouldReturnNameOfExecutionContext()
    {
        var executionContext = new ConcreteExecutionContext("MyExecutionContext");
        Assert.Equal("MyExecutionContext", executionContext.Name);
    }
    
    [Fact]
    public void Address_ShouldReturnAddressOfExecutionContext()
    {
        var executionContext = new ConcreteExecutionContext("MyExecutionContext");
        var expectedAddress = Address.FromHash("MyExecutionContext");
        var testVM = new ExecutionFrameTests.TestVm();
        var executionFrame = new ExecutionFrame(testVM, 0, new ConcreteExecutionContext("Test"), 5);
        var stack = new Stack<VMObject>();
        Assert.Equal(expectedAddress, executionContext.Address);
        Assert.Equal("MyExecutionContext", executionContext.ToString());
        Assert.Throws<NotImplementedException>(() => executionContext.Execute(executionFrame, stack));
    }
}
