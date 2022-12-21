using System.Collections.Generic;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class DomainSettingsTests
{
    [Fact]
    public void TestStakeReward()
    {
        var settings = new StakeReward();
        
        Assert.Equal(settings.staker, Address.Null);
        Assert.Equal(settings.date, Timestamp.Null);
    }
    
    [Fact]
    public void TestStakeRewardValues()
    {
        var address = PhantasmaKeys.Generate().Address;
        var date = Timestamp.Now;
        var settings = new StakeReward(address, date);
        
        Assert.Equal(address, settings.staker);
        Assert.Equal(date, settings.date);
    }
    
    [Fact]
    public void TestSystemTokens()
    {
        var systemTokens = new List<string>
        {
            DomainSettings.FuelTokenSymbol,
            DomainSettings.StakingTokenSymbol,
            DomainSettings.FiatTokenSymbol,
            DomainSettings.RewardTokenSymbol,
            DomainSettings.LiquidityTokenSymbol
        };
        
        Assert.Equal(systemTokens, DomainSettings.SystemTokens);
    }
    
    [Fact]
    public void TestPlatformSupply()
    {
        var platformSupply = UnitConversion.ToBigInteger(100000000, DomainSettings.FuelTokenDecimals);
        
        Assert.Equal(platformSupply, DomainSettings.PlatformSupply);
    }
    
    [Fact]
    public void TestArchiveMinSize()
    {
        Assert.Equal(64, DomainSettings.ArchiveMinSize);
    }
    
    [Fact]
    public void TestArchiveMaxSize()
    {
        Assert.Equal(104857600, DomainSettings.ArchiveMaxSize);
    }
    
    [Fact]
    public void TestArchiveBlockSize()
    {
        Assert.Equal(MerkleTree.ChunkSize, DomainSettings.ArchiveBlockSize);
    }
    
    [Fact]
    public void TestInfusionName()
    {
        Assert.Equal("infusion", DomainSettings.InfusionName);
    }
    
    [Fact]
    public void TestInfusionAddress()
    {
        var infusionAddress = SmartContract.GetAddressFromContractName("infusion");
        
        Assert.Equal(infusionAddress, DomainSettings.InfusionAddress);
    }
    
    [Fact]
    public void TestFuelTokenName()
    {
        Assert.Equal("Phantasma Energy", DomainSettings.FuelTokenName);
    }
    
    [Fact]
    public void TestFuelTokenSymbol()
    {
        Assert.Equal("KCAL", DomainSettings.FuelTokenSymbol);
    }
    
    [Fact]
    public void TestFuelTokenDecimals()
    {
        Assert.Equal(10, DomainSettings.FuelTokenDecimals);
    }
    
    [Fact]
    public void TestStakingTokenName()
    {
        Assert.Equal("Phantasma Stake", DomainSettings.StakingTokenName);
    }
    
    [Fact]
    public void TestStakingTokenSymbol()
    {
        Assert.Equal("SOUL", DomainSettings.StakingTokenSymbol);
    }
    
    [Fact]
    public void TestStakingTokenDecimals()
    {
        Assert.Equal(8, DomainSettings.StakingTokenDecimals);
    }
    
    [Fact]
    public void TestFiatTokenName()
    {
        Assert.Equal("Dollars", DomainSettings.FiatTokenName);
    }
    
    [Fact]
    public void TestFiatTokenSymbol()
    {
        Assert.Equal("USD", DomainSettings.FiatTokenSymbol);
    }
    
    [Fact]
    public void TestFiatTokenDecimals()
    {
        Assert.Equal(8, DomainSettings.FiatTokenDecimals);
    }

}
