using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Serializer;
using Phantasma.Core.Types;
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
        Assert.Equal(_event.ToString(), _event2.ToString());
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
    
    [Fact]
    //string symbol, BigInteger value, string chainName, Timestamp claimDate
    public void TestMasterEventData()
    {
        var symbol = "SOUL";
        BigInteger value = 1;
        var chainName = "test";
        var claimDate = Timestamp.Now;
        var masterEventData = new MasterEventData(symbol, value, chainName, claimDate);
        
        // Assert
        Assert.Equal(masterEventData.Symbol, symbol);
        Assert.Equal(masterEventData.Value, value);
        Assert.Equal(masterEventData.ChainName, chainName);
        Assert.Equal(masterEventData.ClaimDate, claimDate);
    }

    [Fact]
    //OrganizationEventData(string organization, Address memberAddress)
    public void TestOrganizationEventData()
    {
        var organization = "test";
        var memberAddress = Address.Null;
        var organizationEventData = new OrganizationEventData(organization, memberAddress);

        // Assert
        Assert.Equal(organizationEventData.Organization, organization);
        Assert.Equal(organizationEventData.MemberAddress, memberAddress);
    }
    
    [Fact]
    public void TestTokenEventData()
    {
        var symbol = "SOUL";
        BigInteger value = 1;
        var chainName = "test";
        var tokenEventData = new TokenEventData(symbol, value, chainName);
        
        // Assert
        Assert.Equal(tokenEventData.Symbol, symbol);
        Assert.Equal(tokenEventData.Value, value);
        Assert.Equal(tokenEventData.ChainName, chainName);
    }
    
    [Fact]
    public void CompareEventsTest()
    {
        var _event = new Event(EventKind.Custom, Address.Null, "test", new byte[0]{});
        var _event2 = new Event(EventKind.Custom, Address.Null, "test", new byte[0]{});
        Assert.Equal(_event, _event2);
    }

    [Fact]
    public void CompareMultipleEventsTests()
    {
        var events = new List<Event>();
        var events_list2 = new List<Event>();
        var _event = new Event(EventKind.Custom, Address.Null, "test", new byte[0]{});
        var _event2 = new Event(EventKind.Custom, Address.Null, "test", new byte[0]{});
        events.Add(_event);
        events.Add(_event2);
        events_list2.Add(_event);
        events_list2.Add(_event2);
        Assert.Equal(events[0], events[1]);
        Assert.Equal(events_list2[0], events_list2[1]);
        Assert.Equal(events, events_list2);
        Assert.True(events.Except(events_list2).Count() == 0 && events_list2.Except(events).Count() == 0);
    }
    
}
