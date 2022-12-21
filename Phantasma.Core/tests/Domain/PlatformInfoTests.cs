using System.Collections.Generic;
using System.IO;
using Phantasma.Core.Cryptography;

namespace Phantasma.Core.Tests.Domain;
using Phantasma.Core.Domain;
using Xunit;

public class PlatformInfoTests
{
    [Fact]
    public void TestNameProperty()
    {
        // Arrange
        var name = "TestPlatform";
        var symbol = "TP";
        var interopAddresses = new List<PlatformSwapAddress>();
        var platformInfo = new PlatformInfo(name, symbol, interopAddresses);

        // Act
        var result = platformInfo.Name;

        // Assert
        Assert.Equal(name, result);
    }

    [Fact]
    public void TestSymbolProperty()
    {
        // Arrange
        var name = "TestPlatform";
        var symbol = "TP";
        var interopAddresses = new List<PlatformSwapAddress>();
        var platformInfo = new PlatformInfo(name, symbol, interopAddresses);

        // Act
        var result = platformInfo.Symbol;

        // Assert
        Assert.Equal(symbol, result);
    }

    [Fact]
    public void TestInteropAddressesProperty()
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
        var result = platformInfo.InteropAddresses;

        // Assert
        Assert.Equal(interopAddresses, result);
    }
    
    [Fact]
    public void TestSerialize()
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
        var result = platformInfo.Serialize();
        var unserializedPlatformInfo = new PlatformInfo();
        var stream = new MemoryStream(result);
        var reader = new BinaryReader(stream);
        unserializedPlatformInfo.UnserializeData(reader);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(platformInfo.Name, unserializedPlatformInfo.Name);
        Assert.Equal(platformInfo.Symbol, unserializedPlatformInfo.Symbol);
        Assert.Equal(platformInfo.InteropAddresses, unserializedPlatformInfo.InteropAddresses);
    }
    
    [Fact]
    public void TestAddAddress()
    {
        // Arrange
        var name = "TestPlatform";
        var symbol = "TP";
        var interopAddresses = new List<PlatformSwapAddress>();
        var newInteropAddress = new PlatformSwapAddress { ExternalAddress = "0x1234567890", LocalAddress = PhantasmaKeys.Generate().Address };
        var platformInfo = new PlatformInfo(name, symbol, interopAddresses);

        // Act
        platformInfo.AddAddress(newInteropAddress);

        // Assert
        Assert.Equal(1, platformInfo.InteropAddresses.Length);
        Assert.Equal(newInteropAddress, platformInfo.InteropAddresses[0]);
    }
    

}
