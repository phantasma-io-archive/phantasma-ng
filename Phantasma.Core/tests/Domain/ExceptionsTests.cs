using System.Runtime.Intrinsics.Arm;
using Phantasma.Core.Cryptography;

namespace Phantasma.Core.Tests.Domain;

using Xunit;
using Phantasma.Core.Domain;

public class ExceptionsTests
{
    [Fact]
    public void TestChainException()
    {
        var ex = new ChainException("test");
        Assert.Equal("test", ex.Message);
    }
    
    [Fact]
    public void TestContractException()
    {
        var ex = new ContractException("test");
        Assert.Equal("test", ex.Message);
    }
    
    [Fact]
    public void TestArchiveException()
    {
        var ex = new ArchiveException("test");
        Assert.Equal("test", ex.Message);
    }
    
    [Fact]
    public void TestRelayException()
    {
        var ex = new RelayException("test");
        Assert.Equal("test", ex.Message);
    }
    
    [Fact]
    public void TestStorageException()
    {
        var ex = new OracleException("test");
        Assert.Equal("test", ex.Message);
    }
    
    [Fact]
    public void TestSwapException()
    {
        var ex = new SwapException("test");
        Assert.Equal("test", ex.Message);
    }
    
    [Fact]
    public void TestNodeException()
    {
        var ex = new NodeException("test");
        Assert.Equal("test", ex.Message);
    }
    
    [Fact]
    public void TestBlockGenerationException()
    {
        var ex = new BlockGenerationException("test");
        Assert.Equal("test", ex.Message);
    }
    
    [Fact]
    public void TestDuplicatedTransactionException()
    {
        var input = new byte[32];
        var sha256  = CryptoExtensions.Sha256(input);
        var hash = Hash.FromBytes(sha256);
        var ex = new DuplicatedTransactionException(hash, "test");
        Assert.Equal("test", ex.Message);
    }
    
    [Fact]
    public void TestInvalidTransactionException()
    {
        var input = new byte[32];
        var sha256  = CryptoExtensions.Sha256(input);
        var hash = Hash.FromBytes(sha256);
        var ex = new InvalidTransactionException(hash, "test");
        Assert.Equal("test", ex.Message);
    }
}
