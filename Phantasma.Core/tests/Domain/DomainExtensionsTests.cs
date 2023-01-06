using System.Collections.Generic;
using System.Numerics;
using System.Text;
using NSubstitute;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;
using Shouldly;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class DomainExtensionsTests
{
    [Fact]
    public void is_fungible()
    {
        var token = Substitute.For<IToken>();
        var fungible = token.IsFungible();
        fungible.ShouldBeFalse();
    }

    [Fact]
    public void is_burnable()
    {
        var token = Substitute.For<IToken>();
        var burnable = token.IsBurnable();
        burnable.ShouldBeFalse();
    }

    [Fact]
    public void is_transferable()
    {
        var token = Substitute.For<IToken>();
        var transferable = token.IsTransferable();
        transferable.ShouldBeFalse();
    }

    [Fact]
    public void is_capped()
    {
        var token = Substitute.For<IToken>();
        var capped = token.IsCapped();
        capped.ShouldBeFalse();
    }

    [Fact]
    public void get_last_block()
    {
        var runtime = Substitute.For<IRuntime>();
        var block = runtime.GetLastBlock();
        block.ShouldBeNull();
    }

    [Fact]
    public void get_chain_address()
    {
        //TODO
        var platform = Substitute.For<IPlatform>();
        var address = platform.GetChainAddress();
        //address.ShouldBe();
    }

    [Fact]
    public void get_root_chain()
    {
        var runtime = Substitute.For<IRuntime>();
        var chain = runtime.GetRootChain();
        chain.ShouldNotBeNull();
    }

    [Fact]
    public void is_readonly_mode()
    {
        var runtime = Substitute.For<IRuntime>();
        var isReadonly = runtime.IsReadOnlyMode();
        isReadonly.ShouldBeTrue();
    }

    /*
    [Fact]
    public void is_root_chain()
    {
        var runtime = Substitute.For<IRuntime>();
        var isRootChain = runtime.IsRootChain();
        isRootChain.ShouldBeTrue();
    }*/
    
    [Fact]
    public void TestIsFungible()
    {
        // Arrange
        var symbol = "TEST";
        var name = "Test Token";
        var owner = PhantasmaKeys.Generate().Address;
        var decimals = 8;
        var maxSupply = new BigInteger(100000000);
        var flags = TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Finite;
        var script = new byte[] { 1, 2, 3 };
        var abi = new ContractInterface();
        var token = new TokenInfo(symbol, name, owner, maxSupply, decimals , flags, script, abi);

        // Act
        var result = token.IsFungible();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TestGetKind()
    {
        // Arrange
        var address = PhantasmaKeys.Generate().Address;
        var evt = new Event(EventKind.Custom,address, "contractTests");

        // Act
        var result = evt.GetKind<EventKind>();

        // Assert
        Assert.Equal(EventKind.Custom, result);
    }

    [Fact]
    public void TestGetContent()
    {
        // Arrange
        var address = PhantasmaKeys.Generate().Address;
        var text = "Hello, world!";
        var evt = new Event(EventKind.Custom,address, "contractTests", Encoding.UTF8.GetBytes(text));

        // Act
        var result = evt.GetContent<byte[]>();
        var resultString = Encoding.UTF8.GetString(result);

        // Assert
        Assert.Equal("Hello, world!", resultString);
    }

    private enum MyCustomEnum
    {
        One = 1,
        Two = 2,
    }
    
    [Fact]
    public void TestEncodeCustomEvent()
    {
        // Arrange
        var kind = MyCustomEnum.One;

        // Act
        var result = DomainExtensions.EncodeCustomEvent(kind);

        // Assert
        Assert.Equal((EventKind)(EventKind.Custom + 1), result);
    }

    [Fact]
    public void TestDecodeCustomEvent()
    {
        // Arrange
        var kind = (EventKind)(EventKind.Custom + 1);

        // Act
        var result = kind.DecodeCustomEvent<MyCustomEnum>();

        // Assert
        Assert.Equal(MyCustomEnum.One, result);
    }

    
    
    [Fact]
    public void TestGetChainAddress()
    {
        // Arrange
        var name = "TestPlatform";
        var symbol = "TP";
        var userAddress1 = PhantasmaKeys.Generate().Address;
        var userAddress2 = PhantasmaKeys.Generate().Address;
        var interopAddresses = new List<PlatformSwapAddress>
        {
            new PlatformSwapAddress { ExternalAddress = "0x1234567890", LocalAddress = userAddress1 },
            new PlatformSwapAddress { ExternalAddress = "0xabcdefghij", LocalAddress = userAddress2 }
        }; 
        var platformInfo = new PlatformInfo(name, symbol, interopAddresses);

        // Act
        var result = platformInfo.GetChainAddress();

        // Assert
        Assert.Equal(Address.FromHash(name), result);
    }

    [Fact]
    public void TestGetContractName()
    {
        // Arrange

        // Act
        var result = NativeContractKind.Exchange.GetContractName();

        // Assert
        Assert.Equal("exchange", result);
    }
    
    [Fact]
    public void TestFindNativeContractKindByName()
    {
        // Arrange

        // Act
        var result = "stake".FindNativeContractKindByName();

        // Assert
        Assert.Equal(NativeContractKind.Stake, result);
    }

    [Fact]
    public void TestGetOracleTransactionURL()
    {
        // Arrange
        var hash = Hash.FromString("abcdef");

        // Act
        var result = DomainExtensions.GetOracleTransactionURL("myplatform", "mychain", hash);

        // Assert
        Assert.Equal("interop://myplatform/mychain/tx/"+hash.ToString(), result);
    }

    [Fact]
    public void TestGetOracleBlockURL_Hash()
    {
        // Arrange
        var hash = Hash.FromString("abcdef");

        // Act
        var result = DomainExtensions.GetOracleBlockURL("myplatform", "mychain", hash);

        // Assert
        Assert.Equal("interop://myplatform/mychain/block/"+hash.ToString(), result);
    }

    [Fact]
    public void TestGetOracleBlockURL_BigInteger()
    {
        // Arrange

        // Act
        var result = DomainExtensions.GetOracleBlockURL("myplatform", "mychain", 123);

        // Assert
        Assert.Equal("interop://myplatform/mychain/block/123", result);
    }

    [Fact]
    public void TestGetOracleNFTURL()
    {
        // Arrange

        // Act
        var result = DomainExtensions.GetOracleNFTURL("myplatform", "mysymbol", 123);

        // Assert
        Assert.Equal("interop://myplatform/nft/mysymbol/123", result);
    }

    [Fact]
    public void TestGetOracleFeeURL()
    {
        // Arrange

        // Act
        var result = DomainExtensions.GetOracleFeeURL("myplatform");

        // Assert
        Assert.Equal("fee://myplatform", result);
    }
    
    

}
