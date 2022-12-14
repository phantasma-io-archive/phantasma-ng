using System.IO;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Types;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class ITokenTests
{
    [Fact]
    public void Constructor_SetsSymbolProperty()
    {
        // Arrange
        var symbol = "ABC123";

        // Act
        var packedNFTData = new PackedNFTData(symbol, new byte[0], new byte[0]);

        // Assert
        Assert.Equal(symbol, packedNFTData.Symbol);
    }

    [Fact]
    public void Constructor_SetsROMProperty()
    {
        // Arrange
        var rom = new byte[] { 0x01, 0x02, 0x03 };

        // Act
        var packedNFTData = new PackedNFTData("ABC123", rom, new byte[0]);

        // Assert
        Assert.Equal(rom, packedNFTData.ROM);
    }

    [Fact]
    public void Constructor_SetsRAMProperty()
    {
        // Arrange
        var ram = new byte[] { 0x04, 0x05, 0x06 };

        // Act
        var packedNFTData = new PackedNFTData("ABC123", new byte[0], ram);

        // Assert
        Assert.Equal(ram, packedNFTData.RAM);
    }
    
    [Fact]
    public void TestTokenInfusion()
    {
        // Arrange
        var symbol = "MYTOKEN";
        var value = BigInteger.One;

        // Act
        var infusion = new TokenInfusion(symbol, value);

        // Assert
        Assert.Equal(symbol, infusion.Symbol);
    }

    [Fact]
    public void TestUnserializeData()
    {
        // Arrange
        var seriesID = BigInteger.One;
        var mintID = new BigInteger(2);
        var creator = PhantasmaKeys.Generate().Address;
        var currentChain = "chain";
        var currentOwner = PhantasmaKeys.Generate().Address;
        var rom = new byte[] { 0x01, 0x02, 0x03 };
        var ram = new byte[] { 0x04, 0x05, 0x06 };
        var timestamp = new Timestamp(12345);
        var infusion = new[]
            { new TokenInfusion("symbol1", BigInteger.One), new TokenInfusion("symbol2", 2) };
        var mode = TokenSeriesMode.Unique;
        var tokenContent = new TokenContent(seriesID, mintID, currentChain, creator, currentOwner, rom, ram, timestamp,
            infusion, mode);

        // Act
        using (var stream = new MemoryStream(tokenContent.ToByteArray()))
        using (var reader = new BinaryReader(stream))
        {
            tokenContent.UnserializeData(reader);
        }

        // Assert
        Assert.Equal(seriesID, tokenContent.SeriesID);
        Assert.Equal(mintID, tokenContent.MintID);
        Assert.Equal(creator, tokenContent.Creator);
        Assert.Equal(currentChain, tokenContent.CurrentChain);
        Assert.Equal(currentOwner, tokenContent.CurrentOwner);
        Assert.Equal(rom, tokenContent.ROM);
        Assert.Equal(ram, tokenContent.RAM);
        Assert.Equal(timestamp, tokenContent.Timestamp);
        Assert.Equal(infusion, tokenContent.Infusion);
    }
    
    [Fact]
    public void TestTokenContentReplaceROM()
    {
        // Arrange
        var seriesID = BigInteger.One;
        var mintID = new BigInteger(2);
        var creator = PhantasmaKeys.Generate().Address;
        var currentChain = "chain";
        var currentOwner = PhantasmaKeys.Generate().Address;
        var rom = new byte[] { 0x01, 0x02, 0x03 };
        var ram = new byte[] { 0x04, 0x05, 0x06 };
        var timestamp = new Timestamp(12345);
        var infusion = new[]
            { new TokenInfusion("symbol1", BigInteger.One), new TokenInfusion("symbol2", 2) };
        var mode = TokenSeriesMode.Unique;
        var tokenContent = new TokenContent(seriesID, mintID, currentChain, creator, currentOwner, rom, ram, timestamp,
            infusion, mode);

        // Act
        var newRom = new byte[] { 0x07, 0x08, 0x09 };
        tokenContent.ReplaceROM(newRom);

        // Assert
        Assert.Equal(newRom, tokenContent.ROM);
    }
    
    
}
