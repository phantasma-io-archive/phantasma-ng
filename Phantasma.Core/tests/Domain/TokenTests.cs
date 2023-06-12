using System;
using System.Linq;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Serializer;
using Phantasma.Core.Domain.Token;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class TokenTests
{
    [Fact]
    public void SetROM_SetsROMProperty()
    {
        // Arrange
        var tokenSeries = new TokenSeries(0, 100, TokenSeriesMode.Unique, new byte[0], new ContractInterface(), new byte[0]);
        var rom = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        
        // Act
        tokenSeries.SetROM(rom);
        
        // Assert
        Assert.Equal(rom, tokenSeries.ROM);
    }

    [Fact]
    public void SetROM_SetsEmptyArrayIfNull()
    {
        // Arrange
        var tokenSeries = new TokenSeries(0, 100, TokenSeriesMode.Unique, new byte[0], new ContractInterface(),
            new byte[0]);

        // Act
        tokenSeries.SetROM(null);
    }

    [Fact]
    public void GenerateMintID_IncrementsMintCount()
    {
        // Arrange
        var tokenSeries = new TokenSeries(0, 100, TokenSeriesMode.Unique, new byte[0], new ContractInterface(), new byte[0]);
        
        // Act
        var mintId = tokenSeries.GenerateMintID();
        
        // Assert
        Assert.Equal(1, mintId);
        Assert.Equal(1, tokenSeries.MintCount);
    }
    
    [Fact]
    public void TestTokenSeriesConstructor()
    {
        // Arrange
        var mintCount = new BigInteger(1);
        var maxSupply = new BigInteger(10);
        var mode = TokenSeriesMode.Unique;
        var script = new byte[] { 1, 2, 3 };
        var abi = new ContractInterface();
        var rom = new byte[] { 4, 5, 6 };

        // Act
        var tokenSeries = new TokenSeries(mintCount, maxSupply, mode, script, abi, rom);

        // Assert
        Assert.Equal(mintCount, tokenSeries.MintCount);
        Assert.Equal(maxSupply, tokenSeries.MaxSupply);
        Assert.Equal(mode, tokenSeries.Mode);
        Assert.Equal(script, tokenSeries.Script);
        Assert.Equal(abi, tokenSeries.ABI);
        Assert.Equal(rom, tokenSeries.ROM);
    }
    
    [Fact]
    public void TestTokenSeriesConstructor_Empty()
    {
        // Arrange
        // Act
        var tokenSeries = new TokenSeries();

        // Assert
        Assert.Equal(0, tokenSeries.MintCount);
        Assert.Equal(0, tokenSeries.MaxSupply);
        Assert.Equal(TokenSeriesMode.Unique, tokenSeries.Mode);
        Assert.Equal(null, tokenSeries.Script);
        Assert.Equal(null, tokenSeries.ABI);
        Assert.Equal(null, tokenSeries.ROM);
    }
    
    [Fact]
    public void TestTokenSeriesSerialize()
    {
        // Arrange
        var mintCount = new BigInteger(1);
        var maxSupply = new BigInteger(10);
        var mode = TokenSeriesMode.Duplicated;
        var script = new byte[] { 1, 2, 3 };
        var abi = new ContractInterface();
        var rom = new byte[] { 4, 5, 6 };
        var tokenSeries = new TokenSeries(mintCount, maxSupply, mode, script, abi, rom);

        // Act
        var serialized = tokenSeries.Serialize();
        var unserialized = Serialization.Unserialize<TokenSeries>(serialized);

        // Assert
        Assert.Equal(tokenSeries.MintCount, unserialized.MintCount);
        Assert.Equal(tokenSeries.MaxSupply, unserialized.MaxSupply);
        Assert.Equal(tokenSeries.Mode, unserialized.Mode);
        Assert.Equal(tokenSeries.Script, unserialized.Script);
        Assert.Equal(tokenSeries.ABI.Events.Count(), unserialized.ABI.Events.Count());
        Assert.Equal(tokenSeries.ABI.Methods.Count(), unserialized.ABI.Methods.Count());
        Assert.Equal(tokenSeries.ABI.EventCount, unserialized.ABI.EventCount);
        Assert.Equal(tokenSeries.ABI.MethodCount, unserialized.ABI.MethodCount);
        Assert.Equal(tokenSeries.ROM, unserialized.ROM);
    }
    
    [Fact]
    public void TestTokenInfoConstructor()
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
        var rom = new byte[] { 4, 5, 6 };

        // Act
        var tokenInfo = new TokenInfo(symbol, name, owner, maxSupply, decimals , flags, script, abi);

        // Assert
        Assert.Equal(symbol, tokenInfo.Symbol);
        Assert.Equal(name, tokenInfo.Name);
        Assert.Equal(decimals, tokenInfo.Decimals);
        Assert.Equal(maxSupply, tokenInfo.MaxSupply);
        Assert.Equal(flags, tokenInfo.Flags);
        Assert.Equal(owner, tokenInfo.Owner);
        Assert.Equal(script, tokenInfo.Script);
        Assert.Equal(abi, tokenInfo.ABI);
        Assert.Equal($"{name} ({symbol})", tokenInfo.ToString());

    }
    
    [Fact]
    public void TestTokenInfoSerialize()
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
        var tokenInfo = new TokenInfo(symbol, name, owner, maxSupply, decimals , flags, script, abi);

        // Act
        var serialized = tokenInfo.Serialize();
        var unserialized = Serialization.Unserialize<TokenInfo>(serialized);

        // Assert
        Assert.Equal(tokenInfo.Symbol, unserialized.Symbol);
        Assert.Equal(tokenInfo.Name, unserialized.Name);
        Assert.Equal(tokenInfo.Decimals, unserialized.Decimals);
        Assert.Equal(tokenInfo.MaxSupply, unserialized.MaxSupply);
        Assert.Equal(tokenInfo.Flags, unserialized.Flags);
        Assert.Equal(tokenInfo.Owner, unserialized.Owner);
        Assert.Equal(tokenInfo.Script, unserialized.Script);
        Assert.Equal(tokenInfo.ABI.Events.Count(), unserialized.ABI.Events.Count());
        Assert.Equal(tokenInfo.ABI.Methods.Count(), unserialized.ABI.Methods.Count());
        Assert.Equal(tokenInfo.ABI.EventCount, unserialized.ABI.EventCount);
        Assert.Equal(tokenInfo.ABI.MethodCount, unserialized.ABI.MethodCount);
        Assert.Equal(tokenInfo.ToString(), unserialized.ToString());
    }
    
    [Fact]
    public void TestTokenInfoSerialize_Empty()
    {
        // Arrange
        var tokenInfo = new TokenInfo();

        // Act
        Assert.Throws<NullReferenceException>(() => tokenInfo.Serialize());
    }
    
    [Fact]
    public void TestTokenInfo_NonFungibleDivisible()
    {
        // Arrange
        var symbol = "TEST";
        var name = "Test Token";
        var owner = PhantasmaKeys.Generate().Address;
        var decimals = 8;
        var maxSupply = new BigInteger(100000000);
        var flags =  TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Finite;
        var script = new byte[] { 1, 2, 3 };
        var abi = new ContractInterface();
        Assert.Throws<Exception>(() => new TokenInfo(symbol, name, owner, maxSupply, decimals, flags, script, abi));
    }
    
    [Fact]
    public void TestTokenInfo_DivisibleZeroDecimals()
    {
        // Arrange
        var symbol = "TEST";
        var name = "Test Token";
        var owner = PhantasmaKeys.Generate().Address;
        var decimals = 0;
        var maxSupply = new BigInteger(100000000);
        var flags = TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible;
        var script = new byte[] { 1, 2, 3 };
        var abi = new ContractInterface();
        Assert.Throws<Exception>(() => new TokenInfo(symbol, name, owner, maxSupply, decimals, flags, script, abi));
    }
    
    [Fact]
    public void TestTokenInfo_NonDivisibleZeroDecimals()
    {
        // Arrange
        var symbol = "TEST";
        var name = "Test Token";
        var owner = PhantasmaKeys.Generate().Address;
        var decimals = -1;
        var maxSupply = new BigInteger(100000000);
        var flags = TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite;
        var script = new byte[] { 1, 2, 3 };
        var abi = new ContractInterface();
        Assert.Throws<Exception>(() => new TokenInfo(symbol, name, owner, maxSupply, decimals, flags, script, abi));
    }
}
