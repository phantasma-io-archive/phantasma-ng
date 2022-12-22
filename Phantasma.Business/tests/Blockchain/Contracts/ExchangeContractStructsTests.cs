using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain.Contracts;

public class ExchangeContractStructsTests
{
    [Fact]
    public void LPTokenContentROM_SerializeData_CorrectlySerializesFields()
    {
        // Arrange
        var symbol0 = "abc";
        var symbol1 = "def";
        var id = 123;
        var lpTokenContentROM = new LPTokenContentROM(symbol0, symbol1, id);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Act
        lpTokenContentROM.SerializeData(writer);

        // Assert
        writer.Flush();
        stream.Position = 0;

        using var reader = new BinaryReader(stream);
        Assert.Equal(symbol0, reader.ReadVarString());
        Assert.Equal(symbol1, reader.ReadVarString());
        Assert.Equal(id, reader.ReadBigInteger());
    }
    
    [Fact]
    public void LPTokenContentROM_UnserializeData_CorrectlyUnserializesFields()
    {
        // Arrange
        var symbol0 = "abc";
        var symbol1 = "def";
        var id = 123;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.WriteVarString(symbol0);
        writer.WriteVarString(symbol1);
        writer.WriteBigInteger(id);

        writer.Flush();
        stream.Position = 0;

        using var reader = new BinaryReader(stream);
        var lpTokenContentROM = new LPTokenContentROM();

        // Act
        lpTokenContentROM.UnserializeData(reader);

        // Assert
        Assert.Equal(symbol0, lpTokenContentROM.Symbol0);
        Assert.Equal(symbol1, lpTokenContentROM.Symbol1);
        Assert.Equal(id, lpTokenContentROM.ID);
    }
    
    [Fact]
    public void LPTokenContentRAM_SerializeData_CorrectlySerializesFields()
    {
        // Arrange
        var amount0 = 123;
        var amount1 = 456;
        var liquidity = 789;
        var lpTokenContentRAM = new LPTokenContentRAM(amount0, amount1, liquidity);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Act
        lpTokenContentRAM.SerializeData(writer);

        // Assert
        writer.Flush();
        stream.Position = 0;

        using var reader = new BinaryReader(stream);
        Assert.Equal(amount0, reader.ReadBigInteger());
        Assert.Equal(amount1, reader.ReadBigInteger());
        Assert.Equal(liquidity, reader.ReadBigInteger());
        Assert.Equal(0, reader.ReadBigInteger());
        Assert.Equal(0, reader.ReadBigInteger());
    }

    [Fact]
    public void LPTokenContentRAM_UnserializeData_CorrectlyUnserializesFields()
    {
        // Arrange
        var amount0 = 123;
        var amount1 = 456;
        var liquidity = 789;
        var claimedFeesSymbol0 = 321;
        var claimedFeesSymbol1 = 654;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.WriteBigInteger(amount0);
        writer.WriteBigInteger(amount1);
        writer.WriteBigInteger(liquidity);
        writer.WriteBigInteger(claimedFeesSymbol0);
        writer.WriteBigInteger(claimedFeesSymbol1);

        writer.Flush();
        stream.Position = 0;

        using var reader = new BinaryReader(stream);
        var lpTokenContentRAM = new LPTokenContentRAM();

        // Act
        lpTokenContentRAM.UnserializeData(reader);
        // Assert
        Assert.Equal(amount0, lpTokenContentRAM.Amount0);
        Assert.Equal(amount1, lpTokenContentRAM.Amount1);
        Assert.Equal(liquidity, lpTokenContentRAM.Liquidity);
        Assert.Equal(claimedFeesSymbol0, lpTokenContentRAM.ClaimedFeesSymbol0);
        Assert.Equal(claimedFeesSymbol1, lpTokenContentRAM.ClaimedFeesSymbol1);
    }
    
