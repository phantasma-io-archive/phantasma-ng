using System;
using System.Linq;
using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;

using Xunit;

namespace Phantasma.Business.Tests.Blockchain.Contracts.Legacy;

[Collection("SaleContractTest")]
public class SaleContractTest
{
    public uint DefaultEnergyRatioDivisor => StakeContract.DefaultEnergyRatioDivisor;

    public BigInteger StakeToFuel(BigInteger stakeAmount, uint _currentEnergyRatioDivisor)
    {
        return UnitConversion.ConvertDecimals(stakeAmount, DomainSettings.StakingTokenDecimals, DomainSettings.FuelTokenDecimals) / _currentEnergyRatioDivisor;
    }
    
    Address sysAddress;
    PhantasmaKeys user;
    PhantasmaKeys owner;
    Nexus nexus;
    NexusSimulator simulator;
    int amountRequested;
    int gas;
    BigInteger initialAmount;
    BigInteger initialFuel;
    BigInteger startBalance;
    StakeReward reward;

    public SaleContractTest()
    {
        Initialize();
    }

    public void Initialize()
    {
        sysAddress = SmartContract.GetAddressForNative(NativeContractKind.Friends);
        user = PhantasmaKeys.Generate();
        owner = PhantasmaKeys.Generate();
        amountRequested = 100000000;
        gas = 99999;
        initialAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);
        initialFuel = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
        reward = new StakeReward(user.Address, Timestamp.Now);
        InitializeSimulator();

        startBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, user.Address);
    }
        
    protected void InitializeSimulator()
    {
        simulator = new NexusSimulator(owner);
        nexus = simulator.Nexus;
        nexus.SetOracleReader(new OracleSimulator(nexus));
        SetInitialBalance(user.Address);
    }

    protected void SetInitialBalance(Address address)
    {
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.StakingTokenSymbol, initialAmount);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
    }
    
    [Fact]
    public void TestSale()
    {
        var saleUser = PhantasmaKeys.Generate();
        var saleBuyer = PhantasmaKeys.Generate();
        var otherSaleBuyer = PhantasmaKeys.Generate();

        var stakeAmount = UnitConversion.ToBigInteger(1000, DomainSettings.StakingTokenDecimals);
        double realStakeAmount = ((double)stakeAmount) * Math.Pow(10, -DomainSettings.StakingTokenDecimals);
        double realExpectedUnclaimedAmount = ((double)(StakeToFuel(stakeAmount, DefaultEnergyRatioDivisor))) * Math.Pow(10, -DomainSettings.FuelTokenDecimals);

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, saleUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, saleUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);

        simulator.GenerateTransfer(owner, saleBuyer.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, saleBuyer.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);

        simulator.GenerateTransfer(owner, otherSaleBuyer.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, otherSaleBuyer.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
        simulator.EndBlock();

        var saleSymbol = "DANK";
        var decimals = 18;
        var supply = UnitConversion.ToBigInteger(2000000, decimals);

        simulator.BeginBlock();
        simulator.GenerateToken(owner, saleSymbol, "Dank Token", supply, decimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Finite);
        simulator.EndBlock();

        simulator.BeginBlock();
        simulator.MintTokens(owner, saleUser.Address, saleSymbol, supply);
        simulator.EndBlock();

        var oldSellerBalance = nexus.RootChain.GetTokenBalance(nexus.RootStorage, "SOUL", saleUser.Address);

        var saleRate = 3;

        simulator.BeginBlock();
        var tx = simulator.GenerateCustomTransaction(saleUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(saleUser.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Sale, nameof(SaleContract.CreateSale), saleUser.Address, "Dank pre-sale", SaleFlags.Whitelist, (Timestamp) simulator.CurrentTime, (Timestamp)( simulator.CurrentTime + TimeSpan.FromDays(2)), saleSymbol, "SOUL", saleRate, 0, supply, 0, UnitConversion.ToBigInteger(1500/saleRate, decimals)).
                SpendGas(saleUser.Address).EndScript());
        var block = simulator.EndBlock().First();

        var resultBytes = block.GetResultForTransaction(tx.Hash);
        var resultObj = Serialization.Unserialize<VMObject>(resultBytes);
        var saleHash = resultObj.AsInterop<Hash>();

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(saleUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(saleUser.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Sale, nameof(SaleContract.AddToWhitelist), saleHash, saleBuyer.Address)
                .CallContract(NativeContractKind.Sale, nameof(SaleContract.AddToWhitelist), saleHash, otherSaleBuyer.Address).
                SpendGas(saleUser.Address).EndScript());
        simulator.EndBlock().First();

        var purchaseAmount = UnitConversion.ToBigInteger(50, DomainSettings.StakingTokenDecimals);
        BigInteger expectedAmount = 0;

        var baseToken = nexus.GetTokenInfo(nexus.RootStorage, "SOUL");
        var quoteToken = nexus.GetTokenInfo(nexus.RootStorage, saleSymbol);

        for (int i=1; i<=3; i++)
        {
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(saleBuyer, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(saleBuyer.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                    .CallContract(NativeContractKind.Sale, nameof(SaleContract.Purchase), saleBuyer.Address, saleHash, "SOUL", purchaseAmount).
                    SpendGas(saleBuyer.Address).EndScript());
            simulator.EndBlock().First();

            expectedAmount += saleRate * DomainExtensions.ConvertBaseToQuote(null, purchaseAmount, UnitConversion.GetUnitValue(decimals), baseToken, quoteToken);

            resultObj = simulator.InvokeContract(NativeContractKind.Sale, nameof(SaleContract.GetSoldAmount), saleHash);
            var raisedAmount = resultObj.AsNumber();

            Assert.True(raisedAmount == expectedAmount);

            resultObj = simulator.InvokeContract(NativeContractKind.Sale, nameof(SaleContract.GetPurchasedAmount), saleHash, saleBuyer.Address);
            var purchasedAmount = resultObj.AsNumber();

            Assert.True(purchasedAmount == expectedAmount);
        }

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(saleBuyer, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(saleBuyer.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Sale, nameof(SaleContract.Purchase), saleBuyer.Address, saleHash, "SOUL", purchaseAmount).
                SpendGas(saleBuyer.Address).EndScript());
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());


        var otherPurchaseAmount = UnitConversion.ToBigInteger(150, DomainSettings.StakingTokenDecimals);

        {
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(otherSaleBuyer, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(otherSaleBuyer.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                    .CallContract(NativeContractKind.Sale, nameof(SaleContract.Purchase), otherSaleBuyer.Address, saleHash, "SOUL", otherPurchaseAmount).
                    SpendGas(otherSaleBuyer.Address).EndScript());
            simulator.EndBlock().First();

            expectedAmount += saleRate * DomainExtensions.ConvertBaseToQuote(null, otherPurchaseAmount, UnitConversion.GetUnitValue(decimals), baseToken, quoteToken);

            resultObj = simulator.InvokeContract(NativeContractKind.Sale, nameof(SaleContract.GetSoldAmount), saleHash);
            var raisedAmount = resultObj.AsNumber();

            Assert.True(raisedAmount == expectedAmount);
        }

        simulator.TimeSkipDays(4);

        resultObj = simulator.InvokeContract(NativeContractKind.Sale, nameof(SaleContract.GetSoldAmount), saleHash);
        var totalSoldAmount = resultObj.AsNumber();

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(saleBuyer, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(saleBuyer.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Sale, nameof(SaleContract.CloseSale), saleBuyer.Address, saleHash).
                SpendGas(saleBuyer.Address).EndScript());
        simulator.EndBlock().First();

        var buyerBalance = nexus.RootChain.GetTokenBalance(nexus.RootStorage, saleSymbol, saleBuyer.Address);
        BigInteger expectedBalance = 3 * saleRate * DomainExtensions.ConvertBaseToQuote(null, purchaseAmount, UnitConversion.GetUnitValue(decimals), baseToken, quoteToken);
        //Assert.True(buyerBalance == expectedBalance);

        var otherBuyerBalance = nexus.RootChain.GetTokenBalance(nexus.RootStorage, saleSymbol, saleBuyer.Address);
        expectedBalance = saleRate * DomainExtensions.ConvertBaseToQuote(null, otherPurchaseAmount, UnitConversion.GetUnitValue(decimals), baseToken, quoteToken);
        //Assert.True(otherBuyerBalance == expectedBalance);

        var newSellerBalance = nexus.RootChain.GetTokenBalance(nexus.RootStorage, "SOUL", saleUser.Address);

        var totalRaisedAmount = DomainExtensions.ConvertQuoteToBase(null, totalSoldAmount, UnitConversion.GetUnitValue(decimals), baseToken, quoteToken) / saleRate;

        expectedBalance = oldSellerBalance + totalRaisedAmount;
        Assert.True(newSellerBalance == expectedBalance);
    }
}
