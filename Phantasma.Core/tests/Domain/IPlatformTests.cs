using System;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class IPlatformTests
{
    [Fact]
    public void TestInteropBlockConstructor()
    {
        // Create some test data
        var platform = "test_platform";
        var chain = "test_chain";
        var hash =  new Hash(CryptoExtensions.Sha256("TestsHash"));
        var transactions = new Hash[]
        {
            new Hash(CryptoExtensions.Sha256("TestsHash2")),
            new  Hash(CryptoExtensions.Sha256("TestsHash41241"))
        };

        // Create an instance of the InteropBlock class
        var block = new InteropBlock(platform, chain, hash, transactions);

        // Assert that the values were set correctly
        Assert.Equal(platform, block.Platform);
        Assert.Equal(chain, block.Chain);
        Assert.Equal(hash, block.Hash);
        Assert.Equal(transactions, block.Transactions);
    }
    
    [Fact]
    public void TestInteropBlockConstructor_Empty()
    {
        // Create an instance of the InteropBlock class
        var block = new InteropBlock();

        // Assert that the values were set correctly
        Assert.Null(block.Platform);
        Assert.Null(block.Chain);
        Assert.True(block.Hash.IsNull);
        Assert.Null(block.Transactions);
    }
    
    [Fact]
    public void TestInteropTransactionConstructor()
    {
        // Create some test data
        var hash = new Hash(CryptoExtensions.Sha256("TestsHash"));
        var transfers = new InteropTransfer[]
        {
            new InteropTransfer(),
            new InteropTransfer()
        };

        // Create an instance of the InteropTransaction class
        var transaction = new InteropTransaction(hash, transfers);

        // Assert that the values were set correctly
        Assert.Equal(hash, transaction.Hash);
        Assert.Equal(transfers, transaction.Transfers);
    }
    
    [Fact]
    public void TestInteropTransactionConstructor_Empty()
    {
        // Create an instance of the InteropTransaction class
        var transaction = new InteropTransaction();

        // Assert that the values were set correctly
        Assert.True(transaction.Hash.IsNull);
        Assert.Null(transaction.Transfers);
    }
    
    [Fact]
    public void TestInteropTransfer()
    {
        // Arrange
        var expectedSourceChain = "SourceChain";
        var expectedSourceAddress = PhantasmaKeys.Generate().Address;
        var expectedDestinationChain = "DestinationChain";
        var expectedDestinationAddress = PhantasmaKeys.Generate().Address;
        var expectedInteropAddress = PhantasmaKeys.Generate().Address;
        var expectedSymbol = "TEST";
        var expectedValue = new BigInteger(100);
        var expectedData = new byte[] { 0x01, 0x02, 0x03 };

        // Act
        var transfer = new InteropTransfer(expectedSourceChain, expectedSourceAddress, expectedDestinationChain, expectedDestinationAddress, expectedInteropAddress, expectedSymbol, expectedValue, expectedData);

        // Assert
        Assert.Equal(expectedSourceChain, transfer.sourceChain);
        Assert.Equal(expectedSourceAddress, transfer.sourceAddress);
        Assert.Equal(expectedDestinationChain, transfer.destinationChain);
        Assert.Equal(expectedDestinationAddress, transfer.destinationAddress);
        Assert.Equal(expectedInteropAddress, transfer.interopAddress);
        Assert.Equal(expectedSymbol, transfer.Symbol);
        Assert.Equal(expectedValue, transfer.Value);
        Assert.Equal(expectedData, transfer.Data);
    }
    
    [Fact]
    public void TestInteropNFT()
    {
        // Arrange
        var expectedName = "Test NFT";
        var expectedDescription = "This is a test NFT.";
        var expectedImageURL = "http://test.com/nft.png";

        // Act
        var nft = new InteropNFT(expectedName, expectedDescription, expectedImageURL);

        // Assert
        Assert.Equal(expectedName, nft.Name);
        Assert.Equal(expectedDescription, nft.Description);
        Assert.Equal(expectedImageURL, nft.ImageURL);
    }
}