    [Fact]
    public void TradingVolume_Constructor_InitializesValues()
    {
        // Arrange
        string symbol0 = "BTC";
        string symbol1 = "USD";
        string day = "2022-01-01";
        BigInteger volumeSymbol0 = 100;
        BigInteger volumeSymbol1 = 200;

        // Act
        TradingVolume volume = new TradingVolume(symbol0, symbol1, day, volumeSymbol0, volumeSymbol1);

        // Assert
        Assert.Equal(symbol0, volume.Symbol0);
        Assert.Equal(symbol1, volume.Symbol1);
        Assert.Equal(day, volume.Day);
        Assert.Equal(volumeSymbol0, volume.VolumeSymbol0);
        Assert.Equal(volumeSymbol1, volume.VolumeSymbol1);
    }
    
    [Fact]
    public void TradingVolume_SerializeData_WritesValuesToStream()
    {
        // Arrange
        string symbol0 = "BTC";
        string symbol1 = "USD";
        string day = "2022-01-01";
        BigInteger volumeSymbol0 = 100;
        BigInteger volumeSymbol1 = 200;
        TradingVolume volume = new TradingVolume(symbol0, symbol1, day, volumeSymbol0, volumeSymbol1);

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            // Act
            volume.SerializeData(writer);

            // Assert
            writer.Flush();
            stream.Position = 0;
            var reader = new BinaryReader(stream);
            Assert.Equal(symbol0, reader.ReadVarString());
            Assert.Equal(symbol1, reader.ReadVarString());
            Assert.Equal(day, reader.ReadVarString());
            Assert.Equal(volumeSymbol0, reader.ReadBigInteger());
            Assert.Equal(volumeSymbol1, reader.ReadBigInteger());
        }
    }

    [Fact]
    public void TradingVolume_UnserializeData_ReadsValuesFromStream()
    {
        // Arrange
        string symbol0 = "BTC";
        string symbol1 = "USD";
        string day = "2022-01-01";
        BigInteger volumeSymbol0 = 100;
        BigInteger volumeSymbol1 = 200;

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.WriteVarString(symbol0);
            writer.WriteVarString(symbol1);
            writer.WriteVarString(day);
            writer.WriteBigInteger(volumeSymbol0);
            writer.WriteBigInteger(volumeSymbol1);
            writer.Flush();
            stream.Position = 0;

            var volume = new TradingVolume();
            var reader = new BinaryReader(stream);

            // Act
            volume.UnserializeData(reader);

            // Assert
            Assert.Equal(symbol0, volume.Symbol0);
            Assert.Equal(symbol1, volume.Symbol1);
            Assert.Equal(day, volume.Day);
            Assert.Equal(volumeSymbol0, volume.VolumeSymbol0);
            Assert.Equal(volumeSymbol1, volume.VolumeSymbol1);
        }
    }

    [Fact]
    public void Pool_TestConstructor()
    {
        // Arrange
        string symbol0 = "SYM0";
        string symbol1 = "SYM1";
        string symbol0Address = "0x123";
        string symbol1Address = "0x456";
        BigInteger amount0 = 100;
        BigInteger amount1 = 200;
        BigInteger feeRatio = 50;
        BigInteger totalLiquidity = 300;

        // Act
        var pool = new Pool(symbol0, symbol1, symbol0Address, symbol1Address, amount0, amount1, feeRatio,
            totalLiquidity);

        // Assert
        Assert.Equal(symbol0, pool.Symbol0);
        Assert.Equal(symbol1, pool.Symbol1);
        Assert.Equal(symbol0Address, pool.Symbol0Address);
        Assert.Equal(symbol1Address, pool.Symbol1Address);
        Assert.Equal(amount0, pool.Amount0);
        Assert.Equal(amount1, pool.Amount1);
        Assert.Equal(feeRatio, pool.FeeRatio);
        Assert.Equal(totalLiquidity, pool.TotalLiquidity);
    }

    public void Pool_TestSerializeData()
    {
        // Arrange
        string symbol0 = "SYM0";
        string symbol1 = "SYM1";
        string symbol0Address = "0x123";
        string symbol1Address = "0x456";
        BigInteger amount0 = 100;
        BigInteger amount1 = 200;
        BigInteger feeRatio = 50;
        BigInteger totalLiquidity = 300;
        BigInteger feesForUsersSymbol0 = 10;
        BigInteger feesForUsersSymbol1 = 20;
        BigInteger feesForOwnerSymbol0 = 5;
        BigInteger feesForOwnerSymbol1 = 15;
        var pool = new Pool(symbol0, symbol1, symbol0Address, symbol1Address, amount0, amount1, feeRatio,
            totalLiquidity);
        pool.FeesForUsersSymbol0 = feesForUsersSymbol0;
        pool.FeesForUsersSymbol1 = feesForUsersSymbol1;
        pool.FeesForOwnerSymbol0 = feesForOwnerSymbol0;
        pool.FeesForOwnerSymbol1 = feesForOwnerSymbol1;
        
        // Act
        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        
        pool.SerializeData(writer);
        writer.Flush();
        stream.Position = 0;
        var reader = new BinaryReader(stream);
        
        // Assert
        Assert.Equal(symbol0, reader.ReadVarString());
        Assert.Equal(symbol1, reader.ReadVarString());
        Assert.Equal(symbol0Address, reader.ReadVarString());
        Assert.Equal(symbol1Address, reader.ReadVarString());
        Assert.Equal(amount0, reader.ReadBigInteger());
        Assert.Equal(amount1, reader.ReadBigInteger());
        Assert.Equal(feeRatio, reader.ReadBigInteger());
        Assert.Equal(totalLiquidity, reader.ReadBigInteger());
        Assert.Equal(feesForUsersSymbol0, reader.ReadBigInteger());
        Assert.Equal(feesForUsersSymbol1, reader.ReadBigInteger());
        Assert.Equal(feesForOwnerSymbol0, reader.ReadBigInteger());
        Assert.Equal(feesForOwnerSymbol1, reader.ReadBigInteger());
    }
    
    [Fact]
    public void Pool_TestUnserializeData()
    {
        // Arrange
        string symbol0 = "SYM0";
        string symbol1 = "SYM1";
        string symbol0Address = "0x123";
        string symbol1Address = "0x456";
        BigInteger amount0 = 100;
        BigInteger amount1 = 200;
        BigInteger feeRatio = 50;
        BigInteger totalLiquidity = 300;
        BigInteger feesForUsersSymbol0 = 10;
        BigInteger feesForUsersSymbol1 = 20;
        BigInteger feesForOwnerSymbol0 = 5;
        BigInteger feesForOwnerSymbol1 = 15;
        var pool = new Pool(symbol0, symbol1, symbol0Address, symbol1Address, amount0, amount1, feeRatio,
            totalLiquidity);
        
        pool.FeesForOwnerSymbol0 = feesForOwnerSymbol0;
        pool.FeesForOwnerSymbol1 = feesForOwnerSymbol1;
        pool.FeesForUsersSymbol0 = feesForUsersSymbol0;
        pool.FeesForUsersSymbol1 = feesForUsersSymbol1;

        var bytes = new byte[1024];
        var stream = new MemoryStream(bytes);
        var writer = new BinaryWriter(stream);
        var reader = new BinaryReader(stream);
        var poolResult = new Pool();

        
        // Act
        pool.SerializeData(writer);
        stream.Position = 0;
        stream.Flush();
        
        poolResult.UnserializeData(reader);
        
        // Assert
        Assert.Equal(symbol0, poolResult.Symbol0);
        Assert.Equal(symbol1, poolResult.Symbol1);
        Assert.Equal(symbol0Address, poolResult.Symbol0Address);
        Assert.Equal(symbol1Address, poolResult.Symbol1Address);
        Assert.Equal(amount0, poolResult.Amount0);
        Assert.Equal(amount1, poolResult.Amount1);
        Assert.Equal(feeRatio, poolResult.FeeRatio);
        Assert.Equal(totalLiquidity, poolResult.TotalLiquidity);
        Assert.Equal(feesForUsersSymbol0, poolResult.FeesForUsersSymbol0);
        Assert.Equal(feesForUsersSymbol1, poolResult.FeesForUsersSymbol1);
        Assert.Equal(feesForOwnerSymbol0, poolResult.FeesForOwnerSymbol0);
        Assert.Equal(feesForOwnerSymbol1, poolResult.FeesForOwnerSymbol1);
    }

    [Fact]
    public void LPHolderInfo_TestConstructor()
    {
        // Arrange
        var nftID = new BigInteger(12903102312);
        var unclaimedSymbol0 = new BigInteger(100);
        var unclaimedSymbol1 = new BigInteger(200);
        var claimedSymbol0 = new BigInteger(300);
        var claimedSymbol1 = new BigInteger(400);

        // Act
        var holderInfo = new LPHolderInfo(nftID, unclaimedSymbol0, unclaimedSymbol1, claimedSymbol0, claimedSymbol1);

        // Assert
        Assert.Equal(nftID, holderInfo.NFTID);
        Assert.Equal(unclaimedSymbol0, holderInfo.UnclaimedSymbol0);
        Assert.Equal(unclaimedSymbol1, holderInfo.UnclaimedSymbol1);
        Assert.Equal(claimedSymbol0, holderInfo.ClaimedSymbol0);
        Assert.Equal(claimedSymbol1, holderInfo.ClaimedSymbol1);
    }
    
    [Fact]
    public void LPHolderInfo_TestSerializeData()
    {
        // Arrange
        var NFTID = new BigInteger(12903102312);
        var unclaimedSymbol0 = new BigInteger(100);
        var unclaimedSymbol1 = new BigInteger(200);
        var claimedSymbol0 = new BigInteger(300);
        var claimedSymbol1 = new BigInteger(400);
        var holderInfo = new LPHolderInfo(NFTID, unclaimedSymbol0, unclaimedSymbol1, claimedSymbol0, claimedSymbol1);

        var bytes = new byte[512];
        var stream = new MemoryStream(bytes);
        var writer = new BinaryWriter(stream);
        var reader = new BinaryReader(stream);
        
        // Act
        holderInfo.SerializeData(writer);
        stream.Position = 0;
        var result = new LPHolderInfo();
        result.UnserializeData(reader);

        // Assert
        Assert.Equal(NFTID, result.NFTID);
        Assert.Equal(unclaimedSymbol0, result.UnclaimedSymbol0);
        Assert.Equal(unclaimedSymbol1, result.UnclaimedSymbol1);
        Assert.Equal(claimedSymbol0, result.ClaimedSymbol0);
        Assert.Equal(claimedSymbol1, result.ClaimedSymbol1);
    }

    [Fact]
    public void LPHolderInfo_TestUnserializeData()
    {
        // Arrange
        var NFTID = new BigInteger(12903102312);
        var unclaimedSymbol0 = new BigInteger(100);
        var unclaimedSymbol1 = new BigInteger(200);
        var claimedSymbol0 = new BigInteger(300);
        var claimedSymbol1 = new BigInteger(400);
        
        var holderInfoSerialize = new LPHolderInfo(NFTID, unclaimedSymbol0, unclaimedSymbol1, claimedSymbol0, claimedSymbol1);
        var data = holderInfoSerialize.Serialize();
        
        var stream = new MemoryStream(data);
        var reader = new BinaryReader(stream);
        
        // Act
        var holderInfo = new LPHolderInfo();
        holderInfo.UnserializeData(reader);
        
        // Assert
        Assert.Equal(NFTID, holderInfo.NFTID);
        Assert.Equal(unclaimedSymbol0, holderInfo.UnclaimedSymbol0);
        Assert.Equal(unclaimedSymbol1, holderInfo.UnclaimedSymbol1);
        Assert.Equal(claimedSymbol0, holderInfo.ClaimedSymbol0);
        Assert.Equal(claimedSymbol1, holderInfo.ClaimedSymbol1);

    }
    
    [Fact]
    public void ExchangeOrder_TestConstructor()
    {
        // Arrange
        var uid = BigInteger.Parse("12345");
        var timestamp = (Timestamp)DateTime.UtcNow;
        var creator = PhantasmaKeys.Generate().Address;
        var provider = PhantasmaKeys.Generate().Address;
        var amount = BigInteger.Parse("1000");
        var baseSymbol = "SOUL";
        var price = BigInteger.Parse("100000");
        var quoteSymbol = "USD";
        var side = ExchangeOrderSide.Buy;
        var type = ExchangeOrderType.Limit;

        // Act
        var order = new ExchangeOrder(uid, timestamp, creator, provider, amount, baseSymbol, price, quoteSymbol, side, type);

        // Assert
        Assert.Equal(uid, order.Uid);
        Assert.Equal(timestamp, order.Timestamp);
        Assert.Equal(creator, order.Creator);
        Assert.Equal(provider, order.Provider);
        Assert.Equal(amount, order.Amount);
        Assert.Equal(baseSymbol, order.BaseSymbol);
        Assert.Equal(price, order.Price);
        Assert.Equal(quoteSymbol, order.QuoteSymbol);
        Assert.Equal(side, order.Side);
        Assert.Equal(type, order.Type);
    }

    [Fact]
    public void ExchangeOrder_TestCopyConstructor()
    {
        // Arrange
        var uid = BigInteger.Parse("12345");
        var timestamp = (Timestamp)DateTime.UtcNow;
        var creator = PhantasmaKeys.Generate().Address;
        var provider = PhantasmaKeys.Generate().Address;
        var amount = BigInteger.Parse("1000");
        var baseSymbol = "ETH";
        var price = BigInteger.Parse("100000");
        var quoteSymbol = "USD";
        var side = ExchangeOrderSide.Buy;
        var type = ExchangeOrderType.Limit;
        var order = new ExchangeOrder(uid, timestamp, creator, provider, amount, baseSymbol, price, quoteSymbol, side,
            type);

        // Act
        var newPrice = BigInteger.Parse("200000");
        var newOrderSize = BigInteger.Parse("2000");
        var updatedOrder = new ExchangeOrder(order, newPrice, newOrderSize);

        // Assert
        Assert.Equal(uid, updatedOrder.Uid);
        Assert.Equal(timestamp, updatedOrder.Timestamp);
        Assert.Equal(creator, updatedOrder.Creator);
        Assert.Equal(provider, updatedOrder.Provider);
        Assert.Equal(newOrderSize, updatedOrder.Amount);
        Assert.Equal(baseSymbol, updatedOrder.BaseSymbol);
        Assert.Equal(newPrice, updatedOrder.Price);
        Assert.Equal(quoteSymbol, updatedOrder.QuoteSymbol);
        Assert.Equal(side, updatedOrder.Side);
        Assert.Equal(type, updatedOrder.Type);
    }

    /*
     * [Theory]
    [InlineData("12345", "0123456789abcdef", "fedcba9876543210", "1000", "SOUL", "100000", "USD", ExchangeOrderSide.Buy, ExchangeOrderType.Limit)]
    [InlineData("67890", "abcdef0123456789", "1032143219876554", "5000", "KCAL", "500000", "EUR", ExchangeOrderSide.Sell, ExchangeOrderType.Market)]
    public void TestConstructor_ValidInput(string uid, string creator, string provider, string amount, string baseSymbol, string price, string quoteSymbol, ExchangeOrderSide side, ExchangeOrderType type)
    {
        // Arrange
        var timestamp = new Timestamp(DateTime.UtcNow);

        // Act
        var order = new ExchangeOrder(BigInteger.Parse(uid), timestamp, new Address(creator), new Address(provider), BigInteger.Parse(amount), baseSymbol, BigInteger.Parse(price), quoteSymbol, side, type);

        // Assert
        Assert.Equal(BigInteger.Parse(uid), order.Uid);
        Assert.Equal(timestamp, order.Timestamp);
        Assert.Equal(new Address(creator), order.Creator);
        Assert.Equal(new Address(provider), order.Provider);
        Assert.Equal(BigInteger.Parse(amount), order.Amount);
        Assert.Equal(baseSymbol, order.BaseSymbol);
        Assert.Equal(BigInteger.Parse(price), order.Price);
        Assert.Equal(quoteSymbol, order.QuoteSymbol);
        Assert.Equal(side, order.Side);
        Assert.Equal(type, order.Type);
    }
     */

}
