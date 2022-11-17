using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Simulator;

namespace Phantasma.LegacyTests.ContractTests;

[TestClass]
public class ExchangeContractTests
{
    private const string maxDivTokenSymbol = "MADT";        //divisible token with maximum decimal count
    private const string minDivTokenSymbol = "MIDT";        //divisible token with minimum decimal count
    private const string nonDivisibleTokenSymbol = "NDT";

    #region Exchange
    [Ignore]
    [TestMethod]
    public void TestIoCLimitMinimumQuantity()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
        seller.FundBaseToken(quantity: 2m, fundFuel: true);

        //-----------------------------------------
        //test order amount and prices at the limit

        var minimumBaseToken = UnitConversion.ToDecimal(core.simulator.Nexus.RootChain.InvokeContract(core.simulator.Nexus.RootStorage, "exchange", "GetMinimumTokenQuantity", buyer.baseToken).AsNumber(), buyer.baseToken.Decimals);
        var minimumQuoteToken = UnitConversion.ToDecimal(core.simulator.Nexus.RootChain.InvokeContract(core.simulator.Nexus.RootStorage, "exchange", "GetMinimumTokenQuantity", buyer.quoteToken).AsNumber(), buyer.baseToken.Decimals);

        buyer.OpenLimitOrder(minimumBaseToken, minimumQuoteToken, ExchangeOrderSide.Buy, IoC: true);
        seller.OpenLimitOrder(minimumBaseToken, minimumQuoteToken, ExchangeOrderSide.Sell, IoC: true);

