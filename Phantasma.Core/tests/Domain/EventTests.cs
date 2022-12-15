using System.IO;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class EventTests
{
    // Write unit tests with xUnit based on the Phantasma.Core.Domain.Event class

    [Fact]
    public void TestEvent()
    {
        var _event = new Event(EventKind.Custom, Address.Null, "test", new byte[0]{});
        Assert.Equal("test", _event.Contract);
        Assert.Equal(0, _event.Data.Length);
        Assert.True(_event.Data.Length == 0);
    }
    
    [Fact]
    public void TestEventFromBytes()
    {
        var _event = new Event(EventKind.Custom, Address.Null, "test", new byte[0]{});
        var bytes = _event.Serialize();
        var stream = new MemoryStream(bytes);
        var reader = new BinaryReader(stream);
        var _event2 = Event.Unserialize(reader);
        Assert.Equal(_event.Address, _event2.Address);
        Assert.Equal(_event.Contract, _event2.Contract);
        Assert.Equal(_event.Data, _event2.Data);
        Assert.Equal(_event.Kind, _event2.Kind);
    }

    [Fact]
    public void TestEventToString()
    {
        var _event = new Event(EventKind.Custom, Address.Null, "test", new byte[0]{});
        var bytes = _event.Serialize();
        var stream = new MemoryStream(bytes);
        var reader = new BinaryReader(stream);
        var _event2 = Event.Unserialize(reader);
        Assert.Equal(_event.ToString(), _event2.ToString());
        Assert.Equal(_event.Contract, _event2.Contract);
        Assert.Equal(_event.Data, _event2.Data);
        Assert.Equal(_event.Kind, _event2.Kind);
    }
    
    [Fact]
    public void TestTransactionSettleEventData()
    {
        byte[] bytesHash = new byte[32];
        var hash = CryptoExtensions.Sha256(bytesHash);
        Hash sourceHash = new Hash(bytesHash);
        var platform = "test";
        var chain = "test";
        var transaction = new TransactionSettleEventData(sourceHash, platform, chain);
        Assert.Equal(transaction.Chain, chain);
        Assert.Equal(transaction.Platform, platform);
        Assert.Equal(transaction.Hash, sourceHash);
    }
    
    [Fact]
    public void TestGasEventData()
    {
        var address = Address.Null;
        var amount = 5;
        var price = 15;
        var gasEventData = new GasEventData(address, price, amount);
        Assert.Equal(gasEventData.address, address);
        Assert.Equal(gasEventData.amount, amount);
        Assert.Equal(gasEventData.price, price);
    }
    
    [Fact]
    public void TestInfusioneventData()
    {
        var baseSymbol = "SOUL";
        var infusionSymbol = "KCAL";
        BigInteger tokenID = 1;
        BigInteger infusionID = 2;
        var chainName = "test";
        var fusionEventData = new InfusionEventData(baseSymbol, tokenID, infusionSymbol, infusionID, chainName);
        
        // Assert
        Assert.Equal(fusionEventData.BaseSymbol, baseSymbol);
        Assert.Equal(fusionEventData.InfusedSymbol, infusionSymbol);
        Assert.Equal(fusionEventData.TokenID, tokenID);
        Assert.Equal(fusionEventData.InfusedValue, infusionID);
        Assert.Equal(fusionEventData.ChainName, chainName);
    }
}
