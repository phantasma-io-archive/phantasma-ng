using System.Collections.Generic;
using System.IO;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Structs;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Domain.VM.Enums;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class IContractTests
{
    // Add tests for ContractEvent
    [Fact]
    public void ToString_ReturnsExpectedString()
    {
        // Arrange
        var contractEvent = new ContractEvent(
            value: 1,
            name: "myEvent",
            returnType: VMType.Bool,
            description: new byte[] { 0x01, 0x02, 0x03 }
        );

        // Act
        var result = contractEvent.ToString();

        // Assert
        Assert.Equal("myEvent : Bool => 1", result);
    }
    
    [Fact (Skip = "Issue on the Serialization")]
    public void Unserialize_DeserializesBinaryDataCorrectly()
    {
        // Arrange
        var binaryData = new byte[] {
            0x01,                                 // value
            0x04, 0x6D, 0x79, 0x45, 0x76, 0x65,   // name
            0x02,                                 // returnType
            0x03, 0x01, 0x02, 0x03                  // description
        };
        var memoryStream = new MemoryStream(binaryData);
        var reader = new BinaryReader(memoryStream);

        // Act
        var result = ContractEvent.Unserialize(reader);

        // Assert
        Assert.Equal(1, result.value);
        Assert.Equal("myEve", result.name);
        Assert.Equal(VMType.Number, result.returnType);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, result.description);
    }
    
    [Fact]
    public void Serialize_SerializesObjectCorrectly()
    {
        // Arrange
        var contractEvent = new ContractEvent(
            value: 1,
            name: "myEvent",
            returnType: VMType.Bool,
            description: new byte[] { 0x01, 0x02, 0x03 }
        );
        
        var bytes = new byte[128];
        var memoryStream = new MemoryStream(bytes);
        var writer = new BinaryWriter(memoryStream);

        var memoryStreamRead = new MemoryStream(bytes);
        var reader = new BinaryReader(memoryStreamRead);

        // Act
        contractEvent.Serialize(writer);
        var contractEventUnserialize = ContractEvent.Unserialize(reader);
        var result = memoryStream.ToArray();

        // Assert
        var expectedData = new byte[] {
            0x01,                                 // value
            0x07, 0x6D, 0x79, 0x45, 0x76, 0x65,   // name
            0x6E,                                 // returnType
            0x03, 0x01, 0x02, 0x03                  // description
        };
        
        Assert.Equal(contractEvent.name, contractEventUnserialize.name);
        Assert.Equal(contractEvent.value, contractEventUnserialize.value);
        Assert.Equal(contractEvent.returnType, contractEventUnserialize.returnType);
        Assert.Equal(contractEvent.description, contractEventUnserialize.description);
        //Assert.Equal(expectedData, result);
        //Assert.Equal(expectedData, result);
    }
    
    [Fact]
    public void TestContractMethod()
    {
        // Define test input
        var name = "testMethod";
        var returnType = VMType.Bool;
        var labels = new Dictionary<string, int>
        {
            { "testMethod", 0 }
        };
        var parameters = new ContractParameter[]
        {
            new ContractParameter("testParameter", VMType.Number)
        };

        // Create instance of ContractMethod
        var contractMethod = new ContractMethod(name, returnType, labels, parameters);

        // Assert expected results
        Assert.Equal(name, contractMethod.name);
        Assert.Equal(returnType, contractMethod.returnType);
        Assert.Equal(parameters, contractMethod.parameters);
        Assert.False(contractMethod.IsProperty());
        Assert.False(contractMethod.IsTrigger());
    }
    
    [Fact]
    public void TestIsProperty()
    {
        // Define test input
        var name = "getTestProperty";
        var returnType = VMType.Bool;
        var labels = new Dictionary<string, int>
        {
            { "getTestProperty", 0 }
        };
        var parameters = new ContractParameter[]
        {
            new ContractParameter("testParameter", VMType.Number)
        };

        // Create instance of ContractMethod
        var contractMethod = new ContractMethod(name, returnType, labels, parameters);

        // Assert expected result
        Assert.True(contractMethod.IsProperty());
    }
}