        buyer.OpenLimitOrder(1m, 1m, ExchangeOrderSide.Buy);
        buyer.OpenLimitOrder(1m, 1m, ExchangeOrderSide.Buy);
        Assert.IsTrue(seller.OpenLimitOrder(1m + (minimumBaseToken*.99m), 1m, ExchangeOrderSide.Sell) == 1m, "Used leftover under minimum quantity");
    }

    [Ignore]
    [TestMethod]
    public void TestIoCLimitOrderUnmatched()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
        seller.FundBaseToken(quantity: 2m, fundFuel: true);

        //-----------------------------------------
        //test unmatched IoC orders 
        seller.OpenLimitOrder(0.01m, 0.5m, ExchangeOrderSide.Sell);
        buyer.OpenLimitOrder(0.01m, 0.1m, ExchangeOrderSide.Buy);
        Assert.IsTrue(buyer.OpenLimitOrder(0.123m, 0.3m, ExchangeOrderSide.Buy, IoC: true) == 0, "Shouldn't have filled any part of the order");
        Assert.IsTrue(seller.OpenLimitOrder(0.123m, 0.3m, ExchangeOrderSide.Sell, IoC: true) == 0, "Shouldn't have filled any part of the order");
    }

    [Ignore]
    [TestMethod]
    public void TestIoCLimitOrderCompleteFulfilment()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
        seller.FundBaseToken(quantity: 2m, fundFuel: true);

        //-----------------------------------------
        //test fully matched IoC orders
        buyer.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Buy, IoC: false);
        Assert.IsTrue(seller.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Sell, IoC: true) == 0.1m, "Unexpected amount of tokens received");

        seller.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Sell, IoC: false);
        Assert.IsTrue(buyer.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Buy, IoC: true) == 0.1m, "Unexpected amount of tokens received");
    }

    [Ignore]
    [TestMethod]
    public void TestIoCLimitOrderPartialFulfilment()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
        seller.FundBaseToken(quantity: 2m, fundFuel: true);

        //-----------------------------------------
        //test partially matched IoC orders
        buyer.OpenLimitOrder(0.05m, 1m, ExchangeOrderSide.Buy, IoC: false);
        Assert.IsTrue(seller.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Sell, IoC: true) == 0.05m, "Unexpected amount of tokens received");

        seller.OpenLimitOrder(0.05m, 1m, ExchangeOrderSide.Sell, IoC: false);
        Assert.IsTrue(buyer.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Buy, IoC: true) == 0.05m, "Unexpected amount of tokens received");
    }

    [Ignore]
    [TestMethod]
    public void TestIoCLimitOrderMultipleFulfilsPerOrder()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
        seller.FundBaseToken(quantity: 2m, fundFuel: true);

        //-----------------------------------------
        //test multiple fills per order
        buyer.OpenLimitOrder(0.05m, 1m, ExchangeOrderSide.Buy, IoC: false);
        buyer.OpenLimitOrder(0.05m, 2m, ExchangeOrderSide.Buy, IoC: false);
        buyer.OpenLimitOrder(0.05m, 3m, ExchangeOrderSide.Buy, IoC: false);
        buyer.OpenLimitOrder(0.05m, 0.5m, ExchangeOrderSide.Buy, IoC: false);
        Assert.IsTrue(seller.OpenLimitOrder(0.15m, 1m, ExchangeOrderSide.Sell, IoC: true) == 0.3m, "Unexpected amount of tokens received");

        core = new CoreClass();
        buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        seller = new ExchangeUser(baseSymbol, quoteSymbol, core);
        buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
        seller.FundBaseToken(quantity: 2m, fundFuel: true);

        seller.OpenLimitOrder(0.05m, 1m, ExchangeOrderSide.Sell, IoC: false);
        seller.OpenLimitOrder(0.05m, 2m, ExchangeOrderSide.Sell, IoC: false);
        seller.OpenLimitOrder(0.05m, 3m, ExchangeOrderSide.Sell, IoC: false);
        seller.OpenLimitOrder(0.05m, 0.5m, ExchangeOrderSide.Sell, IoC: false);
        Assert.IsTrue(buyer.OpenLimitOrder(0.15m, 3m, ExchangeOrderSide.Buy, IoC: true) == 0.2m, "Unexpected amount of tokens received");

        //TODO: test multiple IoC orders against each other on the same block!
    }

    [Ignore]
    [TestMethod]
    public void TestFailedIOC()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
        seller.FundBaseToken(quantity: 2m, fundFuel: true);

        //-----------------------------------------
        //test order amount and prices below limit
        buyer.OpenLimitOrder(0, 0.5m, ExchangeOrderSide.Buy, IoC: true);
        Assert.IsTrue(false, "Order should fail due to insufficient amount");
        
        try
        {
            buyer.OpenLimitOrder(0.5m, 0, ExchangeOrderSide.Buy, IoC: true);
            Assert.IsTrue(false, "Order should fail due to insufficient price");
        }
        catch (Exception e) { }

        Assert.IsTrue(buyer.OpenLimitOrder(0.123m, 0.3m, ExchangeOrderSide.Buy, IoC: true) == 0, "Shouldn't have filled any part of the order");
        Assert.IsTrue(seller.OpenLimitOrder(0.123m, 0.3m, ExchangeOrderSide.Sell, IoC: true) == 0, "Shouldn't have filled any part of the order");
    }

    [Ignore]
    [TestMethod]
    public void TestLimitMinimumQuantity()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
        seller.FundBaseToken(quantity: 2m, fundFuel: true);

        //-----------------------------------------
        //test order amount and prices at the limit

        var minimumBaseToken = UnitConversion.ToDecimal(core.simulator.Nexus.RootChain.InvokeContract(core.simulator.Nexus.RootStorage, "exchange", "GetMinimumTokenQuantity", buyer.baseToken).AsNumber(), buyer.baseToken.Decimals);
        var minimumQuoteToken = UnitConversion.ToDecimal(core.simulator.Nexus.RootChain.InvokeContract(core.simulator.Nexus.RootStorage, "exchange", "GetMinimumTokenQuantity", buyer.quoteToken).AsNumber(), buyer.baseToken.Decimals);

        buyer.OpenLimitOrder(minimumBaseToken, minimumQuoteToken, ExchangeOrderSide.Buy);
        seller.OpenLimitOrder(minimumBaseToken, minimumQuoteToken, ExchangeOrderSide.Sell);
    }

    [Ignore]
    [TestMethod]
    public void TestLimitOrderUnmatched()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
        seller.FundBaseToken(quantity: 2m, fundFuel: true);

        //-----------------------------------------
        //test unmatched IoC orders 
        seller.OpenLimitOrder(0.01m, 0.5m, ExchangeOrderSide.Sell);
        buyer.OpenLimitOrder(0.01m, 0.1m, ExchangeOrderSide.Buy);
        Assert.IsTrue(buyer.OpenLimitOrder(0.123m, 0.3m, ExchangeOrderSide.Buy, IoC: true) == 0, "Shouldn't have filled any part of the order");
        Assert.IsTrue(seller.OpenLimitOrder(0.123m, 0.3m, ExchangeOrderSide.Sell, IoC: true) == 0, "Shouldn't have filled any part of the order");
    }

    [Ignore]
    [TestMethod]
    public void TestLimitOrderCompleteFulfilment()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
        seller.FundBaseToken(quantity: 2m, fundFuel: true);

        //-----------------------------------------
        //test fully matched IoC orders
        buyer.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Buy, IoC: false);
        Assert.IsTrue(seller.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Sell, IoC: true) == 0.1m, "Unexpected amount of tokens received");

        seller.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Sell, IoC: false);
        Assert.IsTrue(buyer.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Buy, IoC: true) == 0.1m, "Unexpected amount of tokens received");
    }

    [Ignore]
    [TestMethod]
    public void TestLimitOrderPartialFulfilment()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
        seller.FundBaseToken(quantity: 2m, fundFuel: true);

        //-----------------------------------------
        //test partially matched IoC orders
        buyer.OpenLimitOrder(0.05m, 1m, ExchangeOrderSide.Buy, IoC: false);
        Assert.IsTrue(seller.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Sell, IoC: true) == 0.05m, "Unexpected amount of tokens received");

        seller.OpenLimitOrder(0.05m, 1m, ExchangeOrderSide.Sell, IoC: false);
        Assert.IsTrue(buyer.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Buy, IoC: true) == 0.05m, "Unexpected amount of tokens received");
    }

    [Ignore]
    [TestMethod]
    public void TestLimitOrderMultipleFulfilsPerOrder()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
        seller.FundBaseToken(quantity: 2m, fundFuel: true);

        //-----------------------------------------
        //test multiple fills per order
        buyer.OpenLimitOrder(0.05m, 1m, ExchangeOrderSide.Buy, IoC: false);
        buyer.OpenLimitOrder(0.05m, 2m, ExchangeOrderSide.Buy, IoC: false);
        buyer.OpenLimitOrder(0.05m, 3m, ExchangeOrderSide.Buy, IoC: false);
        buyer.OpenLimitOrder(0.05m, 0.5m, ExchangeOrderSide.Buy, IoC: false);
        Assert.IsTrue(seller.OpenLimitOrder(0.15m, 1m, ExchangeOrderSide.Sell, IoC: true) == 0.3m, "Unexpected amount of tokens received");

        buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
        seller.FundBaseToken(quantity: 2m, fundFuel: true);

        seller.OpenLimitOrder(0.05m, 1m, ExchangeOrderSide.Sell, IoC: false);
        seller.OpenLimitOrder(0.05m, 2m, ExchangeOrderSide.Sell, IoC: false);
        seller.OpenLimitOrder(0.05m, 3m, ExchangeOrderSide.Sell, IoC: false);
        seller.OpenLimitOrder(0.05m, 0.5m, ExchangeOrderSide.Sell, IoC: false);
        Assert.IsTrue(buyer.OpenLimitOrder(0.15m, 3m, ExchangeOrderSide.Buy, IoC: true) == 0.2m, "Unexpected amount of tokens received");

        //TODO: test multiple IoC orders against each other on the same block!
    }

    [Ignore]
    [TestMethod]
    public void TestFailedRegular()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
        seller.FundBaseToken(quantity: 2m, fundFuel: true);

        //-----------------------------------------
        //test order amount and prices below limit
        try
        {
            buyer.OpenLimitOrder(0, 0.5m, ExchangeOrderSide.Buy);
            Assert.IsTrue(false, "Order should fail due to insufficient amount");
        }
        catch (Exception e) { }
        try
        {
            buyer.OpenLimitOrder(0.5m, 0, ExchangeOrderSide.Buy);
            Assert.IsTrue(false, "Order should fail due to insufficient price");
        }
        catch (Exception e) { }
    }

    [TestMethod]
    public void TestEmptyBookMarketOrder()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
        seller.FundBaseToken(quantity: 2m, fundFuel: true);

        Assert.IsTrue(buyer.OpenMarketOrder(1, ExchangeOrderSide.Buy) == 0, "Should not have bought anything");
    }

    [Ignore]
    [TestMethod]
    public void TestMarketOrderPartialFill()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
        seller.FundBaseToken(quantity: 2m, fundFuel: true);

        seller.OpenLimitOrder(0.2m, 1m, ExchangeOrderSide.Sell);
        Assert.IsTrue(buyer.OpenMarketOrder(0.3m, ExchangeOrderSide.Buy) == 0.2m, "");
    }

    [TestMethod]
    public void TestMarketOrderCompleteFulfilment()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
        seller.FundBaseToken(quantity: 2m, fundFuel: true);

        seller.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Sell);
        seller.OpenLimitOrder(0.1m, 2m, ExchangeOrderSide.Sell);
        Assert.IsTrue(buyer.OpenMarketOrder(0.3m, ExchangeOrderSide.Buy) == 0.2m, "");
    }

    [TestMethod]
    public void TestMarketOrderTotalFillNoOrderbookWipe()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
        seller.FundBaseToken(quantity: 2m, fundFuel: true);

        seller.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Sell);
        seller.OpenLimitOrder(0.1m, 2m, ExchangeOrderSide.Sell);
        Assert.IsTrue(buyer.OpenMarketOrder(0.25m, ExchangeOrderSide.Buy) == 0.175m, "");
    }
    
    #endregion
    
    #region OTC Tests
    [TestMethod, TestCategory("OTC")]
    public void TestOpenOTCOrder()
    {
        CoreClass core = new CoreClass();

        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        // Create users
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);
        

        // Give Users tokens
        seller.FundUser(soul: 500, kcal: 100);
        

        // Get Initial Balance
        var initialBalance = seller.GetBalance(baseSymbol);

        // Verify my Funds
        Assert.IsTrue(initialBalance == UnitConversion.ToBigInteger(500, GetDecimals(baseSymbol)));

        // Create OTC Offer
        var txValue = seller.OpenOTCOrder(baseSymbol, quoteSymbol, 1m, 2m);

        // Test if the seller lost money.
        var finalBalance = seller.GetBalance(baseSymbol);

        Assert.IsFalse(initialBalance == finalBalance, $"{initialBalance} == {finalBalance}");

        // Test if lost the quantity used
        var subtractSpendToken = initialBalance - UnitConversion.ToBigInteger(2m, GetDecimals(baseSymbol));
        Assert.IsTrue(subtractSpendToken == finalBalance, $"{subtractSpendToken} == {finalBalance}");
    }

    [TestMethod, TestCategory("OTC")]
    public void TestGetOTC()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        // Create users
        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        // Give Users tokens
        buyer.FundUser(soul: 5000m, kcal: 5000m);
        seller.FundUser(soul: 5000m, kcal: 5000m);

        // Test Empty OTC
        var initialOTC = seller.GetOTC();

        var empytOTC = new ExchangeOrder[0];

        Assert.IsTrue(initialOTC.Length == 0);

        // Create an Order
        seller.OpenOTCOrder(baseSymbol, quoteSymbol, 1m, 1m);

        // Test if theres an order
        var finallOTC = seller.GetOTC();

        Assert.IsTrue(initialOTC != finallOTC);
    }


    [TestMethod, TestCategory("OTC")]
    public void TestTakeOTCOrder()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        // Create users
        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        // Give Users tokens
        buyer.FundUser(soul: 500, kcal: 50);
        seller.FundUser(soul: 500, kcal: 50);

        // Get Initial Balance
        var initialBuyer_B = buyer.GetBalance(baseSymbol);
        var initialBuyer_Q = buyer.GetBalance(quoteSymbol);
        var initialSeller_B = seller.GetBalance(baseSymbol);
        var initialSeller_Q = seller.GetBalance(quoteSymbol);

        // Create Order
        var sellerTXFees = seller.OpenOTCOrder(baseSymbol, quoteSymbol, 5, 10);

        // Test if Seller lost balance
        var finalSeller_B = seller.GetBalance(baseSymbol);

        Assert.IsFalse(initialSeller_B == finalSeller_B);

        // Test if lost the quantity used
        Assert.IsTrue((initialSeller_B - UnitConversion.ToBigInteger(10m, GetDecimals(baseSymbol))) == finalSeller_B);

        // Take an Order
        // Get Order UID
        var orderUID = seller.GetOTC().First<ExchangeOrder>().Uid;
        var buyerTXFees = buyer.TakeOTCOrder(orderUID);

        // Check if the order is taken
        var finalSeller_Q = seller.GetBalance(quoteSymbol);
        var finalBuyer_B = buyer.GetBalance(baseSymbol);
        var finalBuyer_Q = buyer.GetBalance(quoteSymbol);

        // Consider Transactions Fees

        // Test seller received
        Assert.IsTrue((initialSeller_Q + UnitConversion.ToBigInteger(5m, GetDecimals(quoteSymbol)) - sellerTXFees) == finalSeller_Q);

        // Test Buyer spend and receibed
        Assert.IsTrue((initialBuyer_B + UnitConversion.ToBigInteger(10m, GetDecimals(baseSymbol))) == finalBuyer_B);
        Assert.IsTrue((initialBuyer_Q - UnitConversion.ToBigInteger(5m, GetDecimals(quoteSymbol)) - buyerTXFees) == finalBuyer_Q);

    }

    [TestMethod, TestCategory("OTC")]
    public void TestCancelOTCOrder()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        // Create users
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        // Give Users tokens
        seller.FundUser(soul: 500, kcal: 50);

        // Get Initial Balance
        var initialBalance = seller.GetBalance(baseSymbol);

        // Create OTC Offer
        seller.OpenOTCOrder(baseSymbol, quoteSymbol, 1m, 5m);

        // Test if the seller lost money.
        var finalBalance = seller.GetBalance(baseSymbol);

        Assert.IsFalse(initialBalance == finalBalance);

        // Test if lost the quantity used
        Assert.IsTrue((initialBalance - UnitConversion.ToBigInteger(5m, GetDecimals(baseSymbol))) == finalBalance);

        // Cancel Order
        // Get Order UID
        var orderUID = seller.GetOTC().First<ExchangeOrder>().Uid;
        seller.CancelOTCOrder(orderUID);

        // Test if the token is back;
        var atualBalance = seller.GetBalance(baseSymbol);

        Assert.IsTrue(initialBalance == atualBalance);
    }
    #endregion

    #region DEX
    // Token Values
    static string poolSymbol0 = DomainSettings.StakingTokenSymbol;
    static BigInteger poolAmount0 = UnitConversion.ToBigInteger(50000, 8);
    static string poolSymbol1 = DomainSettings.FuelTokenSymbol;
    static BigInteger poolAmount1 = UnitConversion.ToBigInteger(160000, 10);
    static string poolSymbol2 = "ETH";
    static BigInteger poolAmount2 = UnitConversion.ToBigInteger(50, 18);
    static string poolSymbol3 = "BNB";
    static BigInteger poolAmount3 = UnitConversion.ToBigInteger(100, 18);
    static string poolSymbol4 = "NEO";
    static BigInteger poolAmount4 = UnitConversion.ToBigInteger(500, 0);
    static string poolSymbol5 = "GAS";
    static BigInteger poolAmount5 = UnitConversion.ToBigInteger(600, 8);

    // Virtual Token
    static string virtualPoolSymbol = "COOL";
    static BigInteger virtualPoolAmount1 = UnitConversion.ToBigInteger(10000000, 10);
    
    private static Address SwapAddress = SmartContract.GetAddressForNative(NativeContractKind.Swap);
    
    private void SetupNormalPool()
    {
        CoreClass core = new CoreClass();

        // Create users
        var poolOwner = new ExchangeUser(DomainSettings.StakingTokenSymbol, DomainSettings.FuelTokenSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 500, kcal: 100);

        // SOUL / KCAL
        poolOwner.CreatePool(poolSymbol0, poolAmount0, poolSymbol1, poolAmount1);

        // SOUL / ETH
        poolOwner.CreatePool(poolSymbol0, poolAmount0, poolSymbol2, poolAmount2);

        // SOUL / NEO
        poolOwner.CreatePool(poolSymbol0, poolAmount0, poolSymbol4, poolAmount4);

        // SOUL / GAS
        poolOwner.CreatePool(poolSymbol0, poolAmount0, poolSymbol5, poolAmount5);
    }

    private void SetupVirtualPool()
    {
        CoreClass core = new CoreClass();

        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 500, kcal: 100);

        // KCAL / VIRTUAL
        poolOwner.CreatePool(poolSymbol1, poolAmount1, virtualPoolSymbol, virtualPoolAmount1);
    }

    private void CreatePools()
    {
        SetupNormalPool();
    }

    [TestMethod]
    public void MigrateTest()
    {
        CoreClass core = new CoreClass(true);


        core.Migrate();

        // Check pools
        // TODO: Checks
    }


    [TestMethod]
    public void CreatePool()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 500, kcal: 100);
        poolOwner.Fund(poolSymbol2, poolAmount2);
        poolOwner.Fund(poolSymbol4, poolAmount4);
        poolOwner.Fund(poolSymbol5, poolAmount5);

        // KCAL / VIRTUAL

        string symbol0 = "SOUL";
        string symbol1 = "COOL";
        BigInteger myPoolAmount0 = UnitConversion.ToBigInteger(10000, 8);
        BigInteger myPoolAmount1 = UnitConversion.ToBigInteger(100000, 8);

        double am0 = (double)myPoolAmount0;
        double am1 = (double)myPoolAmount1;
        BigInteger totalLiquidity = (BigInteger)Math.Sqrt(am0 * am0 / 3);

        // Setup a test user 
        byte[] tokenScript = null;
        
        //simulator.BeginBlock();
        //simulator.GenerateToken(owner, symbol2, "Mankini Token", UnitConversion.ToBigInteger(communitySupply, 0), 0, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite);
        //simulator.MintTokens(owner, owner.Address, symbol2, communitySupply);
        //simulator.EndBlock();
        
        /*
         * simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol1, "Cool Token", myPoolAmount1 * 10, 8,  TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible, tokenScript);
            simulator.MintTokens(owner, owner.Address, poolSymbol1, poolAmount1 * 10);
            simulator.MintTokens(owner, owner.Address, symbol0, myPoolAmount0 * 10);
            simulator.MintTokens(owner, owner.Address, symbol1, myPoolAmount1 * 10);
            simulator.EndBlock();
         */

        // Get Tokens Info
        //token0
        var token0 = core.nexus.GetTokenInfo(core.nexus.RootStorage, symbol0);
        var token0Address = core.nexus.GetTokenContract(core.nexus.RootStorage, symbol0);
        Assert.IsTrue(token0.Symbol == symbol0);

        // token1
        var token1 = core.nexus.GetTokenInfo(core.nexus.RootStorage, symbol1);
        var token1Address = core.nexus.GetTokenContract(core.nexus.RootStorage, symbol1);
        Assert.IsTrue(token1.Symbol == symbol1);
        Assert.IsTrue(token1.Flags.HasFlag(TokenFlags.Transferable), "Not swappable.");

        // Create a Pool
        poolOwner.CreatePool(symbol0, myPoolAmount0, symbol1, 0);

        var pool = poolOwner.GetPool(symbol0, symbol1);

        Assert.IsTrue(pool.Symbol0 == symbol0, "Symbol0 doesn't check");
        Assert.IsTrue(pool.Amount0 == myPoolAmount0, $"Amount0 doesn't check {pool.Amount0}");
        Assert.IsTrue(pool.Symbol1 == symbol1, "Symbol1 doesn't check");
        Assert.IsTrue(pool.Amount1 == myPoolAmount0 / 3, $"Amount1 doesn't check {pool.Amount1}");
        Assert.IsTrue(pool.TotalLiquidity == totalLiquidity, "Liquidity doesn't check"); 
        Assert.IsTrue(pool.Symbol0Address == token0Address.Address.Text);
        Assert.IsTrue(pool.Symbol1Address == token1Address.Address.Text);

        Console.WriteLine($"Check Values | {pool.Symbol0}({pool.Symbol0Address}) -> {pool.Amount0} | {pool.Symbol1}({pool.Symbol1Address}) -> {pool.Amount1} || {pool.TotalLiquidity}");
    }

    [TestMethod]
    [Ignore]
    public void CreateVirtualPool()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 500, kcal: 100);
        poolOwner.Fund(poolSymbol2, poolAmount2);
        poolOwner.Fund(poolSymbol4, poolAmount4);
        poolOwner.Fund(poolSymbol5, poolAmount5);

        BigInteger totalLiquidity = (BigInteger)Math.Sqrt((long)(poolAmount1 * virtualPoolAmount1));

        /*
        simulator.BeginBlock();
        simulator.GenerateToken(owner, virtualPoolSymbol, virtualPoolSymbol, virtualPoolAmount1 * 100, 0, TokenFlags.Burnable | TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol0, virtualPoolAmount1);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
        simulator.MintTokens(owner, testUserA.Address, virtualPoolSymbol, virtualPoolAmount1);
        simulator.EndBlock();*/

        // Get Tokens Info
        //token1
        var token1 = core.nexus.GetTokenInfo(core.nexus.RootStorage, poolSymbol1);
        var token1Address = core.nexus.GetTokenContract(core.nexus.RootStorage, poolSymbol1);
        Assert.IsTrue(token1.Symbol == poolSymbol1, "Symbol1 != Token1");

        // virtual Token
        var virtualToken = core.nexus.GetTokenInfo(core.nexus.RootStorage, virtualPoolSymbol);
        var virtualTokenAddress = core.nexus.GetTokenContract(core.nexus.RootStorage, virtualPoolSymbol);
        Assert.IsTrue(virtualToken.Symbol == virtualPoolSymbol, $"VirtualSymbol != VirtualToken({virtualToken})");

        poolOwner.CreatePool(poolSymbol1, poolAmount1, virtualPoolSymbol, virtualPoolAmount1);

        // Check if the pool was created
        var pool = poolOwner.GetPool(poolSymbol1, virtualPoolSymbol);

        Assert.IsTrue(pool.Symbol0 == poolSymbol1);
        Assert.IsTrue(pool.Amount0 == poolAmount1);
        Assert.IsTrue(pool.Symbol1 == virtualPoolSymbol); 
        Assert.IsTrue(pool.Amount1 == virtualPoolAmount1);
        Assert.IsTrue(pool.TotalLiquidity == totalLiquidity);
        Assert.IsTrue(pool.Symbol0Address == token1Address.Address.Text);
        Assert.IsTrue(pool.Symbol1Address == virtualTokenAddress.Address.Text);

        Console.WriteLine($"Check Values | {pool.Symbol0}({pool.Symbol0Address}) -> {pool.Amount0} | {pool.Symbol1}({pool.Symbol1Address}) -> {pool.Amount1} || {pool.TotalLiquidity}");
    }

    [TestMethod]
    // TODO: Get the pool initial values and calculate the target rate with those values insted of the static ones.
    public void AddLiquidityToPool()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 500, kcal: 100);
        poolOwner.Fund(poolSymbol2, poolAmount2);
        poolOwner.Fund(poolSymbol4, poolAmount4);
        poolOwner.Fund(poolSymbol5, poolAmount5);

        // Get Initial Pool State the Liquidity
        var pool = poolOwner.GetPool(poolSymbol0, poolSymbol2);

        var amount0 = poolAmount0 / 10;
        var amount1 = poolAmount2 / 10;
        var poolRatio = UnitConversion.ConvertDecimals(pool.Amount0, 8, DomainSettings.FiatTokenDecimals) / UnitConversion.ConvertDecimals(pool.Amount1, 18, DomainSettings.FiatTokenDecimals);
        var amountCalculated = UnitConversion.ConvertDecimals((amount0 / poolRatio), DomainSettings.FiatTokenDecimals, 18);

        // setup Tokens for the user
        /*simulator.BeginBlock();
        simulator.MintTokens(owner, testUserA.Address, poolSymbol0, virtualPoolAmount1);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol2, poolAmount2);
        simulator.EndBlock();*/

        // Add Liquidity to the pool
        poolOwner.AddLiquidity(poolSymbol0, amount0, poolSymbol2, 0);

        // Check the Liquidity
        var poolAfter = poolOwner.GetPool(poolSymbol0, poolSymbol2);
        pool.TotalLiquidity += (amount0 * pool.TotalLiquidity) / (pool.Amount0);

        Assert.IsTrue(poolAfter.Symbol0 == poolSymbol0, $"Symbol is incorrect: {poolSymbol0}");
        Assert.IsTrue(poolAfter.Amount0 == pool.Amount0 + amount0, $"Symbol Amount0 is incorrect: {pool.Amount0 + amount0} != {poolAfter.Amount0} {poolSymbol0}");
        Assert.IsTrue(poolAfter.Symbol1 == poolSymbol2, $"Pair is incorrect: {poolSymbol2}");
        Assert.IsTrue(poolAfter.Amount1 == pool.Amount1 + amountCalculated, $"Symbol Amount1 is incorrect: {pool.Amount1 + amountCalculated} != {poolAfter.Amount1} {poolSymbol2}");
        Assert.IsTrue(pool.TotalLiquidity == poolAfter.TotalLiquidity, $"TotalLiquidity doesn't checkout {pool.TotalLiquidity}!={poolAfter.TotalLiquidity}");
    }

    [TestMethod]
    [Ignore]
    public void AddLiquidityToVirtualPool()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 500, kcal: 100);
        poolOwner.Fund(poolSymbol2, poolAmount2);
        poolOwner.Fund(poolSymbol4, poolAmount4);
        poolOwner.Fund(poolSymbol5, poolAmount5);

        //SetupVirtualPool();

        BigInteger totalLiquidity = (BigInteger)Math.Sqrt((long)(poolAmount1 * virtualPoolAmount1));

        /*
        simulator.BeginBlock();
        simulator.GenerateToken(owner, virtualPoolSymbol, virtualPoolSymbol, virtualPoolAmount1 * 100, 0, TokenFlags.Burnable | TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol0, virtualPoolAmount1);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
        simulator.MintTokens(owner, testUserA.Address, virtualPoolSymbol, virtualPoolAmount1);
        simulator.EndBlock();*/

        // Add Liquidity to the pool
        poolOwner.AddLiquidity(poolSymbol1, poolAmount1 / 2, virtualPoolSymbol, virtualPoolAmount1 / 2);

        // Check the Liquidity
        var pool = poolOwner.GetPool(poolSymbol0, poolSymbol1);
        totalLiquidity += (poolAmount1 * pool.TotalLiquidity) / (poolAmount1 + (poolAmount1 / 2));

        Assert.IsTrue(pool.Symbol0 == poolSymbol0, "Symbol is incorrect");
        Assert.IsTrue(pool.Amount0 == poolAmount1 + (poolAmount1 / 2), "Symbol Amount0 is incorrect");
        Assert.IsTrue(pool.Symbol1 == poolSymbol1, "Pair is incorrect");
        Assert.IsTrue(pool.Amount1 == virtualPoolAmount1 + (virtualPoolAmount1 / 2), "Symbol Amount1 is incorrect");
        Assert.IsTrue(pool.TotalLiquidity == totalLiquidity);
    }

    [TestMethod]
    // TODO: Get the pool initial values and calculate the target rate with those values insted of the static ones.
    public void RemoveLiquidityToPool()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 500, kcal: 100);
        poolOwner.Fund(poolSymbol2, poolAmount2);
        poolOwner.Fund(poolSymbol4, poolAmount4);
        poolOwner.Fund(poolSymbol5, poolAmount5);

        // Get Initial Pool State the Liquidity
        var pool = poolOwner.GetPool(poolSymbol0, poolSymbol1);

        BigInteger poolRatio = UnitConversion.ConvertDecimals(pool.Amount0, 8, DomainSettings.FiatTokenDecimals) * 100 / UnitConversion.ConvertDecimals(pool.Amount1, 10, DomainSettings.FiatTokenDecimals);
        var amount0 = poolAmount0 / 2;
        var amount1 = UnitConversion.ConvertDecimals((amount0 * 100 / poolRatio ), DomainSettings.FiatTokenDecimals, 10);
        Console.WriteLine($"ratio:{poolRatio} | amount0:{amount0} | amount1:{amount1}");
        Console.WriteLine($"BeforeTouchingPool: {pool.Amount0} {pool.Symbol0} | {pool.Amount1} {pool.Symbol1} | PoolRatio:{UnitConversion.ConvertDecimals(pool.Amount0, 8, DomainSettings.FiatTokenDecimals) * 100 / UnitConversion.ConvertDecimals(pool.Amount1, 10, DomainSettings.FiatTokenDecimals)}\n");

        BigInteger totalAm0 = pool.Amount0;
        BigInteger totalAm1 = pool.Amount1;
        //poolRatio = UnitConversion.ConvertDecimals(pool.Amount1, 10, DomainSettings.FiatTokenDecimals) / UnitConversion.ConvertDecimals(pool.Amount0, 8, DomainSettings.FiatTokenDecimals);
        BigInteger totalLiquidity = pool.TotalLiquidity;

        // setup Tokens for the user
        /*simulator.BeginBlock();
        simulator.MintTokens(owner, testUserA.Address, poolSymbol0, virtualPoolAmount1);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
        simulator.EndBlock();*/

        // Add Liquidity to the pool
        poolOwner.AddLiquidity(poolSymbol0, poolAmount0, poolSymbol1, poolAmount1);
        
        var lpAdded = (poolAmount0 * totalLiquidity) / totalAm0;
        totalLiquidity += lpAdded;
        totalAm0 += poolAmount0;
        
        var poolBefore = poolOwner.GetPool(poolSymbol0, poolSymbol1);
        Console.WriteLine($"AfterAdd: {poolBefore.Amount0} {poolBefore.Symbol0} | {poolBefore.Amount1} {poolBefore.Symbol1} | PoolRatio:{UnitConversion.ConvertDecimals(poolBefore.Amount0, 8, DomainSettings.FiatTokenDecimals) * 100 / UnitConversion.ConvertDecimals(poolBefore.Amount1, 10, DomainSettings.FiatTokenDecimals)}\n");

        var nftRAMBefore = poolOwner.GetPoolRAM(poolSymbol0, poolSymbol1);

        // Remove Liquidity
        poolOwner.RemoveLiquidity(poolSymbol0, amount0, poolSymbol1, 0);

        // Get Pool
        var poolAfter = poolOwner.GetPool(poolSymbol0, poolSymbol1);

        Console.WriteLine($"AfterRemove: {poolAfter.Amount0} {poolAfter.Symbol0} | {poolAfter.Amount1} {poolAfter.Symbol1} | PoolRatio:{UnitConversion.ConvertDecimals(poolAfter.Amount0, 8, DomainSettings.FiatTokenDecimals) * 100 / UnitConversion.ConvertDecimals(poolAfter.Amount1, 10, DomainSettings.FiatTokenDecimals)}\n");
        BigInteger newLP = ((nftRAMBefore.Amount0 - amount0) * (totalLiquidity - nftRAMBefore.Liquidity)) / (totalAm0 - nftRAMBefore.Amount0);
        var lpRemoved = ((nftRAMBefore.Amount0 - amount0) * (totalLiquidity- nftRAMBefore.Liquidity)) / (totalAm0- nftRAMBefore.Amount0);
        totalLiquidity = totalLiquidity - nftRAMBefore.Liquidity + newLP;
        totalAm0 -= (amount0);

        // Get My NFT DATA 
        var nftRAMAfter = poolOwner.GetPoolRAM(poolSymbol0, poolSymbol1);

        Console.WriteLine($"TEST: BeforeLP:{nftRAMBefore.Liquidity}  | AfterLP:{nftRAMAfter.Liquidity} | LPRemoved:{lpRemoved}");

        // Validation
        Assert.IsFalse(nftRAMBefore.Amount0 == nftRAMAfter.Amount0, "Amount0 does not differ.");
        Assert.IsFalse(nftRAMBefore.Amount1 == nftRAMAfter.Amount1, "Amount1 does not differ.");
        Assert.IsFalse(nftRAMBefore.Liquidity == nftRAMAfter.Liquidity, $"Liquidity does not differ. | {nftRAMBefore.Liquidity} == {nftRAMAfter.Liquidity}");

        Assert.IsTrue(nftRAMBefore.Amount0 - amount0 == nftRAMAfter.Amount0, "Amount0 not true.");
        Assert.IsTrue(nftRAMBefore.Amount1 - amount1 == nftRAMAfter.Amount1, $"Amount1 not true. {nftRAMBefore.Amount1 - amount1} != {nftRAMAfter.Amount1}");
        Assert.IsTrue(newLP == nftRAMAfter.Liquidity, $"Liquidity does differ. | {nftRAMBefore.Liquidity - lpRemoved} == {nftRAMAfter.Liquidity}");

        // Get Amount by Liquidity
        // Liqudity Formula  Liquidity = (amount0 * pool.TotalLiquidity) / pool.Amount0;
        // Amount Formula  amount = Liquidity  * pool.Amount0 / pool.TotalLiquidity;
        //(amount0 * (pool.TotalLiquidity - nftRAM.Liquidity)) / (pool.Amount0 - nftRAM.Amount0);
        var _amount0 = (nftRAMAfter.Liquidity) * poolAfter.Amount0 / poolAfter.TotalLiquidity;
        var _amount1 = (nftRAMAfter.Liquidity) * poolAfter.Amount1 / poolAfter.TotalLiquidity;
        var _pool_amount0 = (poolBefore.Amount0 - nftRAMBefore.Amount0) + amount0;
        var _pool_amount1 = (poolBefore.Amount1 - nftRAMBefore.Amount1) + amount1;
        var _pool_liquidity = totalLiquidity;
         
        Console.WriteLine($"user Initial = am0:{nftRAMBefore.Amount0} | am1:{nftRAMBefore.Amount1} | lp:{nftRAMBefore.Liquidity}");
        Console.WriteLine($"pool Initial = am0:{poolBefore.Amount0} | am1:{poolBefore.Amount1} | lp:{poolBefore.TotalLiquidity}");
        Console.WriteLine($"user after = am0:{nftRAMAfter.Amount0} | am1:{nftRAMAfter.Amount1} | lp:{nftRAMAfter.Liquidity}");
        Console.WriteLine($"pool after = am0:{poolAfter.Amount0} | am1:{poolAfter.Amount1} | lp:{poolAfter.TotalLiquidity}");
        Console.WriteLine($"am0 = {_amount0} == {nftRAMAfter.Amount0} || am1 = {_amount1} == {nftRAMAfter.Amount1}");
        Assert.IsTrue(_pool_amount0 == poolAfter.Amount0, $"Pool Amount0 not calculated properly | {_pool_amount0} != {poolAfter.Amount0}");
        Assert.IsTrue(_pool_amount1 == poolAfter.Amount1, $"Pool Amount1 not calculated properly | {_pool_amount1} != {poolAfter.Amount1}");
        Assert.IsTrue(_pool_liquidity == poolAfter.TotalLiquidity, $"Pool TotalLiquidity not calculated properly | {_pool_liquidity} != {poolAfter.TotalLiquidity}");
        Assert.IsTrue(_amount0 + UnitConversion.ToBigInteger(0.00000001m, 8) >= nftRAMAfter.Amount0, $"Amount0 not calculated properly | {_amount0+ UnitConversion.ToBigInteger(0.00000001m, 8) } != {nftRAMAfter.Amount0}");
        Assert.IsTrue(_amount1 + UnitConversion.ToBigInteger(0.000000001m, 10) >= nftRAMAfter.Amount1, $"Amount1 not calculated properly | {_amount1+ UnitConversion.ToBigInteger(0.000000001m, 10)} != {nftRAMAfter.Amount1}");

        // Get Liquidity by amount
        var liquidityAm0 = nftRAMAfter.Amount0 * totalLiquidity / poolAfter.Amount0;
        var liquidityAm1 = nftRAMAfter.Amount1 * totalLiquidity / poolAfter.Amount1;

        Console.WriteLine($"LiquidityAm0 = {liquidityAm0} == {nftRAMAfter.Liquidity} || LiquidityAm1 = {liquidityAm1} == {nftRAMAfter.Liquidity} | ratio:{nftRAMAfter.Amount0*100 / nftRAMAfter.Amount1}");
        Console.WriteLine($"LiquidityAm0 = {nftRAMAfter.Amount0} * {totalLiquidity} / {poolAfter.Amount1} = {nftRAMAfter.Amount0 * totalLiquidity / poolAfter.Amount0}");
        Console.WriteLine($"LiquidityAm0 = {nftRAMAfter.Amount0} * {poolAfter.TotalLiquidity} / {poolAfter.Amount1} = {nftRAMAfter.Amount0 * poolAfter.TotalLiquidity / poolAfter.Amount0}");

        Assert.IsTrue(liquidityAm0 == nftRAMAfter.Liquidity, "Liquidity Amount0 -> not calculated properly");
        Assert.IsTrue(liquidityAm1 == nftRAMAfter.Liquidity, "Liquidity Amount1 -> not calculated properly");
        Assert.IsTrue(totalLiquidity == poolAfter.TotalLiquidity, $"Liquidity not true.");
    }

    [TestMethod]
    public void RemoveLiquiditySmall()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 500, kcal: 100);
        poolOwner.Fund(poolSymbol2, poolAmount2);
        poolOwner.Fund(poolSymbol4, poolAmount4);
        poolOwner.Fund(poolSymbol5, poolAmount5);
        
        
        // Get Initial Pool State the Liquidity
        var pool = poolOwner.GetPool(poolSymbol0, poolSymbol1);

        BigInteger poolRatio = UnitConversion.ConvertDecimals(pool.Amount0, 8, DomainSettings.FiatTokenDecimals) * 100 / UnitConversion.ConvertDecimals(pool.Amount1, 10, DomainSettings.FiatTokenDecimals);
        var amount0 = poolAmount0 / 2;
        var amount1 = UnitConversion.ConvertDecimals((amount0 * 100 / poolRatio), DomainSettings.FiatTokenDecimals, 10);

        BigInteger totalAm0 = pool.Amount0;
        BigInteger totalAm1 = pool.Amount1;
        BigInteger totalLiquidity = pool.TotalLiquidity;

        // Setup a test user 
        var testUserA = PhantasmaKeys.Generate();

        // setup Tokens for the user
        /*simulator.BeginBlock();
        simulator.MintTokens(owner, testUserA.Address, poolSymbol0, virtualPoolAmount1);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
        simulator.EndBlock();*/


        // Add Liquidity to the pool
        poolOwner.AddLiquidity(poolSymbol0, amount0, poolSymbol1, 0);

        var lpAdded = (amount0 * totalLiquidity) / totalAm0;
        totalLiquidity += lpAdded;
        totalAm0 += amount0;

        var poolBefore = poolOwner.GetPool(poolSymbol0, poolSymbol1);

        var nftRAMBefore = poolOwner.GetPoolRAM(poolSymbol0, poolSymbol1);

        var _amount0 = (nftRAMBefore.Liquidity) * poolBefore.Amount0 / poolBefore.TotalLiquidity;
        var _amount1 = (nftRAMBefore.Liquidity) * poolBefore.Amount1 / poolBefore.TotalLiquidity;

        Assert.IsTrue(_amount0 + UnitConversion.ToBigInteger(0.00000001m, 8)  >= nftRAMBefore.Amount0, $"Amount0 not calculated properly | {_amount0} != {nftRAMBefore.Amount0}");
        Assert.IsTrue(_amount1 + UnitConversion.ToBigInteger(0.00000001m, 10) >= nftRAMBefore.Amount1, $"Amount1 not calculated properly | {_amount1} != {nftRAMBefore.Amount1}");
    }

    [TestMethod]
    [Ignore]
    public void RemoveLiquidityToVirtualPool()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 500, kcal: 100);
        poolOwner.Fund(poolSymbol2, poolAmount2);
        poolOwner.Fund(poolSymbol4, poolAmount4);
        poolOwner.Fund(poolSymbol5, poolAmount5);

        //SetupVirtualPool();

        BigInteger totalAm0 = poolAmount1;
        BigInteger totalAm1 = virtualPoolAmount1;
        BigInteger totalLiquidity = (BigInteger)Math.Sqrt((long)(totalAm0 * totalAm1));


        /*
        simulator.BeginBlock();
        simulator.GenerateToken(owner, virtualPoolSymbol, virtualPoolSymbol, virtualPoolAmount1 * 100, 0, TokenFlags.Burnable | TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol0, virtualPoolAmount1);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
        simulator.MintTokens(owner, testUserA.Address, virtualPoolSymbol, virtualPoolAmount1);
        simulator.EndBlock();*/

        // Add Liquidity to the pool
        poolOwner.AddLiquidity(poolSymbol1, poolAmount1, virtualPoolSymbol, virtualPoolAmount1);
        var lpAdded = (poolAmount1 * totalLiquidity) / totalAm0;
        totalLiquidity += lpAdded;
        totalAm0 += poolAmount1;

        var nftRAMBefore = poolOwner.GetPoolRAM(poolSymbol1, virtualPoolSymbol);

        // Remove Liquidity
        poolOwner.RemoveLiquidity(poolSymbol1, poolAmount1 / 2, virtualPoolSymbol, virtualPoolAmount1 / 2);

        // Get Pool
        var pool = poolOwner.GetPool(poolSymbol1, virtualPoolSymbol);
        var lpRemoved = ((poolAmount1 / 2) * totalLiquidity) / totalAm0;
        totalLiquidity -= lpRemoved;
        totalAm0 -= (poolAmount1 / 2);

        // Get My NFT DATA 
        var nftRAMAfter = poolOwner.GetPoolRAM(poolSymbol1, virtualPoolSymbol);

        // Validation
        Assert.IsFalse(nftRAMBefore.Amount0 == nftRAMAfter.Amount0, "Amount0 does not differ.");
        Assert.IsFalse(nftRAMBefore.Amount1 == nftRAMAfter.Amount1, "Amount1 does not differ.");
        Assert.IsFalse(nftRAMBefore.Liquidity == nftRAMAfter.Liquidity, "Liquidity does not differ.");

        Assert.IsTrue(nftRAMBefore.Amount0 - (poolAmount1 / 2) == nftRAMAfter.Amount0, "Amount0 not true.");
        Assert.IsTrue(nftRAMBefore.Amount1 - (virtualPoolAmount1 / 2) == nftRAMAfter.Amount1, "Amount1 not true.");

        // Get Amount by Liquidity
        // Liqudity Formula  Liquidity = (amount0 * pool.TotalLiquidity) / pool.Amount0;
        // Amount Formula  amount = Liquidity  * pool.Amount0 / pool.TotalLiquidity;
        var amount0 = nftRAMAfter.Liquidity * pool.Amount0 / pool.TotalLiquidity;
        var amount1 = nftRAMAfter.Liquidity * pool.Amount1 / pool.TotalLiquidity;

        Console.WriteLine($"am0 = {amount0} == {nftRAMAfter.Amount0} || am1 = {amount1} == {nftRAMAfter.Amount1}");
        Assert.IsTrue(amount0 == nftRAMAfter.Amount0, "Amount0 not calculated properly");
        Assert.IsTrue(amount1 == nftRAMAfter.Amount1, "Amount1 not calculated properly");

        // Get Liquidity by amount
        var liquidityAm0 = nftRAMAfter.Amount0 * totalLiquidity / pool.Amount0;
        var liquidityAm1 = nftRAMAfter.Amount1 * totalLiquidity / pool.Amount1;

        Console.WriteLine($"LiquidityAm0 = {liquidityAm0} == {nftRAMAfter.Liquidity} || LiquidityAm1 = {liquidityAm1} == {nftRAMAfter.Liquidity}");

        Assert.IsTrue(liquidityAm0 == nftRAMAfter.Liquidity, "Liquidity Amount0 -> not calculated properly");
        Assert.IsTrue(liquidityAm1 == nftRAMAfter.Liquidity, "Liquidity Amount1 -> not calculated properly");
        Assert.IsTrue(totalLiquidity == pool.TotalLiquidity, "Liquidity not true.");
    }

    
    /*
     [TestMethod]
    // TODO: Get the pool initial values and calculate the target rate with those values insted of the static ones.
    public void GetRatesForSwap()
    {
        

        var scriptPool = new ScriptBuilder().CallContract("swap", "GetPool", poolSymbol0, poolSymbol1).EndScript();
        var resultPool = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptPool);
        var pool = poolOwner.GetPool(poolSymbol0, poolSymbol1);

        BigInteger amount = UnitConversion.ToBigInteger(5, 8);
        BigInteger targetRate = (pool.Amount1 * (1 - 3 / 100) * amount / (pool.Amount0 + (1 - 3 / 100) * amount));

        var script = new ScriptBuilder().CallContract("swap", "GetRates", poolSymbol0, amount).EndScript();

        var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);

        var temp = result.ToObject();
        var rates = (SwapPair[])temp;

        decimal rate = 0;

        foreach (var entry in rates)
        {
            if (entry.Symbol == DomainSettings.FuelTokenSymbol)
            {
                rate = UnitConversion.ToDecimal(entry.Value, DomainSettings.FuelTokenDecimals);
                break;
            }
        }

        Assert.IsTrue(rate == UnitConversion.ToDecimal(targetRate, DomainSettings.FuelTokenDecimals), $"{rate} != {targetRate}");
    }*/

    [TestMethod]
    // TODO: Get the pool initial values and calculate the target rate with those values insted of the static ones.
    public void GetRateForSwap()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 500, kcal: 100);
        poolOwner.Fund(poolSymbol2, poolAmount2);
        poolOwner.Fund(poolSymbol4, poolAmount4);
        poolOwner.Fund(poolSymbol5, poolAmount5);
        
        var pool = poolOwner.GetPool(poolSymbol0, poolSymbol1);

        BigInteger amount = UnitConversion.ToBigInteger(5, 8);
        BigInteger targetRate = pool.Amount1 * (1 - 3 / 100) * amount / (pool.Amount0 + (1 - 3 / 100) * amount);
        
        var rate = poolOwner.GetRate(poolSymbol0, poolSymbol1, amount);

        Assert.IsTrue(targetRate == rate);
    }

    [TestMethod]
    public void SwapTokens()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var poolOwner2 = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 500, kcal: 100);
        poolOwner.Fund(poolSymbol2, poolAmount2);
        poolOwner.Fund(poolSymbol4, poolAmount4);
        poolOwner.Fund(poolSymbol5, poolAmount5);

        BigInteger swapValue = UnitConversion.ToBigInteger(100, 8);

        /*simulator.BeginBlock();
        simulator.MintTokens(owner, testUserA.Address, poolSymbol0, poolAmount0 * 10);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol2, poolAmount2 * 2);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol4, poolAmount4 * 2);
        simulator.MintTokens(owner, testUserB.Address, poolSymbol0, virtualPoolAmount1);
        simulator.MintTokens(owner, testUserB.Address, poolSymbol1, poolAmount1);
        simulator.EndBlock();*/

        
        
        var beforeTXBalanceKCAL = poolOwner2.GetBalance(poolSymbol1);

        // Add Liquidity to the pool SOUL / KCAL
        /*simulator.BeginBlock();
        var tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.Minimal, () =>
                ScriptUtils
                .BeginScript()
                .AllowGas(testUserA.Address, Address.Null, 1, 9999)
                .CallContract("swap", "AddLiquidity", testUserA.Address, poolSymbol0, poolAmount0, poolSymbol1, poolAmount1)
                .SpendGas(testUserA.Address)
                .EndScript()
            );
        var block = simulator.EndBlock().First();*/

        // Get Rate
        var rate = poolOwner2.GetRate(poolSymbol0, poolSymbol2, swapValue);

        Console.WriteLine($"{swapValue} {poolSymbol0} for {rate} {poolSymbol2}");

        // Make Swap SOUL / ETH
        poolOwner2.SwapTokens(poolSymbol1, poolSymbol2, swapValue);
        var afterTXBalanceKCAL = poolOwner2.GetBalance(poolSymbol1);
        var kcalfee = beforeTXBalanceKCAL - afterTXBalanceKCAL;
        Console.WriteLine($"KCAL Fee: {kcalfee}");

        // Check trade
        var originalBalance = poolOwner2.GetBalance(poolSymbol2);
        Assert.IsTrue(rate == originalBalance, $"{rate} != {originalBalance}");

        // Make Swap SOUL / KCAL
        rate = poolOwner2.GetRate(poolSymbol0, poolSymbol1, swapValue);

        Console.WriteLine($"{swapValue} {poolSymbol0} for {rate} {poolSymbol1}");


        poolOwner2.SwapTokens(poolSymbol0, poolSymbol1, swapValue);

        originalBalance = poolOwner2.GetBalance(poolSymbol1);

        Assert.IsTrue(rate == originalBalance-afterTXBalanceKCAL+kcalfee, $"{rate} != {originalBalance-afterTXBalanceKCAL+kcalfee}");
    }

    [TestMethod]
    public void SwapTokensReverse()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var poolOwner2 = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 500, kcal: 100);
        poolOwner.Fund(poolSymbol2, poolAmount2);
        poolOwner.Fund(poolSymbol4, poolAmount4);
        poolOwner.Fund(poolSymbol5, poolAmount5);

        BigInteger swapValueKCAL = UnitConversion.ToBigInteger(1000, 10);
        BigInteger swapValueETH = UnitConversion.ToBigInteger(1, 18);

        /*simulator.BeginBlock();
        simulator.MintTokens(owner, testUserA.Address, poolSymbol0, poolAmount0 * 10);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol2, poolAmount2 * 2);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol4, poolAmount4 * 2);
        simulator.MintTokens(owner, testUserB.Address, poolSymbol0, poolAmount0);
        simulator.MintTokens(owner, testUserB.Address, poolSymbol1, poolAmount1);
        simulator.MintTokens(owner, testUserB.Address, poolSymbol2, poolAmount2 * 2);
        simulator.EndBlock();*/

        var beforeTXBalanceSOUL = poolOwner2.GetBalance(poolSymbol0);
        var beforeTXBalanceKCAL = poolOwner2.GetBalance(poolSymbol1);
        var beforeTXBalanceETH = poolOwner2.GetBalance(poolSymbol2);

        // Add Liquidity to the pool SOUL / KCAL
        poolOwner.AddLiquidity(poolSymbol0, poolAmount0, poolSymbol1, poolAmount1);

        // SOUL / ETH

        // Get Rate
        var rate = poolOwner2.GetRate(poolSymbol2, poolSymbol0, swapValueETH);

        Console.WriteLine($"{UnitConversion.ToDecimal(swapValueETH, 18)} {poolSymbol2} for {UnitConversion.ToDecimal(rate, 8)} {poolSymbol0}");
        // Make Swap SOUL / ETH
        poolOwner2.SwapTokens(poolSymbol2, poolSymbol0, swapValueETH);
        
        var afterTXBalanceKCAL = poolOwner2.GetBalance(poolSymbol1);
        var kcalfee = beforeTXBalanceKCAL - afterTXBalanceKCAL;
        Console.WriteLine($"KCAL Fee: {UnitConversion.ToDecimal(kcalfee, 10)}");

        // Check trade
        var afterTXBalanceSOUL = poolOwner2.GetBalance(poolSymbol0);
        var afterTXBalanceETH = poolOwner2.GetBalance(poolSymbol2);

        Assert.IsTrue(beforeTXBalanceSOUL + rate == afterTXBalanceSOUL, $"{beforeTXBalanceSOUL+rate} != {afterTXBalanceSOUL}");
        Assert.IsTrue(beforeTXBalanceETH - swapValueETH == afterTXBalanceETH, $"{beforeTXBalanceETH - swapValueETH} != {afterTXBalanceETH}");

        // Make Swap SOUL / KCAL
        rate = poolOwner2.GetRate(poolSymbol1, poolSymbol0, swapValueKCAL);

        Console.WriteLine($"{UnitConversion.ToDecimal(swapValueKCAL, 10)} {poolSymbol1} for {UnitConversion.ToDecimal(rate, 8)} {poolSymbol0}");

        poolOwner2.SwapTokens(poolSymbol1, poolSymbol0, swapValueKCAL);

        var afterTXBalanceSOULEND = poolOwner2.GetBalance(poolSymbol0);
        var afterTXBalanceKCALEND = poolOwner2.GetBalance(poolSymbol1);

        Assert.IsTrue(afterTXBalanceSOUL + rate == afterTXBalanceSOULEND, $"{rate} != {afterTXBalanceSOULEND}");
        Assert.IsTrue(afterTXBalanceKCALEND == afterTXBalanceKCAL - kcalfee - swapValueKCAL, $"{afterTXBalanceKCALEND} != {afterTXBalanceKCAL - kcalfee - swapValueKCAL}");
    }


    [TestMethod]
    public void SwapVirtual()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var poolOwner2 = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 500, kcal: 100);
        poolOwner.Fund(poolSymbol2, poolAmount2);
        poolOwner.Fund(poolSymbol4, poolAmount4);
        poolOwner.Fund(poolSymbol5, poolAmount5);
        
        
        BigInteger swapValueKCAL = UnitConversion.ToBigInteger(1000, 10);
        BigInteger swapValueETH = UnitConversion.ToBigInteger(1, 18);

        /*simulator.BeginBlock();
        simulator.MintTokens(owner, testUserA.Address, poolSymbol0, poolAmount0 * 10);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol2, poolAmount2 * 2);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol4, poolAmount4 * 2);
        simulator.MintTokens(owner, testUserB.Address, poolSymbol0, poolAmount0);
        simulator.MintTokens(owner, testUserB.Address, poolSymbol1, poolAmount1);
        simulator.MintTokens(owner, testUserB.Address, poolSymbol2, poolAmount2 * 2);
        simulator.EndBlock();*/

        var beforeTXBalanceKCAL = poolOwner2.GetBalance(poolSymbol1);
        var beforeTXBalanceETH = poolOwner2.GetBalance( poolSymbol2 );

        // Get Rate
        var rate = poolOwner.GetRate(poolSymbol1, poolSymbol2, swapValueKCAL);

        Console.WriteLine($"{UnitConversion.ToDecimal(swapValueKCAL, 10)} {poolSymbol1} for {UnitConversion.ToDecimal(rate, 18)} {poolSymbol2}");
        // Make Swap SOUL / ETH
        poolOwner2.SwapTokens(poolSymbol1, poolSymbol2, swapValueKCAL);
        
        var afterTXBalanceKCAL =  poolOwner.GetBalance(poolSymbol1);
        var afterTXBalanceETH =  poolOwner.GetBalance(poolSymbol2);
        var kcalfee = beforeTXBalanceKCAL - afterTXBalanceKCAL - swapValueKCAL;


        Assert.IsTrue(afterTXBalanceETH == beforeTXBalanceETH+rate, $"{afterTXBalanceETH} != {beforeTXBalanceETH+rate}");
        Assert.IsTrue(beforeTXBalanceKCAL - kcalfee - swapValueKCAL == afterTXBalanceKCAL, $"{beforeTXBalanceKCAL - kcalfee - swapValueKCAL} != {afterTXBalanceKCAL}");
    }

    [TestMethod]
    // TODO: Finish this
    public void SwapFee()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var poolOwner2 = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 500, kcal: 100);
        poolOwner.Fund(poolSymbol2, poolAmount2);
        poolOwner.Fund(poolSymbol4, poolAmount4);
        poolOwner.Fund(poolSymbol5, poolAmount5);
        

        var initialKcal = 1000000;

        BigInteger swapValueSOUL = UnitConversion.ToBigInteger(1, 8);
        BigInteger swapValueKCAL = UnitConversion.ToBigInteger(1, 10);
        BigInteger swapFee = UnitConversion.ConvertDecimals(swapValueSOUL, 8, 8);

        /*simulator.BeginBlock();
        simulator.MintTokens(owner, testUserA.Address, poolSymbol0, poolAmount0 );
        simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol2, poolAmount2 * 2);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol4, poolAmount4 * 2);
        simulator.MintTokens(owner, testUserB.Address, poolSymbol0, poolAmount0);
        simulator.MintTokens(owner, testUserB.Address, poolSymbol1, initialKcal);
        simulator.EndBlock();*/

        // Get Balances before starting trades
        var beforeTXBalanceSOUL = poolOwner2.GetBalance( poolSymbol0 );
        var beforeTXBalanceKCAL = poolOwner2.GetBalance( poolSymbol1 );
        var kcalToSwap = swapValueSOUL;
        kcalToSwap -= UnitConversion.ConvertDecimals(beforeTXBalanceKCAL, DomainSettings.FuelTokenDecimals, 8);

        // Kcal to trade for
        Console.WriteLine($"Soul amount: {kcalToSwap} | {beforeTXBalanceKCAL} | {swapValueSOUL} - { UnitConversion.ConvertDecimals(beforeTXBalanceKCAL, DomainSettings.FuelTokenDecimals, 8)} = {kcalToSwap}");

        // Get Pool
        var pool = poolOwner.GetPool(poolSymbol1, poolSymbol0);

        var rateByPool = pool.Amount0 * (1 - 97 / 100) * kcalToSwap / (pool.Amount1 + (1 - 97 / 100) * kcalToSwap);
        rateByPool = UnitConversion.ConvertDecimals(rateByPool, 10, 8);

        // Get Rate
        var rate = poolOwner2.GetRate(poolSymbol0, poolSymbol1, kcalToSwap);

        Console.WriteLine($"{UnitConversion.ToDecimal(kcalToSwap, 8)} {poolSymbol0} for {UnitConversion.ToDecimal(rate, 10)} {poolSymbol1} | Swap ->  {UnitConversion.ToDecimal(rateByPool, 10)}");

        // Make Swap SOUL / KCAL (SwapFee)
        poolOwner2.SwapFee(poolSymbol0, swapValueSOUL);
        

        // Get the balances
        var afterTXBalanceSOUL = poolOwner2.GetBalance( poolSymbol0 );
        var afterTXBalanceKCAL = poolOwner2.GetBalance( poolSymbol1 );

        var kcalfee = afterTXBalanceKCAL - beforeTXBalanceKCAL - rate;
        Console.WriteLine($"Fee:{UnitConversion.ToDecimal(kcalfee, 10)}");

        Console.WriteLine($"{beforeTXBalanceSOUL} != {afterTXBalanceSOUL} | {afterTXBalanceKCAL}");

        Assert.IsTrue(afterTXBalanceSOUL == beforeTXBalanceSOUL-(kcalToSwap + UnitConversion.ConvertDecimals(500, 10, 8)), $"SOUL {afterTXBalanceSOUL} != {beforeTXBalanceSOUL-(kcalToSwap + UnitConversion.ConvertDecimals(500, 10, 8))}");
        Assert.IsTrue(beforeTXBalanceKCAL + kcalfee + rate == afterTXBalanceKCAL, $"KCAL {beforeTXBalanceKCAL + kcalfee + rate} != {afterTXBalanceKCAL}");
    }

    [TestMethod]
    public void GetUnclaimed()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var poolOwner2 = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 500, kcal: 100);
        poolOwner.Fund(poolSymbol2, poolAmount2);
        poolOwner.Fund(poolSymbol4, poolAmount4);
        poolOwner.Fund(poolSymbol5, poolAmount5);
        

        var testUserA = PhantasmaKeys.Generate();

        int swapValue = 1000;

        /*simulator.BeginBlock();
        simulator.MintTokens(owner, testUserA.Address, poolSymbol0, virtualPoolAmount1);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
        simulator.EndBlock();*/

        // Add Liquidity to the pool
        poolOwner.AddLiquidity(poolSymbol0, poolAmount0, poolSymbol1, poolAmount1);

        // Get Rate
        //UnitConversion.ConvertDecimals(swapValue, 0, 10)
        var unclaimed = poolOwner.GetUnclaimedFees(poolSymbol0, poolSymbol1);

        Assert.IsTrue(unclaimed == 0, "Unclaimed Failed");
        
        // TODO: Add more tests (Swap on the pool and check the fees)
    }


    [TestMethod]
    public void GetFees()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var poolOwner2 = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 500, kcal: 100);
        poolOwner.Fund(poolSymbol2, poolAmount2);
        poolOwner.Fund(poolSymbol4, poolAmount4);
        poolOwner.Fund(poolSymbol5, poolAmount5);
        
        var swapValueSOUL = UnitConversion.ToBigInteger(10, 8);

        /*simulator.BeginBlock();
        simulator.MintTokens(owner, testUserA.Address, poolSymbol0, poolAmount0 * 2);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol1, poolAmount1 * 2);
        simulator.MintTokens(owner, testUserB.Address, poolSymbol0, poolAmount0);
        simulator.MintTokens(owner, testUserB.Address, poolSymbol1, poolAmount1);
        simulator.EndBlock();*/

        // Add Liquidity to the pool
        poolOwner.AddLiquidity(poolSymbol0, poolAmount0, poolSymbol1, poolAmount1);

        // Get Pool
        var pool = poolOwner.GetPool(poolSymbol0, poolSymbol1);

        // Get RAM
        var nftRAMBefore = poolOwner.GetPoolRAM(poolSymbol0, poolSymbol1);

        // Make a Swap
        var rate = poolOwner.GetRate(poolSymbol0, poolSymbol1, swapValueSOUL);

        Console.WriteLine($"{UnitConversion.ToDecimal(swapValueSOUL, 8)} {poolSymbol0} for {UnitConversion.ToDecimal(rate, 10)} {poolSymbol1}");
        
        // Make Swap SOUL / ETH
        poolOwner2.SwapTokens(poolSymbol0, poolSymbol1, swapValueSOUL);

        // Get Rate
        var unclaimed = poolOwner.GetUnclaimedFees(poolSymbol0, poolSymbol1);
        
        BigInteger UserPercent = 75;
        BigInteger totalFees = rate * 3 / 100;
        BigInteger feeForUsers = totalFees * 100 / UserPercent;
        BigInteger feeAmount = nftRAMBefore.Liquidity * 1000000000000 / pool.TotalLiquidity;
        var calculatedFees = feeForUsers * feeAmount / 1000000000000;

        Assert.IsTrue(unclaimed == calculatedFees, $"Unclaimed Failed | {unclaimed} != {calculatedFees}");
    }

    [TestMethod]
    public void GetClaimFees()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var poolOwner2 = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 500, kcal: 100);
        poolOwner.Fund(poolSymbol2, poolAmount2);
        poolOwner.Fund(poolSymbol4, poolAmount4);
        poolOwner.Fund(poolSymbol5, poolAmount5);

        var swapValueSOUL = UnitConversion.ToBigInteger(10, 8);

        /*simulator.BeginBlock();
        simulator.MintTokens(owner, testUserA.Address, poolSymbol0, poolAmount0 * 2);
        simulator.MintTokens(owner, testUserA.Address, poolSymbol1, poolAmount1 * 3);
        simulator.MintTokens(owner, testUserB.Address, poolSymbol0, poolAmount0);
        simulator.MintTokens(owner, testUserB.Address, poolSymbol1, poolAmount1);
        simulator.EndBlock();*/

        // Add Liquidity to the pool
        poolOwner.AddLiquidity(poolSymbol0, poolAmount0, poolSymbol1, 10);

        // Get Pool
        var pool = poolOwner.GetPool(poolSymbol0, poolSymbol1);

        // Get RAM
        var nftRAMBefore = poolOwner.GetPoolRAM(poolSymbol0, poolSymbol1);

        // Make a Swap
        var rate = poolOwner2.GetRate(poolSymbol0, poolSymbol1, swapValueSOUL);

        Console.WriteLine($"{UnitConversion.ToDecimal(swapValueSOUL, 8)} {poolSymbol0} for {UnitConversion.ToDecimal(rate, 10)} {poolSymbol1}");
        // Make Swap SOUL / ETH
        poolOwner2.SwapTokens(poolSymbol0, poolSymbol1, swapValueSOUL);

        // Get Unclaimed Fees
        var unclaimed = poolOwner.GetUnclaimedFees(poolSymbol0, poolSymbol1);
        BigInteger UserPercent = 75;
        BigInteger totalFees = rate * 3 / 100;
        BigInteger feeForUsers = totalFees * 100 / UserPercent;
        BigInteger feeAmount = nftRAMBefore.Liquidity * 1000000000000 / pool.TotalLiquidity;
        var calculatedFees = feeForUsers * feeAmount / 1000000000000;

        Console.WriteLine($"{calculatedFees} {poolSymbol1}");
        Assert.IsTrue(unclaimed == calculatedFees, $"Unclaimed Failed | {unclaimed} != {calculatedFees}");

        // Claim Fees
        // Get User Balance Before Claiming Fees
        var beforeTXBalanceSOUL = poolOwner.GetBalance( poolSymbol0 );
        var beforeTXBalanceKCAL = poolOwner.GetBalance( poolSymbol1 );

        poolOwner.ClaimFees(poolSymbol0, poolSymbol1);

        // Get User Balance After Claiming Fees
        var afterTXBalanceSOUL = poolOwner.GetBalance(poolSymbol0);
        var afterTXBalanceKCAL = poolOwner.GetBalance(poolSymbol1);
        
        var unclaimedAfter = poolOwner.GetUnclaimedFees(poolSymbol0, poolSymbol1);

        Assert.IsTrue(afterTXBalanceSOUL == beforeTXBalanceSOUL+calculatedFees, $"Soul Claimed Failed | {afterTXBalanceSOUL} != {beforeTXBalanceSOUL}");
        Assert.IsTrue(beforeTXBalanceKCAL != afterTXBalanceKCAL, $"Kcal for TX Failed | {beforeTXBalanceKCAL} != {afterTXBalanceKCAL}");
        Assert.IsTrue(unclaimedAfter == 0, $"Kcal for TX Failed | {unclaimedAfter} != {0}");
    }

    [TestMethod]
    [Ignore]
    public void CosmicSwap()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var poolOwner2 = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 500, kcal: 100);
        poolOwner.Fund(poolSymbol2, poolAmount2);
        poolOwner.Fund(poolSymbol4, poolAmount4);
        poolOwner.Fund(poolSymbol5, poolAmount5);

        var fuelAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
        var transferAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);

        var symbol = "COOL";

        /*simulator.BeginBlock();
        simulator.GenerateToken(owner, symbol, "CoolToken", 1000000, 0, TokenFlags.Burnable | TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite);
        simulator.MintTokens(owner, testUserA.Address, symbol, 100000);
        simulator.EndBlock();

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
        var blockA = simulator.EndBlock().FirstOrDefault();

        Assert.IsTrue(blockA != null);
        Assert.IsFalse(blockA.OracleData.Any());*/

        var originalBalance = poolOwner.GetBalance( DomainSettings.FuelTokenSymbol );

        var swapAmount = UnitConversion.ToBigInteger(0.01m, DomainSettings.StakingTokenDecimals);
        core.simulator.BeginBlock();
        core.simulator.GenerateSwap(poolOwner.userKeys, core.nexus.RootChain, DomainSettings.StakingTokenSymbol, DomainSettings.FuelTokenSymbol, swapAmount);
        var blockB = core.simulator.EndBlock().FirstOrDefault();

        var finalBalance = poolOwner.GetBalance( DomainSettings.FuelTokenSymbol );
        Assert.IsTrue(finalBalance > originalBalance);

        /*
        swapAmount = 10;
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
        {
           return ScriptUtils.BeginScript().
                AllowGas(testUserA.Address, Address.Null, 400, 9999).
                //CallContract("swap", "SwapFiat", testUserA.Address, symbol, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(0.1m, DomainSettings.FiatTokenDecimals)).
                CallContract("swap", "SwapTokens", testUserA.Address, symbol, DomainSettings.FuelTokenSymbol, new BigInteger(1)).
                SpendGas(testUserA.Address).
                EndScript();
        });
        simulator.EndBlock();*/
    }
    /*

    [TestMethod]
    public void ChainSwapIn()
    {
        

        var neoKeys = Neo.Core.NeoKeys.Generate();

        var limit = 800;

        // 1 - at this point a real NEO transaction would be done to the NEO address obtained from getPlatforms in the API
        // here we just use a random hardcoded hash and a fake oracle to simulate it
        var swapSymbol = "GAS";
        var neoTxHash = OracleSimulator.SimulateExternalTransaction("neo", Pay.Chains.NeoWallet.NeoID, neoKeys.PublicKey, neoKeys.Address, swapSymbol, 2);

        var tokenInfo = nexus.GetTokenInfo(nexus.RootStorage, swapSymbol);

        // 2 - transcode the neo address and settle the Neo transaction on Phantasma
        var transcodedAddress = Address.FromKey(neoKeys);

        var testUser = PhantasmaKeys.Generate();

        var platformName = Pay.Chains.NeoWallet.NeoPlatform;
        var platformChain = Pay.Chains.NeoWallet.NeoPlatform;

        var gasPrice = simulator.MinimumFee;

        Func<decimal, byte[]> genScript = (fee) =>
        {
            return new ScriptBuilder()
            .CallContract("interop", "SettleTransaction", transcodedAddress, platformName, platformChain, neoTxHash)
            .CallContract("swap", "SwapFee", transcodedAddress, swapSymbol, UnitConversion.ToBigInteger(fee, DomainSettings.FuelTokenDecimals))
            .TransferBalance(swapSymbol, transcodedAddress, testUser.Address)
            .AllowGas(transcodedAddress, Address.Null, gasPrice, limit)
            .TransferBalance(DomainSettings.FuelTokenSymbol, transcodedAddress, testUser.Address)
            .SpendGas(transcodedAddress).EndScript();
        };

        // note the 0.1m passed here could be anything else. It's just used to calculate the actual fee
        var vm = new GasMachine(genScript(0.1m), 0, null);
        var result = vm.Execute();
        var usedGas = UnitConversion.ToDecimal((int)(vm.UsedGas * gasPrice), DomainSettings.FuelTokenDecimals);

        simulator.BeginBlock();
        var tx = simulator.GenerateCustomTransaction(neoKeys, ProofOfWork.None, () =>
        {
            return genScript(usedGas);
        });

        simulator.EndBlock();

        var swapToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, swapSymbol);
        var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, swapToken, transcodedAddress);
        Assert.IsTrue(balance == 0);

        balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, swapToken, testUser.Address);
        Assert.IsTrue(balance > 0);

        var settleHash = (Hash)nexus.RootChain.InvokeContract(nexus.RootStorage, "interop", nameof(InteropContract.GetSettlement), "neo", neoTxHash).ToObject();
        Assert.IsTrue(settleHash == tx.Hash);

        var fuelToken = nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
        var leftoverBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, transcodedAddress);
        //Assert.IsTrue(leftoverBalance == 0);
    }

    [TestMethod]
    public void ChainSwapOut()
    {
        

        var rootChain = nexus.RootChain;

        var testUser = PhantasmaKeys.Generate();

        var potAddress = SmartContract.GetAddressForNative(NativeContractKind.Swap);

        // 0 - just send some assets to the 
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals));
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals));
        simulator.MintTokens(owner, potAddress, "GAS", poolAmount5);
        simulator.MintTokens(owner, testUser.Address, poolSymbol1, poolAmount1);
        simulator.MintTokens(owner, potAddress, poolSymbol1, poolAmount1);
        simulator.EndBlock();

        var oldBalance = rootChain.GetTokenBalance(rootChain.Storage, DomainSettings.StakingTokenSymbol, testUser.Address);
        var oldSupply = rootChain.GetTokenSupply(rootChain.Storage, DomainSettings.StakingTokenSymbol);

        // 1 - transfer to an external interop address
        var targetAddress = NeoWallet.EncodeAddress("AG2vKfVpTozPz2MXvye4uDCtYcTnYhGM8F");
        simulator.BeginBlock();
        simulator.GenerateTransfer(testUser, targetAddress, nexus.RootChain, DomainSettings.StakingTokenSymbol, UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals));
        simulator.EndBlock();

        var currentBalance = rootChain.GetTokenBalance(rootChain.Storage, DomainSettings.StakingTokenSymbol, testUser.Address);
        var currentSupply = rootChain.GetTokenSupply(rootChain.Storage, DomainSettings.StakingTokenSymbol);

        Assert.IsTrue(currentBalance < oldBalance);
        Assert.IsTrue(currentBalance == 0);

        Assert.IsTrue(currentSupply < oldSupply);
    }

    [TestMethod]
    public void QuoteConversions()
    {
        

        Assert.IsTrue(nexus.PlatformExists(nexus.RootStorage, "neo"));
        Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, "NEO"));

        var context = new StorageChangeSetContext(nexus.RootStorage);
        var runtime = new RuntimeVM(-1, new byte[0], 0, nexus.RootChain, Address.Null, Timestamp.Now, null, context, new OracleSimulator(nexus), ChainTask.Null, true);

        var temp = runtime.GetTokenQuote("NEO", "KCAL", 1);
        var price = UnitConversion.ToDecimal(temp, DomainSettings.FuelTokenDecimals);
        Assert.IsTrue(price == 100);

        temp = runtime.GetTokenQuote("KCAL", "NEO", UnitConversion.ToBigInteger(100, DomainSettings.FuelTokenDecimals));
        price = UnitConversion.ToDecimal(temp, 0);
        Assert.IsTrue(price == 1);

        temp = runtime.GetTokenQuote("SOUL", "KCAL", UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals));
        price = UnitConversion.ToDecimal(temp, DomainSettings.FuelTokenDecimals);
        Assert.IsTrue(price == 5);
    }*/

        

    

    #endregion

    #region AuxFunctions

    private static int GetDecimals(string symbol)
    {
        switch (symbol)
        {
            case "SOUL": return 8;
            case "KCAL": return 10;
            default: throw new System.Exception("Unknown decimals for " + symbol);
        }
    }
    
    class CoreClass
    {
        public PhantasmaKeys owner;
        public NexusSimulator simulator;
        public Nexus nexus;

        public CoreClass()
        {
            InitExchange();
        }

        public CoreClass(bool Pools) : base()
        {
            if (Pools)
                InitPools();
        }

        private void InitExchange()
        {
            owner = PhantasmaKeys.Generate();
            simulator = new NexusSimulator(owner);
            nexus = simulator.Nexus;
            
            var balanceBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, owner.Address);
            simulator.GetFundsInTheFuture(owner);
            var balanceAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, owner.Address);

            Console.WriteLine($"before {balanceBefore} != after {balanceAfter}");
            //CreateTokens();
        }

        public void InitPools()
        {
            simulator.BeginBlock();
            simulator.MintTokens(owner, owner.Address, poolSymbol0, poolAmount0 * 100);
            simulator.MintTokens(owner, owner.Address, poolSymbol1, poolAmount1 * 100);
            simulator.MintTokens(owner, owner.Address, poolSymbol2, poolAmount2 * 100);
            simulator.MintTokens(owner, owner.Address, poolSymbol4, poolAmount4 * 100);
            simulator.MintTokens(owner, owner.Address, poolSymbol5, poolAmount5 * 100);
            simulator.MintTokens(owner, SwapAddress, poolSymbol0, poolAmount0);
            simulator.GenerateTransfer(owner, SwapAddress, nexus.RootChain, poolSymbol1, poolAmount1 * 2);
            simulator.GenerateTransfer(owner, SwapAddress, nexus.RootChain, poolSymbol2, poolAmount2);
            simulator.GenerateTransfer(owner, SwapAddress, nexus.RootChain, poolSymbol4, poolAmount4);
            simulator.GenerateTransfer(owner, SwapAddress, nexus.RootChain, poolSymbol5, poolAmount5);
            simulator.EndBlock();

            Migrate();
        }

        private void CreateTokens()
        {
            string[] tokenList = { maxDivTokenSymbol, nonDivisibleTokenSymbol };

            simulator.BeginBlock();

            foreach (var symbol in tokenList)
            {
                int decimals = 0;
                BigInteger supply = 0;
                TokenFlags flags = TokenFlags.Divisible;

                switch (symbol)
                {
                    case maxDivTokenSymbol:
                        decimals = DomainSettings.MAX_TOKEN_DECIMALS;
                        supply = UnitConversion.ToBigInteger(100000000, decimals);
                        flags = TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite | TokenFlags.Divisible;
                        break;

                    case minDivTokenSymbol:
                        decimals = 1;
                        supply = UnitConversion.ToBigInteger(100000000, 18);
                        flags = TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite | TokenFlags.Divisible;
                        break;

                    case nonDivisibleTokenSymbol:
                        decimals = 0;
                        supply = UnitConversion.ToBigInteger(100000000, 18);
                        flags = TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite;
                        break;
                }

                simulator.GenerateToken(owner, symbol, $"{symbol}Token", supply, decimals, flags);
                simulator.MintTokens(owner, owner.Address, symbol, supply);
            }

            simulator.EndBlock();
        }

        public void Migrate()
        {
            // Migrate Call Old Way
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(owner, ProofOfWork.Minimal, () =>
                ScriptUtils
                    .BeginScript()
                    .AllowGas(owner.Address, Address.Null, 1, 9999)
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.MigrateToV3))
                    .SpendGas(owner.Address)
                    .EndScript());
            var block = simulator.EndBlock().First();
            var resultBytes = block.GetResultForTransaction(tx.Hash);
        }
    }

    class ExchangeUser
    {
        private readonly PhantasmaKeys user;
        public IToken baseToken;
        public IToken quoteToken;
        public PhantasmaKeys userKeys;
        public CoreClass core;
        public NexusSimulator simulator;
        public Nexus nexus;
        
        public enum TokenType { Base, Quote}

        public ExchangeUser(string baseSymbol, string quoteSymbol,  CoreClass core = null)
        {
            user = PhantasmaKeys.Generate();
            userKeys = user;
            this.core = core;
            simulator = core.simulator;
            nexus = core.nexus;
            baseToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, baseSymbol);
            quoteToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, quoteSymbol);
        }

        #region Exchange
        public decimal OpenLimitOrder(BigInteger orderSize, BigInteger orderPrice, ExchangeOrderSide side, bool IoC = false)
        {
            return OpenLimitOrder(UnitConversion.ToDecimal(orderSize, baseToken.Decimals), UnitConversion.ToDecimal(orderPrice, quoteToken.Decimals), side, IoC);
        }

        //Opens a limit order and returns how many tokens the user purchased/sold
        public decimal OpenLimitOrder(decimal orderSize, decimal orderPrice, ExchangeOrderSide side, bool IoC = false)
        {
            var nexus = simulator.Nexus;       

            var baseSymbol = baseToken.Symbol;
            var baseDecimals = baseToken.Decimals;
            var quoteSymbol = quoteToken.Symbol;
            var quoteDecimals = quoteToken.Decimals;

            var orderSizeBigint = UnitConversion.ToBigInteger(orderSize, baseDecimals);
            var orderPriceBigint = UnitConversion.ToBigInteger(orderPrice, quoteDecimals);

            var OpenerBaseTokensInitial = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, baseToken, user.Address);
            var OpenerQuoteTokensInitial = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, quoteToken, user.Address);

            BigInteger OpenerBaseTokensDelta = 0;
            BigInteger OpenerQuoteTokensDelta = 0;

            //get the starting balance for every address on the opposite side of the orderbook, so we can compare it to the final balance of each of those addresses
            var otherSide = side == ExchangeOrderSide.Buy ? ExchangeOrderSide.Sell : ExchangeOrderSide.Buy;
            var startingOppositeOrderbook = (ExchangeOrder[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "exchange", "GetOrderBook", baseSymbol, quoteSymbol, otherSide).ToObject();
            var OtherAddressesTokensInitial = new Dictionary<Address, BigInteger>();

            //*******************************************************************************************************************************************************************************
            //*** the following method to check token balance state only works for the scenario of a single new exchange order per block that triggers other pre-existing exchange orders ***
            //*******************************************************************************************************************************************************************************
            foreach (var oppositeOrder in startingOppositeOrderbook)
            {
                if (OtherAddressesTokensInitial.ContainsKey(oppositeOrder.Creator) == false)
                {
                    var targetSymbol = otherSide == ExchangeOrderSide.Buy ? baseSymbol : quoteSymbol;
                    var targetToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, targetSymbol);
                    OtherAddressesTokensInitial.Add(oppositeOrder.Creator, simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, targetToken, oppositeOrder.Creator));
                }
            }
            //--------------------------


            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(user.Address, Address.Null, 1, 9999)
                    .CallContract("exchange", "OpenLimitOrder", user.Address, user.Address, baseSymbol, quoteSymbol, orderSizeBigint, orderPriceBigint, side, IoC).
                    SpendGas(user.Address).EndScript());
            simulator.EndBlock();

            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            BigInteger escrowedAmount = 0;

            //take into account the transfer of the owner's wallet to the chain address
            if (side == ExchangeOrderSide.Buy)
            {
                escrowedAmount = UnitConversion.ToBigInteger(orderSize * orderPrice, quoteDecimals);
                OpenerQuoteTokensDelta -= escrowedAmount;
            }
            else if (side == ExchangeOrderSide.Sell)
            {
                escrowedAmount = orderSizeBigint;
                OpenerBaseTokensDelta -= escrowedAmount;
            }

            //take into account tx cost in case one of the symbols is the FuelToken
            if (baseSymbol == DomainSettings.FuelTokenSymbol)
            {
                OpenerBaseTokensDelta -= txCost;
            }
            else
            if (quoteSymbol == DomainSettings.FuelTokenSymbol)
            {
                OpenerQuoteTokensDelta -= txCost;
            }

            var events = nexus.FindBlockByTransaction(tx).GetEventsForTransaction(tx.Hash);

            var wasNewOrderCreated = events.Count(x => x.Kind == EventKind.OrderCreated && x.Address == user.Address) == 1;
            Assert.IsTrue(wasNewOrderCreated, "Order was not created");

            var wasNewOrderClosed = events.Count(x => x.Kind == EventKind.OrderClosed && x.Address == user.Address) == 1;
            var wasNewOrderCancelled = events.Count(x => x.Kind == EventKind.OrderCancelled && x.Address == user.Address) == 1;

            var createdOrderEvent = events.First(x => x.Kind == EventKind.OrderCreated);
            var createdOrderUid = Serialization.Unserialize<BigInteger>(createdOrderEvent.Data);
            ExchangeOrder createdOrderPostFill = new ExchangeOrder();

            //----------------
            //verify the order is still in the orderbook according to each case

            //in case the new order was IoC and it wasnt closed, order should have been cancelled
            if (wasNewOrderClosed == false && IoC)
            {
                Assert.IsTrue(wasNewOrderCancelled, "Non closed IoC order did not get cancelled");
            }
            else
            //if the new order was closed
            if (wasNewOrderClosed)
            {
                //and check that the order no longer exists on the orderbook
                try
                {
                    simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "exchange", "GetExchangeOrder", createdOrderUid);
                    Assert.IsTrue(false, "Closed order exists on the orderbooks");
                }
                catch (Exception e)
                {
                    //purposefully empty, this is the expected code-path
                }
            }
            else //if the order was not IoC and it wasn't closed, then:
            {
                Assert.IsTrue(IoC == false, "All IoC orders should have been triggered by the previous ifs");

                //check that it still exists on the orderbook
                try
                {
                    createdOrderPostFill = (ExchangeOrder)simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "exchange", "GetExchangeOrder", createdOrderUid).ToObject();
                }
                catch (Exception e)
                {
                    Assert.IsTrue(false, "Non-IoC unclosed order does not exist on the orderbooks");
                }
            }
            //------------------

            //------------------
            //validate that everyone received their tokens appropriately

            BigInteger escrowedUsage = 0;   //this will hold the amount of the escrowed amount that was actually used in the filling of the order
                                            //for IoC orders, we need to make sure that what wasn't used gets returned properly
                                            //for non IoC orders, we need to make sure that what wasn't used stays on the orderbook
            BigInteger baseTokensReceived = 0, quoteTokensReceived = 0;
            var OtherAddressesTokensDelta = new Dictionary<Address, BigInteger>();

            //*******************************************************************************************************************************************************************************
            //*** the following method to check token balance state only works for the scenario of a single new exchange order per block that triggers other pre-existing exchange orders ***
            //*******************************************************************************************************************************************************************************

            //calculate the expected delta of the balances of all addresses involved
            var tokenExchangeEvents = events.Where(x => x.Kind == EventKind.TokenClaim);

            foreach (var tokenExchangeEvent in tokenExchangeEvents)
            {
                var eventData = Serialization.Unserialize<TokenEventData>(tokenExchangeEvent.Data);

                if (tokenExchangeEvent.Address == user.Address)
                {
                    if(eventData.Symbol == baseSymbol)
                        baseTokensReceived += eventData.Value;
                    else
                    if(eventData.Symbol == quoteSymbol)
                        quoteTokensReceived += eventData.Value;
                }
                else
                {
                    Console.WriteLine("tokenExchangeEvent.Contract " + tokenExchangeEvent.Contract);
                    Console.WriteLine("tokenExchangeEvent.Address " + tokenExchangeEvent.Address);
                    Console.WriteLine("tokenExchangeEvent.Address2 " + SmartContract.GetAddressForNative(NativeContractKind.Exchange));
                    Console.WriteLine("tokenExchangeEvent.Address gas " + SmartContract.GetAddressForNative( NativeContractKind.Gas));
                    Assert.IsTrue(OtherAddressesTokensInitial.ContainsKey(tokenExchangeEvent.Address), "Address that was not on this orderbook received tokens");

                    if (OtherAddressesTokensDelta.ContainsKey(tokenExchangeEvent.Address))
                        OtherAddressesTokensDelta[tokenExchangeEvent.Address] += eventData.Value;
                    else
                        OtherAddressesTokensDelta.Add(tokenExchangeEvent.Address, eventData.Value);

                    escrowedUsage += eventData.Value;   //the tokens other addresses receive come from the escrowed amount of the order opener
                }
            }

            OpenerBaseTokensDelta += baseTokensReceived;
            OpenerQuoteTokensDelta += quoteTokensReceived;

            var expectedRemainingEscrow = escrowedAmount - escrowedUsage;

            if (IoC)
            {
                switch (side)
                {
                    case ExchangeOrderSide.Buy:
                        //Assert.IsTrue(Abs(OpenerQuoteTokensDelta) == escrowedUsage - (quoteSymbol == DomainSettings.FuelTokenSymbol ? txCost : 0));
                        break;

                    case ExchangeOrderSide.Sell:
                        //Assert.IsTrue(Abs(OpenerBaseTokensDelta) == escrowedUsage - (baseSymbol == DomainSettings.FuelTokenSymbol ? txCost : 0));
                        break;
                }
            }
            else //if the user order was not closed and it wasnt IoC, it should have the correct unfilled amount
            {
                BigInteger actualRemainingEscrow;
                if (expectedRemainingEscrow == 0)
                {
                    Assert.IsTrue(wasNewOrderClosed, "Order wasn't closed but we expect no leftover escrow");
                    try
                    {
                        //should throw an exception because order should not exist
                        simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "exchange", "GetOrderLeftoverEscrow", createdOrderUid);
                        actualRemainingEscrow = -1;
                    }
                    catch (Exception e)
                    {
                        actualRemainingEscrow = 0;
                    }
                }
                else
                {
                    actualRemainingEscrow = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "exchange", "GetOrderLeftoverEscrow", createdOrderUid).AsNumber();
                }
                
                Assert.IsTrue(expectedRemainingEscrow == actualRemainingEscrow);
            }


            //get the actual final balance of all addresses involved and make sure it matches the expected deltas
            var OpenerBaseTokensFinal = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, baseToken, user.Address);
            var OpenerQuoteTokensFinal = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, quoteToken, user.Address);

            Assert.IsTrue(OpenerBaseTokensFinal == OpenerBaseTokensDelta + OpenerBaseTokensInitial);
            Assert.IsTrue(OpenerQuoteTokensFinal == OpenerQuoteTokensDelta + OpenerQuoteTokensInitial);

            foreach (var entry in OtherAddressesTokensInitial)
            {
                var otherAddressInitialTokens = entry.Value;
                BigInteger delta = 0;

                if (OtherAddressesTokensDelta.ContainsKey(entry.Key))
                    delta = OtherAddressesTokensDelta[entry.Key];

                var targetSymbol = otherSide == ExchangeOrderSide.Buy ? baseSymbol : quoteSymbol;
                var targetToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, targetSymbol);

                var otherAddressFinalTokens = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, targetToken, entry.Key);

                Assert.IsTrue(otherAddressFinalTokens == delta + otherAddressInitialTokens);
            }

            return side == ExchangeOrderSide.Buy ? UnitConversion.ToDecimal(baseTokensReceived, baseToken.Decimals) : UnitConversion.ToDecimal(quoteTokensReceived, quoteToken.Decimals);
        }

        public decimal OpenMarketOrder(decimal orderSize, ExchangeOrderSide side)
        {
            var nexus = simulator.Nexus;

            var baseSymbol = baseToken.Symbol;
            var baseDecimals = baseToken.Decimals;
            var quoteSymbol = quoteToken.Symbol;
            var quoteDecimals = quoteToken.Decimals;

            var orderToken = side == ExchangeOrderSide.Buy ? quoteToken : baseToken;

            var orderSizeBigint = UnitConversion.ToBigInteger(orderSize, orderToken.Decimals);

            var OpenerBaseTokensInitial = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, baseToken, user.Address);
            var OpenerQuoteTokensInitial = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, quoteToken, user.Address);

            BigInteger OpenerBaseTokensDelta = 0;
            BigInteger OpenerQuoteTokensDelta = 0;

            //get the starting balance for every address on the opposite side of the orderbook, so we can compare it to the final balance of each of those addresses
            var otherSide = side == ExchangeOrderSide.Buy ? ExchangeOrderSide.Sell : ExchangeOrderSide.Buy;
            var startingOppositeOrderbook = (ExchangeOrder[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "exchange", "GetOrderBook", baseSymbol, quoteSymbol, otherSide).ToObject();
            var OtherAddressesTokensInitial = new Dictionary<Address, BigInteger>();

            //*******************************************************************************************************************************************************************************
            //*** the following method to check token balance state only works for the scenario of a single new exchange order per block that triggers other pre-existing exchange orders ***
            //*******************************************************************************************************************************************************************************
            foreach (var oppositeOrder in startingOppositeOrderbook)
            {
                if (OtherAddressesTokensInitial.ContainsKey(oppositeOrder.Creator) == false)
                {
                    var targetSymbol = otherSide == ExchangeOrderSide.Buy ? baseSymbol : quoteSymbol;
                    var targetToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, targetSymbol);
                    OtherAddressesTokensInitial.Add(oppositeOrder.Creator, simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, targetToken, oppositeOrder.Creator));
                }
            }
            //--------------------------


            if (side == ExchangeOrderSide.Buy)
            {
                Console.WriteLine("buy now");
            }
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(user.Address, Address.Null, 1, 9999)
                    .CallContract("exchange", "OpenMarketOrder", user.Address, user.Address, baseSymbol, quoteSymbol, orderSizeBigint, side).
                    SpendGas(user.Address).EndScript());
            simulator.EndBlock();

            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
            
            BigInteger escrowedAmount = orderSizeBigint;

            //take into account the transfer of the owner's wallet to the chain address
            if (side == ExchangeOrderSide.Buy)
            {
                OpenerQuoteTokensDelta -= escrowedAmount;
            }
            else if (side == ExchangeOrderSide.Sell)
            {
                OpenerBaseTokensDelta -= escrowedAmount;
            }

            //take into account tx cost in case one of the symbols is the FuelToken
            if (baseSymbol == DomainSettings.FuelTokenSymbol)
            {
                OpenerBaseTokensDelta -= txCost;
            }
            else
            if (quoteSymbol == DomainSettings.FuelTokenSymbol)
            {
                OpenerQuoteTokensDelta -= txCost;
            }

            var events = nexus.FindBlockByTransaction(tx).GetEventsForTransaction(tx.Hash);

            var ordersCreated = events.Count(x => x.Kind == EventKind.OrderCreated && x.Address == user.Address);
            var wasNewOrderCreated = ordersCreated >= 1;
            Assert.IsTrue(wasNewOrderCreated, "No orders were created");

            var ordersClosed = events.Count(x => x.Kind == EventKind.OrderClosed && x.Address == user.Address);
            var wasNewOrderClosed = ordersClosed == 1;
            var wasNewOrderCancelled = events.Count(x => x.Kind == EventKind.OrderCancelled && x.Address == user.Address) == 1;

            var createdOrderEvent = events.First(x => x.Kind == EventKind.OrderCreated);
            var createdOrderUid = Serialization.Unserialize<BigInteger>(createdOrderEvent.Data);
            ExchangeOrder createdOrderPostFill = new ExchangeOrder();

            //----------------
            //verify the order does not exist in the orderbook

            //in case the new order was IoC and it wasnt closed, order should have been cancelled
            if (wasNewOrderClosed == false)
            {
                Assert.IsTrue(wasNewOrderCancelled, "Non closed order did not get cancelled");
            }
            else
                //if the new order was closed
            if (wasNewOrderClosed)
            {
                Assert.IsTrue(wasNewOrderCancelled == false, "Closed order also got cancelled");
            }

            //check that the order no longer exists on the orderbook
            try
            {
                simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "exchange", "GetExchangeOrder", createdOrderUid);
                Assert.IsTrue(false, "Market order exists on the orderbooks");
            }
            catch (Exception e)
            {
                //purposefully empty, this is the expected code-path
            }

            //------------------
            //validate that everyone received their tokens appropriately

            BigInteger escrowedUsage = 0;   //this will hold the amount of the escrowed amount that was actually used in the filling of the order
                                            //for IoC orders, we need to make sure that what wasn't used gets returned properly
                                            //for non IoC orders, we need to make sure that what wasn't used stays on the orderbook
            BigInteger baseTokensReceived = 0, quoteTokensReceived = 0;
            var OtherAddressesTokensDelta = new Dictionary<Address, BigInteger>();

            //*******************************************************************************************************************************************************************************
            //*** the following method to check token balance state only works for the scenario of a single new exchange order per block that triggers other pre-existing exchange orders ***
            //*******************************************************************************************************************************************************************************

            //calculate the expected delta of the balances of all addresses involved

            Console.WriteLine("event count: " + events.Count());
            foreach (var evt in events)
            {
                Console.WriteLine("kind: " + evt.Kind);
            }
            var tokenExchangeEvents = events.Where(x => x.Kind == EventKind.TokenClaim);
            Console.WriteLine("exchange event count: " + tokenExchangeEvents.Count());

            foreach (var tokenExchangeEvent in tokenExchangeEvents)
            {
                var eventData = Serialization.Unserialize<TokenEventData>(tokenExchangeEvent.Data);

                if (tokenExchangeEvent.Address == user.Address)
                {
                    if (eventData.Symbol == baseSymbol)
                        baseTokensReceived += eventData.Value;
                    else
                    if (eventData.Symbol == quoteSymbol)
                        quoteTokensReceived += eventData.Value;
                }
                else
                {
                    Assert.IsTrue(OtherAddressesTokensInitial.ContainsKey(tokenExchangeEvent.Address), "Address that was not on this orderbook received tokens");

                    if (OtherAddressesTokensDelta.ContainsKey(tokenExchangeEvent.Address))
                        OtherAddressesTokensDelta[tokenExchangeEvent.Address] += eventData.Value;
                    else
                        OtherAddressesTokensDelta.Add(tokenExchangeEvent.Address, eventData.Value);

                    escrowedUsage += eventData.Value;   //the tokens other addresses receive come from the escrowed amount of the order opener
                }
            }

            OpenerBaseTokensDelta += baseTokensReceived;
            OpenerQuoteTokensDelta += quoteTokensReceived;

            var expectedRemainingEscrow = escrowedAmount - escrowedUsage;
            //Console.WriteLine("expectedRemainingEscrow: " + expectedRemainingEscrow);

            switch (side)
            {
                case ExchangeOrderSide.Buy:
                    //Console.WriteLine($"{Abs(OpenerQuoteTokensDelta)} == {escrowedUsage} - {(quoteSymbol == DomainSettings.FuelTokenSymbol ? txCost : 0)}");
                    //Assert.IsTrue(Abs(OpenerQuoteTokensDelta) == expectedRemainingEscrow - (quoteSymbol == DomainSettings.FuelTokenSymbol ? txCost : 0));
                    break;

                case ExchangeOrderSide.Sell:
                    //Assert.IsTrue(Abs(OpenerBaseTokensDelta) == expectedRemainingEscrow - (baseSymbol == DomainSettings.FuelTokenSymbol ? txCost : 0));
                    break;
            }

            //get the actual final balance of all addresses involved and make sure it matches the expected deltas
            var OpenerBaseTokensFinal = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, baseToken, user.Address);
            var OpenerQuoteTokensFinal = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, quoteToken, user.Address);

            Console.WriteLine($"final: {OpenerBaseTokensFinal} == {OpenerBaseTokensDelta} + {OpenerBaseTokensInitial}");
            Assert.IsTrue(OpenerBaseTokensFinal == OpenerBaseTokensDelta + OpenerBaseTokensInitial);
            Assert.IsTrue(OpenerQuoteTokensFinal == OpenerQuoteTokensDelta + OpenerQuoteTokensInitial);

            foreach (var entry in OtherAddressesTokensInitial)
            {
                var otherAddressInitialTokens = entry.Value;
                BigInteger delta = 0;

                if (OtherAddressesTokensDelta.ContainsKey(entry.Key))
                    delta = OtherAddressesTokensDelta[entry.Key];

                var targetSymbol = otherSide == ExchangeOrderSide.Buy ? baseSymbol : quoteSymbol;
                var targetToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, targetSymbol);

                var otherAddressFinalTokens = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, targetToken, entry.Key);

                Assert.IsTrue(otherAddressFinalTokens == delta + otherAddressInitialTokens);
            }

            return side == ExchangeOrderSide.Buy ? UnitConversion.ToDecimal(baseTokensReceived, baseToken.Decimals) : UnitConversion.ToDecimal(quoteTokensReceived, quoteToken.Decimals);
        }
        #endregion

        #region OTC
        public BigInteger OpenOTCOrder(string baseSymbol, string quoteSymbol, decimal amount, decimal price)
        {
            var amountBigint = UnitConversion.ToBigInteger(amount, GetDecimals(quoteSymbol));
            var priceBigint = UnitConversion.ToBigInteger(price, GetDecimals(baseSymbol));

            // Create OTC Order
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(user.Address, Address.Null, 1, 9999)
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.OpenOTCOrder), user.Address, baseSymbol, quoteSymbol, amountBigint, priceBigint)
                    .SpendGas(user.Address)
                    .EndScript());
            simulator.EndBlock();

            // Get Tx Cost
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
            return txCost;
        }

        public BigInteger TakeOTCOrder(BigInteger uid)
        {
            // Take an Order
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(user.Address, Address.Null, 1, 9999)
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.TakeOrder), user.Address, uid)
                    .SpendGas(user.Address)
                    .EndScript());
            simulator.EndBlock();

            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
            return txCost;
        }

        public void CancelOTCOrder(BigInteger uid)
        {
            // Take an Order
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(user.Address, Address.Null, 1, 9999)
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.CancelOTCOrder), user.Address, uid)
                    .SpendGas(user.Address)
                    .EndScript());
            simulator.EndBlock();
        }

        // Get OTC Orders
        public ExchangeOrder[] GetOTC()
        {
            return (ExchangeOrder[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "exchange", "GetOTC").ToObject();
        }
        #endregion
        
        #region DEX
        public BigInteger AddLiquidity(string baseSymbol, BigInteger amount0, string pairSymbol, BigInteger amount1)
        {
            
            // Add Liquidity to the pool
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.Minimal, () =>
                ScriptUtils
                    .BeginScript()
                    .AllowGas(user.Address, Address.Null, 1, 9999)
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.AddLiquidity), user.Address, baseSymbol, amount0, pairSymbol, amount1)
                    .SpendGas(user.Address)
                    .EndScript()
            );
            var block = simulator.EndBlock().First();

            // Get Tx Cost
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
            return txCost;
        }
        
        public BigInteger RemoveLiquidity(string poolSymbol0, BigInteger poolAmount0, string poolSymbol1, BigInteger poolAmount1)
        {
            // SOUL / KCAL
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.Minimal, () =>
                ScriptUtils
                    .BeginScript()
                    .AllowGas(user.Address, Address.Null, 1, 9999)
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.RemoveLiquidity), user.Address, poolSymbol0, poolAmount0, poolSymbol1, poolAmount1)
                    .SpendGas(user.Address)
                    .EndScript());
            var block = simulator.EndBlock().First();
            var resultBytes = block.GetResultForTransaction(tx.Hash);
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
            return txCost;
        }

        public BigInteger CreatePool(string poolSymbol0, BigInteger poolAmount0, string poolSymbol1, BigInteger poolAmount1)
        {
            // SOUL / KCAL
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.Minimal, () =>
                ScriptUtils
                    .BeginScript()
                    .AllowGas(user.Address, Address.Null, 1, 9999)
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.CreatePool), user.Address, poolSymbol0, poolAmount0, poolSymbol1, poolAmount1)
                    .SpendGas(user.Address)
                    .EndScript());
            var block = simulator.EndBlock().First();
            var resultBytes = block.GetResultForTransaction(tx.Hash);
            
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
            return txCost;
        }

        public BigInteger SwapTokens(string poolSymbol0, string poolSymbol1, BigInteger amount)
        {
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.Minimal, () =>
                ScriptUtils
                    .BeginScript()
                    .AllowGas(user.Address, Address.Null, 1, 9999)
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.SwapTokens), user.Address, poolSymbol0, poolSymbol1, amount)
                    .SpendGas(user.Address)
                    .EndScript()
            );
            var block = simulator.EndBlock().First();
            
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
            return txCost;
        }

        public BigInteger SwapFee(string poolSymbol0, BigInteger amount)
        {
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.Minimal, () =>
                ScriptUtils
                    .BeginScript()
                    .AllowGas(user.Address, Address.Null, 1, 500)
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.SwapFee), user.Address, poolSymbol0, amount)
                    .SpendGas(user.Address)
                    .EndScript()
            );
            var block = simulator.EndBlock().First();
            
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
            return txCost;
        }

        public BigInteger ClaimFees(string poolSymbol0, string poolSymbol1)
        {
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.Minimal, () =>
                ScriptUtils
                    .BeginScript()
                    .AllowGas(user.Address, Address.Null, 1, 9999)
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.ClaimFees), user.Address, poolSymbol0, poolSymbol1)
                    .SpendGas(user.Address)
                    .EndScript()
            );
            var block = simulator.EndBlock().First();
            
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
            return txCost;
        }

        public Pool GetPool(string poolSymbol0, string poolSymbol1)
        {
            var script = new ScriptBuilder()
                .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.GetPool), poolSymbol0, poolSymbol1)
                .EndScript();
            var result = simulator.Nexus.RootChain.InvokeScript(core.nexus.RootStorage, script);
            var pool = result.AsStruct<Pool>();
            return pool;
        }

        public LPTokenContentRAM GetPoolRAM(string poolSymbol0, string poolSymbol1)
        {
            var script = new ScriptBuilder()
                .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.GetMyPoolRAM), user.Address, poolSymbol0, poolSymbol1)
                .EndScript();
            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);
            var nftRAM = result.AsStruct<LPTokenContentRAM>();
            return nftRAM;
        }

        public BigInteger GetUnclaimedFees(string poolSymbol0, string poolSymbol1)
        {
            var script = new ScriptBuilder()
                .CallContract("swap", "GetUnclaimedFees", user.Address, poolSymbol0, poolSymbol1)
                .EndScript();
            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);
            var unclaimed = (BigInteger)result.AsNumber();
            return unclaimed;
        }


        public BigInteger GetRate(string poolSymbol0, string poolSymbol1, BigInteger amount)
        {
            var script = new ScriptBuilder()
                .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.GetRate), poolSymbol0, poolSymbol1, amount)
                .EndScript();

            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);

            var rate = result.AsNumber();
            return rate;
        }

        #endregion
        
        public void Fund(string symbol, BigInteger amount)
        {
            var chain = simulator.Nexus.RootChain as Chain;

            simulator.BeginBlock();
            var txA = simulator.GenerateTransfer(core.owner, user.Address, chain, symbol, amount);
            simulator.EndBlock();
        }

        public void FundUser(decimal soul, decimal kcal)
        {
            var chain = simulator.Nexus.RootChain as Chain;

            simulator.BeginBlock();
            var txA = simulator.GenerateTransfer(core.owner, user.Address, chain, DomainSettings.StakingTokenSymbol, UnitConversion.ToBigInteger(soul, DomainSettings.StakingTokenDecimals));
            var txB = simulator.GenerateTransfer(core.owner, user.Address, chain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(kcal, DomainSettings.FuelTokenDecimals));
            simulator.EndBlock();
        }
        
        public void FundBaseToken(decimal quantity, bool fundFuel = false) => FundUser(true, quantity, fundFuel);
        public void FundQuoteToken(decimal quantity, bool fundFuel = false) => FundUser(false, quantity, fundFuel);


        //transfers the given quantity of a specified token to this user, plus some fuel to pay for transactions
        private void FundUser(bool fundBase, decimal quantity, bool fundFuel = false)
        {
            var nexus = simulator.Nexus;
            var token = fundBase ? baseToken : quoteToken;

            var chain = simulator.Nexus.RootChain as Chain;

            simulator.BeginBlock();
            simulator.GenerateTransfer(core.owner, user.Address, chain, token.Symbol, UnitConversion.ToBigInteger(quantity, GetDecimals(token.Symbol)));

            if (fundFuel)
                simulator.GenerateTransfer(core.owner, user.Address, chain, DomainSettings.FuelTokenSymbol, 500000);

            simulator.EndBlock();
        }

        public BigInteger GetBalance(string symbol)
        {
            var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);
            return nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, user.Address);
        }
    }
    #endregion
}
