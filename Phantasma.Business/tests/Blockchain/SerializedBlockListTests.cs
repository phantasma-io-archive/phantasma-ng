using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Core.Domain;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain;

public class SerializedBlockListTests
{
    [Fact]
    public void TestToByteArray()
    {
        // Arrange
        var blocks = new Dictionary<BigInteger, Block>()
        {
            // Add some blocks to the dictionary
        };

        var blockTransactions = new Dictionary<BigInteger, List<Transaction>>()
        {
            // Add some transactions to the dictionary
        };

        var serializedBlockList = new SerializedBlockList()
        {
            Blocks = blocks,
            BlockTransactions = blockTransactions
        };

        // Act
        var result = serializedBlockList.ToByteArray();

        // Assert
        Assert.NotNull(result);
        // Assert that the result is what you expect it to be
    }
    
    /*[Fact]
    public void TestDeserialize()
    {
        // Arrange
        var serializedBytes = new byte[] {
            // Add the serialized bytes here
        };

        // Act
        var result = SerializedBlockList.Deserialize(serializedBytes);

        // Assert
        Assert.NotNull(result);
        // Assert that the result is what you expect it to be
    }*/
    
    [Fact]
    public void TestDeserializeData()
    {
        // Arrange
        var serializedBytes = new byte[] {
            00, 01,
            // Add the serialized bytes here
        };

        var stream = new MemoryStream(serializedBytes);
        var reader = new BinaryReader(stream);

        var serializedBlockList = new SerializedBlockList();

        // Act
        serializedBlockList.DeserializeData(reader);

        // Assert
        // Assert that the properties of the serializedBlockList object are what you expect them to be
    }

}
