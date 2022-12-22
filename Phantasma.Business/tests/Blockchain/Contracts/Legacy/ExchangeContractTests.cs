using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;

using Xunit;
using Shouldly;

namespace Phantasma.Business.Tests.Blockchain.Contracts.Legacy;

[Collection(nameof(SystemTestCollectionDefinition))]
public class ExchangeContractTests
{
    private const string maxDivTokenSymbol = "MADT";        //divisible token with maximum decimal count
    private const string minDivTokenSymbol = "MIDT";        //divisible token with minimum decimal count
    private const string nonDivisibleTokenSymbol = "NDT";

    #region Exchange
    [Fact(Skip = "Skip")]
    public void TestIoCLimitMinimumQuantity()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;

        var baseSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(baseSymbol));
        var quoteSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(quoteSymbol));

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: baseSymbolAmount, fundFuel: true);
        seller.FundBaseToken(quantity: quoteSymbolAmount, fundFuel: true);

        //-----------------------------------------
        //test order amount and prices at the limit
        var qtyBase = core.simulator.InvokeContract(NativeContractKind.Exchange, nameof(ExchangeContract.GetMinimumQuantity), buyer.baseToken.Decimals).AsNumber();
        var qtyQuote = core.simulator.InvokeContract(NativeContractKind.Exchange, nameof(ExchangeContract.GetMinimumQuantity), buyer.quoteToken.Decimals).AsNumber();

        buyer.OpenLimitOrder(baseSymbol, quoteSymbol, qtyBase, qtyQuote, ExchangeOrderSide.Buy);
        seller.OpenLimitOrder(baseSymbol, quoteSymbol, qtyBase, qtyQuote, ExchangeOrderSide.Sell);

        var orderSizeBase = UnitConversion.ToBigInteger(1, GetDecimals(baseSymbol));
        var orderPriceBase = UnitConversion.ToBigInteger(1, GetDecimals(quoteSymbol));

        buyer.OpenLimitOrder(baseSymbol, quoteSymbol, orderSizeBase, orderPriceBase, ExchangeOrderSide.Buy);
        buyer.OpenLimitOrder(baseSymbol, quoteSymbol, orderSizeBase, orderPriceBase, ExchangeOrderSide.Buy);

        var seller_orderSize = orderSizeBase + (qtyBase * 100 / 99);
        
        seller.OpenLimitOrder(baseSymbol, quoteSymbol, seller_orderSize, orderPriceBase, ExchangeOrderSide.Sell);
        Assert.True(core.simulator.LastBlockWasSuccessful(), "Used leftover under minimum quantity");
    }

    [Fact(Skip = "Ignore Exchange tests")]
    public void TestIoCLimitOrderUnmatched()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;
        
        var baseSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(baseSymbol));
        var quoteSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(quoteSymbol));

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: baseSymbolAmount, fundFuel: true);
        seller.FundBaseToken(quantity: quoteSymbolAmount, fundFuel: true);

        //-----------------------------------------
        //test unmatched IoC orders 
        seller.OpenLimitOrder(0.01m, 0.5m, ExchangeOrderSide.Sell);
        buyer.OpenLimitOrder(0.01m, 0.1m, ExchangeOrderSide.Buy);
        Assert.True(buyer.OpenLimitOrder(0.123m, 0.3m, ExchangeOrderSide.Buy, IoC: true) == 0, "Shouldn't have filled any part of the order");
        Assert.True(seller.OpenLimitOrder(0.123m, 0.3m, ExchangeOrderSide.Sell, IoC: true) == 0, "Shouldn't have filled any part of the order");
    }

    [Fact(Skip = "Ignore Exchange tests")]
    public void TestIoCLimitOrderCompleteFulfilment()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;
        
        var baseSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(baseSymbol));
        var quoteSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(quoteSymbol));

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: baseSymbolAmount, fundFuel: true);
        seller.FundBaseToken(quantity: quoteSymbolAmount, fundFuel: true);

        //-----------------------------------------
        //test fully matched IoC orders
        buyer.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Buy, IoC: false);
        Assert.True(seller.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Sell, IoC: true) == 0.1m, "Unexpected amount of tokens received");

        seller.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Sell, IoC: false);
        Assert.True(buyer.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Buy, IoC: true) == 0.1m, "Unexpected amount of tokens received");
    }

    [Fact(Skip = "Ignore Exchange tests")]
    public void TestIoCLimitOrderPartialFulfilment()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;
        
        var baseSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(baseSymbol));
        var quoteSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(quoteSymbol));

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: baseSymbolAmount, fundFuel: true);
        seller.FundBaseToken(quantity: quoteSymbolAmount, fundFuel: true);

        //-----------------------------------------
        //test partially matched IoC orders
        buyer.OpenLimitOrder(0.05m, 1m, ExchangeOrderSide.Buy, IoC: false);
        Assert.True(seller.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Sell, IoC: true) == 0.05m, "Unexpected amount of tokens received");

        seller.OpenLimitOrder(0.05m, 1m, ExchangeOrderSide.Sell, IoC: false);
        Assert.True(buyer.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Buy, IoC: true) == 0.05m, "Unexpected amount of tokens received");
    }
    
    [Fact(Skip = "Ignore Exchange tests")]
    public void TestIoCLimitOrderMultipleFulfilsPerOrder()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;
        
        var baseSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(baseSymbol));
        var quoteSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(quoteSymbol));

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: baseSymbolAmount, fundFuel: true);
        seller.FundBaseToken(quantity: quoteSymbolAmount, fundFuel: true);

        //-----------------------------------------
        //test multiple fills per order
        buyer.OpenLimitOrder(0.05m, 1m, ExchangeOrderSide.Buy, IoC: false);
        buyer.OpenLimitOrder(0.05m, 2m, ExchangeOrderSide.Buy, IoC: false);
        buyer.OpenLimitOrder(0.05m, 3m, ExchangeOrderSide.Buy, IoC: false);
        buyer.OpenLimitOrder(0.05m, 0.5m, ExchangeOrderSide.Buy, IoC: false);
        Assert.True(seller.OpenLimitOrder(0.15m, 1m, ExchangeOrderSide.Sell, IoC: true) == 0.3m, "Unexpected amount of tokens received");

        core = new CoreClass();
        buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        seller = new ExchangeUser(baseSymbol, quoteSymbol, core);
        buyer.FundQuoteToken(quantity: quoteSymbolAmount, fundFuel: true);
        seller.FundBaseToken(quantity: baseSymbolAmount, fundFuel: true);

        seller.OpenLimitOrder(0.05m, 1m, ExchangeOrderSide.Sell, IoC: false);
        seller.OpenLimitOrder(0.05m, 2m, ExchangeOrderSide.Sell, IoC: false);
        seller.OpenLimitOrder(0.05m, 3m, ExchangeOrderSide.Sell, IoC: false);
        seller.OpenLimitOrder(0.05m, 0.5m, ExchangeOrderSide.Sell, IoC: false);
        Assert.True(buyer.OpenLimitOrder(0.15m, 3m, ExchangeOrderSide.Buy, IoC: true) == 0.2m, "Unexpected amount of tokens received");

        //TODO: test multiple IoC orders against each other on the same block!
    }

    [Fact]
    public void TestFailedIOC()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;
        
        var baseSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(baseSymbol));
        var quoteSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(quoteSymbol));

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: baseSymbolAmount, fundFuel: true);
        seller.FundBaseToken(quantity: quoteSymbolAmount, fundFuel: true);
        

        //-----------------------------------------
        //test order amount and prices below limit
        var orderPrice = UnitConversion.ToBigInteger(0.5m, GetDecimals(quoteSymbol));
        
        buyer.OpenLimitOrder(baseSymbol, quoteSymbol, 0, orderPrice, ExchangeOrderSide.Buy, IoC: true);
        Assert.False(core.simulator.LastBlockWasSuccessful());
        //Assert.True(false, "Order should fail due to insufficient amount");
        
        buyer.OpenLimitOrder(baseSymbol, quoteSymbol,orderPrice, 0, ExchangeOrderSide.Buy, IoC: true);
        Assert.False(core.simulator.LastBlockWasSuccessful());
        
        var orderPrices = UnitConversion.ToBigInteger(0.3m, GetDecimals(quoteSymbol));
        var orderSize = UnitConversion.ToBigInteger(0.123m, GetDecimals(baseSymbol));

        buyer.OpenLimitOrder(baseSymbol, quoteSymbol, orderSize, orderPrices, ExchangeOrderSide.Buy, IoC: true);
        Assert.False(core.simulator.LastBlockWasSuccessful(), "Shouldn't have filled any part of the order");
        seller.OpenLimitOrder(baseSymbol, quoteSymbol, orderSize, orderPrices, ExchangeOrderSide.Sell, IoC: true);
        Assert.False(core.simulator.LastBlockWasSuccessful(), "Shouldn't have filled any part of the order");
    }

    [Fact(Skip = "Ignore Exchange tests")]
    public void TestLimitMinimumQuantity()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;
        
        var baseSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(baseSymbol));
        var quoteSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(quoteSymbol));

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: quoteSymbolAmount, fundFuel: true);
        seller.FundBaseToken(quantity: baseSymbolAmount, fundFuel: true);

        //-----------------------------------------
        //test order amount and prices at the limit

        var minimumBaseToken = UnitConversion.ToDecimal(core.simulator.InvokeContract(NativeContractKind.Exchange, "GetMinimumTokenQuantity", buyer.baseToken).AsNumber(), buyer.baseToken.Decimals);
        var minimumQuoteToken = UnitConversion.ToDecimal(core.simulator.InvokeContract(NativeContractKind.Exchange, "GetMinimumTokenQuantity", buyer.quoteToken).AsNumber(), buyer.baseToken.Decimals);

        buyer.OpenLimitOrder(minimumBaseToken, minimumQuoteToken, ExchangeOrderSide.Buy);
        seller.OpenLimitOrder(minimumBaseToken, minimumQuoteToken, ExchangeOrderSide.Sell);
    }

    [Fact(Skip = "Ignore Exchange tests")]
    public void TestLimitOrderUnmatched()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;
        
        var baseSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(baseSymbol));
        var quoteSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(quoteSymbol));

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: quoteSymbolAmount, fundFuel: true);
        seller.FundBaseToken(quantity: baseSymbolAmount, fundFuel: true);

        //-----------------------------------------
        //test unmatched IoC orders 
        seller.OpenLimitOrder(0.01m, 0.5m, ExchangeOrderSide.Sell);
        buyer.OpenLimitOrder(0.01m, 0.1m, ExchangeOrderSide.Buy);
        Assert.True(buyer.OpenLimitOrder(0.123m, 0.3m, ExchangeOrderSide.Buy, IoC: true) == 0, "Shouldn't have filled any part of the order");
        Assert.True(seller.OpenLimitOrder(0.123m, 0.3m, ExchangeOrderSide.Sell, IoC: true) == 0, "Shouldn't have filled any part of the order");
    }

    [Fact(Skip = "Ignore Exchange tests")]
    public void TestLimitOrderCompleteFulfilment()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;
        
        var baseSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(baseSymbol));
        var quoteSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(quoteSymbol));

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: quoteSymbolAmount, fundFuel: true);
        seller.FundBaseToken(quantity: baseSymbolAmount, fundFuel: true);

        //-----------------------------------------
        //test fully matched IoC orders
        buyer.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Buy, IoC: false);
        Assert.True(seller.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Sell, IoC: true) == 0.1m, "Unexpected amount of tokens received");

        seller.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Sell, IoC: false);
        Assert.True(buyer.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Buy, IoC: true) == 0.1m, "Unexpected amount of tokens received");
    }

    [Fact(Skip = "Ignore Exchange tests")]
    public void TestLimitOrderPartialFulfilment()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;

        var baseSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(baseSymbol));
        var quoteSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(quoteSymbol));
        
        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: quoteSymbolAmount, fundFuel: true);
        seller.FundBaseToken(quantity: baseSymbolAmount, fundFuel: true);

        //-----------------------------------------
        //test partially matched IoC orders
        buyer.OpenLimitOrder(0.05m, 1m, ExchangeOrderSide.Buy, IoC: false);
        Assert.True(seller.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Sell, IoC: true) == 0.05m, "Unexpected amount of tokens received");

        seller.OpenLimitOrder(0.05m, 1m, ExchangeOrderSide.Sell, IoC: false);
        Assert.True(buyer.OpenLimitOrder(0.1m, 1m, ExchangeOrderSide.Buy, IoC: true) == 0.05m, "Unexpected amount of tokens received");
    }

    [Fact(Skip = "Ignore Exchange tests")]
    public void TestLimitOrderMultipleFulfilsPerOrder()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;

        var baseSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(baseSymbol));
        var quoteSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(quoteSymbol));
        
        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: quoteSymbolAmount, fundFuel: true);
        seller.FundBaseToken(quantity: baseSymbolAmount, fundFuel: true);

        //-----------------------------------------
        //test multiple fills per order
        buyer.OpenLimitOrder(0.05m, 1m, ExchangeOrderSide.Buy, IoC: false);
        buyer.OpenLimitOrder(0.05m, 2m, ExchangeOrderSide.Buy, IoC: false);
        buyer.OpenLimitOrder(0.05m, 3m, ExchangeOrderSide.Buy, IoC: false);
        buyer.OpenLimitOrder(0.05m, 0.5m, ExchangeOrderSide.Buy, IoC: false);
        Assert.True(seller.OpenLimitOrder(0.15m, 1m, ExchangeOrderSide.Sell, IoC: true) == 0.3m, "Unexpected amount of tokens received");

        buyer.FundQuoteToken(quantity: quoteSymbolAmount, fundFuel: true);
        seller.FundBaseToken(quantity: baseSymbolAmount, fundFuel: true);

        seller.OpenLimitOrder(0.05m, 1m, ExchangeOrderSide.Sell, IoC: false);
        seller.OpenLimitOrder(0.05m, 2m, ExchangeOrderSide.Sell, IoC: false);
        seller.OpenLimitOrder(0.05m, 3m, ExchangeOrderSide.Sell, IoC: false);
        seller.OpenLimitOrder(0.05m, 0.5m, ExchangeOrderSide.Sell, IoC: false);
        Assert.True(buyer.OpenLimitOrder(0.15m, 3m, ExchangeOrderSide.Buy, IoC: true) == 0.2m, "Unexpected amount of tokens received");

        //TODO: test multiple IoC orders against each other on the same block!
    }

    [Fact]
    public void TestFailedRegular()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;
        
        var baseSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(baseSymbol));
        var quoteSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(quoteSymbol));
        
        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: quoteSymbolAmount, fundFuel: true);
        seller.FundBaseToken(quantity: baseSymbolAmount, fundFuel: true);

        //-----------------------------------------
        //test order amount and prices below limit
        try
        {
            buyer.OpenLimitOrder(0, 0.5m, ExchangeOrderSide.Buy);
            Assert.True(false, "Order should fail due to insufficient amount");
        }
        catch (Exception e) { }
        try
        {
            buyer.OpenLimitOrder(0.5m, 0, ExchangeOrderSide.Buy);
            Assert.True(false, "Order should fail due to insufficient price");
        }
        catch (Exception e) { }
    }

    [Fact]
    public void TestEmptyBookMarketOrder()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;

        var baseSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(baseSymbol));
        var quoteSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(quoteSymbol));
        
        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: quoteSymbolAmount, fundFuel: true);
        seller.FundBaseToken(quantity: baseSymbolAmount, fundFuel: true);

        Assert.True(buyer.OpenMarketOrder(1, ExchangeOrderSide.Buy) == 0, "Should not have bought anything");
    }

    [Fact(Skip = "Ignore Exchange tests")]
    public void TestMarketOrderPartialFill()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;

        var baseSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(baseSymbol));
        var quoteSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(quoteSymbol));
        
        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: quoteSymbolAmount, fundFuel: true);
        seller.FundBaseToken(quantity: baseSymbolAmount, fundFuel: true);

        seller.OpenLimitOrder(0.2m, 1m, ExchangeOrderSide.Sell);
        Assert.True(buyer.OpenMarketOrder(0.3m, ExchangeOrderSide.Buy) == 0.2m, "");
    }

    [Fact(Skip = "Ignore Exchange tests")]
    public void TestMarketOrderCompleteFulfilment()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;
        
        var baseSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(baseSymbol));
        var quoteSymbolAmount = UnitConversion.ToBigInteger(2, GetDecimals(quoteSymbol));

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: quoteSymbolAmount, fundFuel: true);
        seller.FundBaseToken(quantity: baseSymbolAmount, fundFuel: true);
        
        var orderSize1 = UnitConversion.ToBigInteger(0.1m, GetDecimals(baseSymbol));
        var orderSize2 = UnitConversion.ToBigInteger(0.1m, GetDecimals(baseSymbol));


        seller.OpenLimitOrder(baseSymbol, quoteSymbol, orderSize1, 1, ExchangeOrderSide.Sell);
        seller.OpenLimitOrder(baseSymbol, quoteSymbol, orderSize2, 2, ExchangeOrderSide.Sell);

        var marketOrder = buyer.OpenMarketOrder( 0.3m, ExchangeOrderSide.Buy);

        Assert.True(marketOrder == 0.2m, $"{marketOrder} == 0.2m");
    }

    [Fact]
    public void TestMarketOrderTotalFillNoOrderbookWipe()
    {
        CoreClass core = new CoreClass();

        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = maxDivTokenSymbol;
        
        var baseSymbolAmount = UnitConversion.ToBigInteger(5, GetDecimals(baseSymbol));
        var quoteSymbolAmount = UnitConversion.ToBigInteger(5, GetDecimals(quoteSymbol));

        var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

        buyer.FundQuoteToken(quantity: quoteSymbolAmount, fundFuel: true);
        seller.FundBaseToken(quantity: baseSymbolAmount, fundFuel: true);
        
        var orderSize1 = UnitConversion.ToBigInteger(0.1m, GetDecimals(baseSymbol));
        var orderSize2 = UnitConversion.ToBigInteger(0.1m, GetDecimals(baseSymbol));
        var orderPrice1 = UnitConversion.ToBigInteger(1, GetDecimals(quoteSymbol));
        var orderPrice2 = UnitConversion.ToBigInteger(2, GetDecimals(quoteSymbol));

        seller.OpenLimitOrder(baseSymbol, quoteSymbol, orderSize1, orderPrice1, ExchangeOrderSide.Sell);
        seller.OpenLimitOrder(baseSymbol, quoteSymbol, orderSize2, orderPrice2, ExchangeOrderSide.Sell);
        Assert.True(buyer.OpenMarketOrder(0.25m, ExchangeOrderSide.Buy) == 0.175m, "");
    }
    
    #endregion
    
    #region OTC Tests
    [Fact]
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
        Assert.True(initialBalance == UnitConversion.ToBigInteger(500, GetDecimals(baseSymbol)));

        // Create OTC Offer
        var txValue = seller.OpenOTCOrder(baseSymbol, quoteSymbol, 1m, 2m);

        // Test if the seller lost money.
        var finalBalance = seller.GetBalance(baseSymbol);

        Assert.False(initialBalance == finalBalance, $"{initialBalance} == {finalBalance}");

        // Test if lost the quantity used
        var subtractSpendToken = initialBalance - UnitConversion.ToBigInteger(2m, GetDecimals(baseSymbol));
        Assert.True(subtractSpendToken == finalBalance, $"{subtractSpendToken} == {finalBalance}");
    }

    [Fact]
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

        Assert.True(initialOTC.Length == 0);

        // Create an Order
        seller.OpenOTCOrder(baseSymbol, quoteSymbol, 1m, 1m);

        // Test if theres an order
        var finallOTC = seller.GetOTC();

        Assert.True(initialOTC != finallOTC);
    }


    [Fact]
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

        Assert.False(initialSeller_B == finalSeller_B);

        // Test if lost the quantity used
        Assert.True((initialSeller_B - UnitConversion.ToBigInteger(10m, GetDecimals(baseSymbol))) == finalSeller_B);

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
        Assert.True((initialSeller_Q + UnitConversion.ToBigInteger(5m, GetDecimals(quoteSymbol)) - sellerTXFees) == finalSeller_Q);

        // Test Buyer spend and receibed
        Assert.True((initialBuyer_B + UnitConversion.ToBigInteger(10m, GetDecimals(baseSymbol))) == finalBuyer_B);
        Assert.True((initialBuyer_Q - UnitConversion.ToBigInteger(5m, GetDecimals(quoteSymbol)) - buyerTXFees) == finalBuyer_Q);

    }

    [Fact]
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

        Assert.False(initialBalance == finalBalance);

        // Test if lost the quantity used
        Assert.True((initialBalance - UnitConversion.ToBigInteger(5m, GetDecimals(baseSymbol))) == finalBalance);

        // Cancel Order
        // Get Order UID
        var orderUID = seller.GetOTC().First<ExchangeOrder>().Uid;
        seller.CancelOTCOrder(orderUID);

        // Test if the token is back;
        var atualBalance = seller.GetBalance(baseSymbol);

        Assert.True(initialBalance == atualBalance);
    }
    #endregion

    #region DEX
    // Token Values
    private static string LP_ContractCompiled_PVM =
        "000D01041C5068616E7461736D61204C69717569646974792050726F76696465720301082700000B000D0104024C500301083500000B000D010601000301084200000B000D010601010301084F00000B000D010301080301085C00000B000D010601000301086900000B000D010301000301087600000B000D010408446174612E4765740D0204024C500D0003010803000D0004065F6F776E6572030003020701040303030D00040941646472657373282907000403020301030108BF00000B000D00040F52756E74696D652E56657273696F6E070004000D010301001A0001000A001E010D00043243757272656E74206E657875732070726F746F636F6C2076657273696F6E2073686F756C642062652030206F72206D6F72650C0000040303030D000409416464726573732829070004030203040204010D040601000204020D05026C04076765744E616D6504000000000107746F6B656E4944030E6765744465736372697074696F6E045D0000000107746F6B656E4944030B676574496D61676555524C04F10000000107746F6B656E4944030A676574496E666F55524C041E0100000107746F6B656E4944030003050D0502FD52010004010D0004066D696E744944030003010D0004024C5003000D00041152756E74696D652E52656164546F6B656E070004023202020D0004066D696E744944300203000D0104044C5020230203020E020204230102040304085C00000B0004010D000403524F4D030003010D0004024C5003000D00041152756E74696D652E52656164546F6B656E070004023202020D000403524F4D300203003203030D0104124C6971756469747920666F7220706F6F6C200203020D04040753796D626F6C30300202040D040403202F200203050D06040753796D626F6C3130050506230405062302060423010402030208F000000B0004010D01041F68747470733A2F2F7068616E7461736D612E696F2F696D672F6C702E706E670301081D01000B0004010201020D01041868747470733A2F2F7068616E7461736D612E696F2F6C702F0202030E030304230103040304085101000B03050D0507040000000003050D0503010003050D0503010003050D0504024C50030502010503050D0404174E657875732E437265617465546F6B656E53657269657307040D05040B4D696772617465546F563303050D04040865786368616E67652D04042E04000D030408446174612E53657403010D0004065F6F776E65720300070303020D0004085F6368616E676564030007030B000D010408446174612E4765740D0204024C500D0003010803000D0004065F6F776E6572030003020701040303030D00040941646472657373282907000403040103010D000409416464726573732829070004010402320202040432040402030603060D05041152756E74696D652E49735769746E65737307050405090536040D06040E7769746E657373206661696C65640C06000D0703010103070204070E07070203070202070E07070203070D0704024C50030702010703070D0702220200FACEE6DD84C950B54361FB71DFE736CDC64D894294305B4FEEAF3C61ABF2F2F703070D0004094164647265737328290700040703070D06041152756E74696D652E4D696E74546F6B656E07060406020605020506030608BC04000B000D010408446174612E4765740D0204024C500D0003010803000D0004065F6F776E6572030003020701040303030D000409416464726573732829070004030D0003010603000D0004085F6368616E6765640300030207010404040103010D00040941646472657373282907000401020402150202090256050D0504194F776E65722077617320616C7265616479206368616E6765640C050002030503050D02041152756E74696D652E49735769746E6573730702040209028D050D05040E7769746E657373206661696C65640C05000D000420416464726573732E697353797374656D206E6F7420696D706C656D656E7465640C000902E5050D050427746865206E65772061646472657373206973206E6F7420612073797374656D20616464726573730C05000201020202030D02060101020204000D010408446174612E53657403030D0004065F6F776E65720300070103040D0004085F6368616E676564030007010B000D02040F52756E74696D652E436F6E74657874070204020202010D03041752756E74696D652E50726576696F7573436F6E74657874070304030203020D030403633A20020104230304050D030407207C7C20703A2002020423030406230506030303088A06000B00040103010D000409416464726573732829070004010D0204174E6F7420616C6C6F77656420746F20757067726164652E0C02000B000D010408446174612E4765740D0204024C500D0003010803000D0004065F6F776E6572030003020701040303030D000409416464726573732829070004030D0003010603000D0004085F6368616E6765640300030207010404040103010D00040941646472657373282907000401040203020D0004094164647265737328290700040202040515050509056E070D0604194F776E65722077617320616C7265616479206368616E6765640C060002030603060D05041152756E74696D652E49735769746E657373070504050905A5070D06040E7769746E657373206661696C65640C06000D000420416464726573732E697353797374656D206E6F7420696D706C656D656E7465640C000905FD070D060427746865206E65772061646472657373206973206E6F7420612073797374656D20616464726573730C0600020105020306190506070A071A080202050205030D0506010102050400000D010408446174612E53657403030D0004065F6F776E65720300070103040D0004085F6368616E676564030007010B000D010408446174612E4765740D0204024C500D0003010803000D0004065F6F776E6572030003020701040303030D00040941646472657373282907000403040103010D00040941646472657373282907000401040203020D00040941646472657373282907000402040404050E0505030D0704024C50020706020407020608190708090909E7080D07040E696E76616C69642073796D626F6C0C070002030803080D07041152756E74696D652E49735769746E6573730707040709071E090D08040E7769746E657373206661696C65640C0800082209000B00040103010D0004094164647265737328290700040104020E02020302010403040D03041152756E74696D652E49735769746E65737307030403090376090D04040E7769746E657373206661696C65640C0400087A09000B00040103010D00040941646472657373282907000401040203020D00040941646472657373282907000402040304040E04040302010603060D05041152756E74696D652E49735769746E657373070504050905E5090D06040E7769746E657373206661696C65640C060008E909000B";

    private static string LP_ContractCompiled_ABI =
        "11076765744E616D650400000000000967657453796D626F6C0428000000000E69735472616E7366657261626C650636000000000A69734275726E61626C650643000000000B676574446563696D616C7303500000000008697346696E697465065D000000000C6765744D6178537570706C79036A00000000086765744F776E65720877000000000A496E697469616C697A6500C0000000010D636F6E74726163744F776E657208044D696E7403A1030000030466726F6D0803726F6D010372616D010B4368616E67654F776E657200BD040000010466726F6D080B74657374436F6E74657874042306000000096F6E55706772616465008B060000010466726F6D08096F6E4D69677261746500C0060000020466726F6D0802746F08066F6E4D696E74004A080000040466726F6D0802746F080673796D626F6C0407746F6B656E494403046275726E0023090000020466726F6D0807746F6B656E494403066F6E4275726E007B090000040466726F6D0802746F080673796D626F6C0407746F6B656E49440300";
    
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
    
    public static TokenFlags flags = TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite | TokenFlags.Divisible;

    static CoreClass.ExchangeTokenInfo soul = new CoreClass.ExchangeTokenInfo("SOUL", "Phantasma SOUL", poolAmount0*100, 8, flags );
    static CoreClass.ExchangeTokenInfo kcal = new CoreClass.ExchangeTokenInfo("KCAL", "Phantasma KCAL", poolAmount1*100, 10, flags );
    static CoreClass.ExchangeTokenInfo eth = new CoreClass.ExchangeTokenInfo("PETH", "Phantasma ETH", poolAmount2*100, 18, flags );
    static CoreClass.ExchangeTokenInfo bnb = new CoreClass.ExchangeTokenInfo("PBNB", "Phantasma BNB", poolAmount3*100, 18, flags );
    static CoreClass.ExchangeTokenInfo neo = new CoreClass.ExchangeTokenInfo("PNEO", "Phantasma NEO", poolAmount4*100, 1, flags );
    static CoreClass.ExchangeTokenInfo gas = new CoreClass.ExchangeTokenInfo("PGAS", "Phantasma GAS", poolAmount5 *100, 8, flags);
    static CoreClass.ExchangeTokenInfo cool = new CoreClass.ExchangeTokenInfo("COOL", "Phantasma Cool", UnitConversion.ToBigInteger(10000000, 8), 8, flags );


    // Virtual Token
    static string virtualPoolSymbol = "COOL";
    static BigInteger virtualPoolAmount1 = UnitConversion.ToBigInteger(10000000, 10);
    
    private static Address ExchangeAddress = SmartContract.GetAddressForNative(NativeContractKind.Exchange);
    
    private void SetupNormalPool()
    {
        CoreClass core = new CoreClass();

        // Create users
        var poolOwner = new ExchangeUser(DomainSettings.StakingTokenSymbol, DomainSettings.FuelTokenSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 500, kcal: 100);

        // SOUL / KCAL
        poolOwner.CreatePool(soul.Symbol, poolAmount0, eth.Symbol, poolAmount1);

        // SOUL / ETH
        poolOwner.CreatePool(soul.Symbol, poolAmount0, eth.Symbol, poolAmount2);

        // SOUL / NEO
        poolOwner.CreatePool(soul.Symbol, poolAmount0, neo.Symbol, poolAmount4);

        // SOUL / GAS
        poolOwner.CreatePool(soul.Symbol, poolAmount0, gas.Symbol, poolAmount5);
    }
    

    private void CreatePools()
    {
        SetupNormalPool();
    }

    [Fact]
    public void MigrateTest()
    {
        CoreClass core = new CoreClass();
        var poolOwner = new ExchangeUser(soul.Symbol, kcal.Symbol, core);

        
        core.InitFunds();
        core.Migrate();

        // Check pools
        var poolSOULKCAL = poolOwner.GetPool(soul.Symbol, kcal.Symbol);
        var poolSOULETH = poolOwner.GetPool(soul.Symbol, eth.Symbol);
        var poolSOULBNB = poolOwner.GetPool(soul.Symbol, bnb.Symbol);
        var poolSOULNEO = poolOwner.GetPool(soul.Symbol, neo.Symbol);
        var poolSOULGAS = poolOwner.GetPool(soul.Symbol, gas.Symbol);
        
        // TODO: Checks
        Assert.True(poolSOULKCAL.Symbol0 == soul.Symbol, "Symbol0 doesn't check");
        Assert.True(poolSOULKCAL.Symbol1 == kcal.Symbol, "Symbol1 doesn't check");
        Assert.True(poolSOULETH.Symbol0 == soul.Symbol, "Symbol0 doesn't check");
        Assert.True(poolSOULETH.Symbol1 == eth.Symbol, "Symbol1 doesn't check");
        Assert.True(poolSOULBNB.Symbol0 == soul.Symbol, "Symbol0 doesn't check");
        Assert.True(poolSOULBNB.Symbol1 == bnb.Symbol, "Symbol1 doesn't check");
        Assert.True(poolSOULNEO.Symbol0 == soul.Symbol, "Symbol0 doesn't check");
        Assert.True(poolSOULNEO.Symbol1 == neo.Symbol, "Symbol1 doesn't check");
        Assert.True(poolSOULGAS.Symbol0 == soul.Symbol, "Symbol0 doesn't check");
        Assert.True(poolSOULGAS.Symbol1 == gas.Symbol, "Symbol1 doesn't check");
        //Assert.True(poolSOULKCAL.Amount0 == myPoolAmount0, $"Amount0 doesn't check {poolSOULKCAL.Amount0}");
    }


    [Fact]
    
    public void CreatePool()
    {
        Filter.Test(() =>
        {
            CoreClass core = new CoreClass();

            // Setup symbols
            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = DomainSettings.FuelTokenSymbol;
            //GetTokenQuote
            var soulPrice = UnitConversion.ToDecimal(100, DomainSettings.FiatTokenDecimals);
            var coolPrice = UnitConversion.ToDecimal(300, DomainSettings.FiatTokenDecimals);

            decimal ratio = decimal.Round(soulPrice / coolPrice, DomainSettings.MAX_TOKEN_DECIMALS / 2, MidpointRounding.AwayFromZero);

            BigInteger myPoolAmount0 = UnitConversion.ToBigInteger(10000, 8);
            BigInteger myPoolAmount1 = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(myPoolAmount0, 8) / ratio, 8);

            core.InitFunds();
            core.Migrate();

            // Create users
            var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);

            // Create Token
            //CoreClass.ExchangeTokenInfo cool = new CoreClass.ExchangeTokenInfo("COOL", "Phantasma Cool", UnitConversion.ToBigInteger(10000000, 8), 8, flags );
            //poolOwner.CreateToken(cool);
            Assert.True(core.simulator.LastBlockWasSuccessful());

            // Give Users tokens
            poolOwner.FundUser(soul: 50000, kcal: 100);
            poolOwner.Fund(eth.Symbol, poolAmount2);
            poolOwner.Fund(bnb.Symbol, poolAmount3);
            poolOwner.Fund(neo.Symbol, poolAmount4);
            poolOwner.Fund(gas.Symbol, poolAmount5);
            poolOwner.Fund(cool.Symbol, myPoolAmount1 * 2);

            double am0 = (double)myPoolAmount0;
            double am1 = (double)myPoolAmount1;
            BigInteger totalLiquidity = (BigInteger)Math.Sqrt(am0 * am1) * UnitConversion.GetUnitValue(DomainSettings.MAX_TOKEN_DECIMALS);

            // Get Tokens Info
            //token0
            var token0 = core.nexus.GetTokenInfo(core.nexus.RootStorage, soul.Symbol);
            var token0Address = core.nexus.GetTokenContract(core.nexus.RootStorage, soul.Symbol);
            Assert.True(token0.Symbol == soul.Symbol);

            // token1
            var token1 = core.nexus.GetTokenInfo(core.nexus.RootStorage, cool.Symbol);
            var token1Address = core.nexus.GetTokenContract(core.nexus.RootStorage, cool.Symbol);
            Assert.True(token1.Symbol == cool.Symbol);
            Assert.True(token1.Flags.HasFlag(TokenFlags.Transferable), "Not swappable.");

            // Create a Pool
            poolOwner.CreatePool(soul.Symbol, myPoolAmount0, cool.Symbol, 0);
            Assert.True(core.simulator.LastBlockWasSuccessful());

            var pool = poolOwner.GetPool(soul.Symbol, cool.Symbol);

            Assert.True(pool.Symbol0 == soul.Symbol, "Symbol0 doesn't check");
            Assert.True(pool.Amount0 == myPoolAmount0, $"Amount0 doesn't check {pool.Amount0}");
            Assert.True(pool.Symbol1 == cool.Symbol, "Symbol1 doesn't check");
            Assert.True(pool.Amount1 == myPoolAmount1, $"Amount1 doesn't check {pool.Amount1} != {myPoolAmount1}");
            Assert.True(pool.TotalLiquidity == totalLiquidity, "Liquidity doesn't check");
            Assert.True(pool.Symbol0Address == token0Address.Address.Text);
            Assert.True(pool.Symbol1Address == token1Address.Address.Text);

            Console.WriteLine($"Check Values | {pool.Symbol0}({pool.Symbol0Address}) -> {pool.Amount0} | {pool.Symbol1}({pool.Symbol1Address}) -> {pool.Amount1} || {pool.TotalLiquidity}");
        });
    }

    [Fact]
    
    public void CreateVirtualPool()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.InitFunds();
        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 50000, kcal: 1000);
        poolOwner.Fund(eth.Symbol, poolAmount2);
        poolOwner.Fund(bnb.Symbol, poolAmount3);
        poolOwner.Fund(neo.Symbol, poolAmount4);
        poolOwner.Fund(gas.Symbol, poolAmount5);
        poolOwner.Fund(cool.Symbol, UnitConversion.ToBigInteger(10000, cool.Decimals));
        
        // Amounts 
        var amount0 = UnitConversion.ToBigInteger(500, kcal.Decimals);
        var amount1 = UnitConversion.ToBigInteger(5000, cool.Decimals);
        
        var initialAmount0SameDecimals = UnitConversion.ConvertDecimals(amount0, kcal.Decimals, 8);
        var initialAmount1SameDecimals = UnitConversion.ConvertDecimals(amount1, cool.Decimals, 8);
        
        var virtualPool = poolOwner.GetPool(kcal.Symbol, cool.Symbol);
        BigInteger totalLiquidity = (BigInteger)Math.Sqrt((double)(initialAmount0SameDecimals * initialAmount1SameDecimals)) * UnitConversion.GetUnitValue(DomainSettings.MAX_TOKEN_DECIMALS);

        // Get Tokens Info
        //token1
        var token1 = core.nexus.GetTokenInfo(core.nexus.RootStorage, kcal.Symbol);
        var token1Address = core.nexus.GetTokenContract(core.nexus.RootStorage, kcal.Symbol);
        Assert.True(token1.Symbol == kcal.Symbol, "Symbol1 != Token1");

        // virtual Token
        var virtualToken = core.nexus.GetTokenInfo(core.nexus.RootStorage, cool.Symbol);
        var virtualTokenAddress = core.nexus.GetTokenContract(core.nexus.RootStorage, cool.Symbol);
        Assert.True(cool.Symbol == virtualToken.Symbol, $"VirtualSymbol != VirtualToken({virtualToken})");

        poolOwner.CreatePool(kcal.Symbol, amount0, cool.Symbol , amount1);
        Assert.True(core.simulator.LastBlockWasSuccessful());
        
        // Check if the pool was created
        var pool = poolOwner.GetPool(kcal.Symbol, cool.Symbol);

        Assert.True(pool.Symbol0 == kcal.Symbol);
        Assert.True(pool.Amount0 == amount0);
        Assert.True(pool.Symbol1 == cool.Symbol); 
        Assert.True(pool.Amount1 == amount1);
        Assert.True(pool.TotalLiquidity == totalLiquidity);
        Assert.True(pool.Symbol0Address == token1Address.Address.Text);
        Assert.True(pool.Symbol1Address == virtualTokenAddress.Address.Text);

        Console.WriteLine($"Check Values | {pool.Symbol0}({pool.Symbol0Address}) -> {pool.Amount0} | {pool.Symbol1}({pool.Symbol1Address}) -> {pool.Amount1} || {pool.TotalLiquidity}");
    }

    [Fact]
    
    // TODO: Get the pool initial values and calculate the target rate with those values insted of the static ones.
    public void AddLiquidityToPool()
    {
        Filter.Test(() =>
        {
            CoreClass core = new CoreClass();

            // Setup symbols
            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = DomainSettings.FuelTokenSymbol;

            core.InitFunds();
            core.Migrate();

            // Create users
            var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);

            // Give Users tokens
            poolOwner.FundUser(soul: 500000, kcal: 100);
            poolOwner.Fund(eth.Symbol, poolAmount2 * 2);
            poolOwner.Fund(bnb.Symbol, poolAmount3);
            poolOwner.Fund(neo.Symbol, poolAmount4);
            poolOwner.Fund(gas.Symbol, poolAmount5);

            // Get Initial Pool State the Liquidity
            var pool = poolOwner.GetPool(soul.Symbol, eth.Symbol);
            var balance = poolOwner.GetBalance(eth.Symbol);
            Console.WriteLine($"Balance:{UnitConversion.ToDecimal(balance, eth.Decimals)}");
            var amount0 = UnitConversion.ToBigInteger(500, soul.Decimals);
            var amount1 = poolAmount2 / 10;
            var poolRatio =
                UnitConversion.ConvertDecimals(pool.Amount0, soul.Decimals, DomainSettings.FiatTokenDecimals) /
                UnitConversion.ConvertDecimals(pool.Amount1, eth.Decimals, DomainSettings.FiatTokenDecimals);
            var amountCalculated =
                UnitConversion.ConvertDecimals((amount0 / poolRatio), DomainSettings.FiatTokenDecimals, eth.Decimals);

            // Add Liquidity to the pool
            poolOwner.AddLiquidity(soul.Symbol, amount0, eth.Symbol, 0);
            Assert.True(core.simulator.LastBlockWasSuccessful());

            // Check the Liquidity
            var poolAfter = poolOwner.GetPool(soul.Symbol, eth.Symbol);
            pool.TotalLiquidity += (amount0 * pool.TotalLiquidity) / (pool.Amount0);

            Assert.True(poolAfter.Symbol0 == soul.Symbol, $"Symbol is incorrect: {soul.Symbol}");
            Assert.True(poolAfter.Amount0 == pool.Amount0 + amount0,
                $"Symbol Amount0 is incorrect: {pool.Amount0 + amount0} != {poolAfter.Amount0} {soul.Symbol}");
            Assert.True(poolAfter.Symbol1 == eth.Symbol, $"Pair is incorrect: {eth.Symbol}");
            Assert.True(poolAfter.Amount1 == pool.Amount1 + amountCalculated,
                $"Symbol Amount1 is incorrect: {pool.Amount1 + amountCalculated} != {poolAfter.Amount1} {eth.Symbol}");
            Assert.True(pool.TotalLiquidity == poolAfter.TotalLiquidity,
                $"TotalLiquidity doesn't checkout {pool.TotalLiquidity}!={poolAfter.TotalLiquidity}");
        });
    }

    [Fact]
    public void AddLiquidityToVirtualPool()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.InitFunds();
        core.Migrate();

        core.SetupVirtualPools();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var poolOwner2 = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 50000, kcal: 10000);
        poolOwner.Fund(eth.Symbol, poolAmount2);
        poolOwner.Fund(neo.Symbol, poolAmount4);
        poolOwner.Fund(gas.Symbol, poolAmount5);
        poolOwner.Fund(cool.Symbol, UnitConversion.ToBigInteger(20000, cool.Decimals));
        
        poolOwner2.FundUser(soul: 50000, kcal: 10000);
        poolOwner2.Fund(cool.Symbol, UnitConversion.ToBigInteger(20000, cool.Decimals));

        var initialAmount0 = UnitConversion.ToBigInteger(1000, kcal.Decimals);
        var initialAmount1 = UnitConversion.ToBigInteger(5000, cool.Decimals);
        var initialAmount0SameDecimals = UnitConversion.ConvertDecimals(initialAmount0, kcal.Decimals, 8);
        var initialAmount1SameDecimals = UnitConversion.ConvertDecimals(initialAmount1, cool.Decimals, 8);
        var amount0 = UnitConversion.ToBigInteger(100, kcal.Decimals);
        var amount1 = UnitConversion.ToBigInteger(500, cool.Decimals);

        var pool = poolOwner.GetPool(kcal.Symbol, cool.Symbol);
        BigInteger totalLiquidity = ExchangeContract.Sqrt(initialAmount0SameDecimals * initialAmount1SameDecimals) * UnitConversion.GetUnitValue(DomainSettings.MAX_TOKEN_DECIMALS);
        Assert.True(totalLiquidity == pool.TotalLiquidity);

        // Add Liquidity to the pool
        poolOwner.AddLiquidity(kcal.Symbol, amount0, cool.Symbol, amount1);
        Assert.True(core.simulator.LastBlockWasSuccessful());

        // Check the Liquidity
        totalLiquidity += (amount0 * pool.TotalLiquidity) / (pool.Amount0);
        
        var poolAfter = poolOwner.GetPool(kcal.Symbol, cool.Symbol);

        Console.WriteLine($"{poolAfter.Amount1} != {pool.Amount1+amount1}");
        Assert.True(poolAfter.Symbol0 == kcal.Symbol, "Symbol is incorrect");
        Assert.True(poolAfter.Amount0 == pool.Amount0 + amount0, "Symbol Amount0 is incorrect");
        Assert.True(poolAfter.Symbol1 == cool.Symbol, "Pair is incorrect");
        Assert.True(poolAfter.Amount1 == pool.Amount1 + amount1, "Symbol Amount1 is incorrect");
        Assert.True(poolAfter.TotalLiquidity == totalLiquidity, $"{poolAfter.TotalLiquidity} == {totalLiquidity}");
    }

    [Fact]
    public void RemoveLiquidityToPool()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.InitFunds();
        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 50000, kcal: 100);
        poolOwner.Fund(eth.Symbol, poolAmount2);
        poolOwner.Fund(neo.Symbol, poolAmount4);
        poolOwner.Fund(gas.Symbol, poolAmount5);

        // Get Initial Pool State the Liquidity
        var pool = poolOwner.GetPool(soul.Symbol, eth.Symbol);

        BigInteger poolRatio = UnitConversion.ConvertDecimals(pool.Amount0, soul.Decimals, DomainSettings.FiatTokenDecimals) / UnitConversion.ConvertDecimals(pool.Amount1, eth.Decimals, DomainSettings.FiatTokenDecimals);
        var amount0 = UnitConversion.ToBigInteger(5000, soul.Decimals);
        var amount1 = UnitConversion.ConvertDecimals((amount0  / poolRatio ), DomainSettings.FiatTokenDecimals, eth.Decimals);
        Console.WriteLine($"ratio:{poolRatio} | amount0:{amount0} | amount1:{amount1}");
        Console.WriteLine($"BeforeTouchingPool: {pool.Amount0} {pool.Symbol0} | {pool.Amount1} {pool.Symbol1} | PoolRatio:{UnitConversion.ConvertDecimals(pool.Amount0, soul.Decimals, DomainSettings.FiatTokenDecimals) / UnitConversion.ConvertDecimals(pool.Amount1, eth.Decimals, DomainSettings.FiatTokenDecimals)}\n");

        BigInteger sameDecimalsAmount0 =  UnitConversion.ConvertDecimals(pool.Amount0, soul.Decimals, DomainSettings.FiatTokenDecimals);
        BigInteger sameDecimalsAmount1 =  UnitConversion.ConvertDecimals(pool.Amount1, eth.Decimals, DomainSettings.FiatTokenDecimals);

        BigInteger totalAm0 = pool.Amount0;
        BigInteger totalAm1 = pool.Amount1;
        BigInteger totalLiquidity = pool.TotalLiquidity;
        var calculatedTLP = ExchangeContract.Sqrt(sameDecimalsAmount0 * sameDecimalsAmount1) *
                            UnitConversion.GetUnitValue(DomainSettings.MAX_TOKEN_DECIMALS);
        Assert.True(calculatedTLP == totalLiquidity, $"{calculatedTLP} == {totalLiquidity}");
        
        // Add Liquidity to the pool
        poolOwner.AddLiquidity(soul.Symbol, amount0, eth.Symbol, amount1);
        Assert.True(core.simulator.LastBlockWasSuccessful());
        
        var lpAdded = (amount0 * totalLiquidity) / totalAm0;
        totalLiquidity += lpAdded;
        totalAm0 += amount0;
        
        var poolBefore = poolOwner.GetPool(soul.Symbol, eth.Symbol);
        Console.WriteLine($"AfterAdd: {poolBefore.Amount0} {poolBefore.Symbol0} | {poolBefore.Amount1} {poolBefore.Symbol1} | PoolRatio:{UnitConversion.ConvertDecimals(poolBefore.Amount0, soul.Decimals, DomainSettings.FiatTokenDecimals) * 100 / UnitConversion.ConvertDecimals(poolBefore.Amount1, eth.Decimals, DomainSettings.FiatTokenDecimals)}\n");

        var nftRAMBefore = poolOwner.GetPoolRAM(soul.Symbol, eth.Symbol);

        // Remove Liquidity
        var amount0Remove = UnitConversion.ToBigInteger(2000, soul.Decimals);
        var amount1Remove = UnitConversion.ConvertDecimals((amount0Remove  / poolRatio ), DomainSettings.FiatTokenDecimals, eth.Decimals);

        poolOwner.RemoveLiquidity(soul.Symbol, amount0Remove, eth.Symbol, 0);
        Assert.True(core.simulator.LastBlockWasSuccessful());

        // Get Pool
        var poolAfter = poolOwner.GetPool(soul.Symbol, eth.Symbol);

        Console.WriteLine($"AfterRemove: {poolAfter.Amount0} {poolAfter.Symbol0} | {poolAfter.Amount1} {poolAfter.Symbol1} | PoolRatio:{UnitConversion.ConvertDecimals(poolAfter.Amount0, soul.Decimals, DomainSettings.FiatTokenDecimals) * 100 / UnitConversion.ConvertDecimals(poolAfter.Amount1, eth.Decimals, DomainSettings.FiatTokenDecimals)}\n");
        
        BigInteger newLP = ((nftRAMBefore.Amount0 - amount0Remove) * (totalLiquidity - nftRAMBefore.Liquidity)) / (totalAm0 - nftRAMBefore.Amount0);
        var lpRemoved = ((nftRAMBefore.Amount0 - amount0Remove) * (totalLiquidity- nftRAMBefore.Liquidity)) / (totalAm0- nftRAMBefore.Amount0);
        totalLiquidity = totalLiquidity - nftRAMBefore.Liquidity + newLP;
        totalAm0 -= (amount0Remove);

        // Get My NFT DATA 
        var nftRAMAfter = poolOwner.GetPoolRAM(soul.Symbol, eth.Symbol);

        Console.WriteLine($"TEST: BeforeLP:{nftRAMBefore.Liquidity}  | AfterLP:{nftRAMAfter.Liquidity} | LPRemoved:{lpRemoved}");

        // Validation
        Assert.False(nftRAMBefore.Amount0 == nftRAMAfter.Amount0, "Amount0 does not differ.");
        Assert.False(nftRAMBefore.Amount1 == nftRAMAfter.Amount1, "Amount1 does not differ.");
        Assert.False(nftRAMBefore.Liquidity == nftRAMAfter.Liquidity, $"Liquidity does not differ. | {nftRAMBefore.Liquidity} == {nftRAMAfter.Liquidity}");

        Assert.True(nftRAMBefore.Amount0 - amount0Remove == nftRAMAfter.Amount0, "Amount0 not true.");
        Assert.True(nftRAMBefore.Amount1 - amount1Remove == nftRAMAfter.Amount1, $"Amount1 not true. {nftRAMBefore.Amount1 - amount1} != {nftRAMAfter.Amount1}");
        Assert.True(newLP == nftRAMAfter.Liquidity, $"Liquidity does differ. | {newLP} == {nftRAMAfter.Liquidity}");

        // Get Amount by Liquidity
        // Liqudity Formula  Liquidity = (amount0 * pool.TotalLiquidity) / pool.Amount0;
        // Amount Formula  amount = Liquidity  * pool.Amount0 / pool.TotalLiquidity;
        //(amount0 * (pool.TotalLiquidity - nftRAM.Liquidity)) / (pool.Amount0 - nftRAM.Amount0);
        var _amount0 = (nftRAMAfter.Liquidity) * poolAfter.Amount0 / poolAfter.TotalLiquidity;
        var _amount1 = (nftRAMAfter.Liquidity) * poolAfter.Amount1 / poolAfter.TotalLiquidity;
        var _pool_amount0 = (poolBefore.Amount0 - amount0Remove );
        var _pool_amount1 = (poolBefore.Amount1 - amount1Remove );
        var _pool_liquidity = totalLiquidity;
         
        Console.WriteLine($"user Initial = am0:{nftRAMBefore.Amount0} | am1:{nftRAMBefore.Amount1} | lp:{nftRAMBefore.Liquidity}");
        Console.WriteLine($"pool Initial = am0:{poolBefore.Amount0} | am1:{poolBefore.Amount1} | lp:{poolBefore.TotalLiquidity}");
        Console.WriteLine($"user after = am0:{nftRAMAfter.Amount0} | am1:{nftRAMAfter.Amount1} | lp:{nftRAMAfter.Liquidity}");
        Console.WriteLine($"pool after = am0:{poolAfter.Amount0} | am1:{poolAfter.Amount1} | lp:{poolAfter.TotalLiquidity}");
        Console.WriteLine($"am0 = {_amount0} == {nftRAMAfter.Amount0} || am1 = {_amount1} == {nftRAMAfter.Amount1}");
        Assert.True(_pool_amount0 == poolAfter.Amount0, $"Pool Amount0 not calculated properly | {_pool_amount0} != {poolAfter.Amount0}");
        Assert.True(_pool_amount1 == poolAfter.Amount1, $"Pool Amount1 not calculated properly | {_pool_amount1} != {poolAfter.Amount1}");
        Assert.True(_pool_liquidity == poolAfter.TotalLiquidity, $"Pool TotalLiquidity not calculated properly | {_pool_liquidity} != {poolAfter.TotalLiquidity}");
        Assert.True(_amount0 == nftRAMAfter.Amount0, $"Amount0 not calculated properly | {_amount0+ UnitConversion.ToBigInteger(0.00000001m, soul.Decimals) } != {nftRAMAfter.Amount0}");
        Assert.True(_amount1 == nftRAMAfter.Amount1, $"Amount1 not calculated properly | {_amount1+ UnitConversion.ToBigInteger(0.000000001m, eth.Decimals)} != {nftRAMAfter.Amount1}");

        // Get Liquidity by amount
        var liquidityAm0 = nftRAMAfter.Amount0 * totalLiquidity / poolAfter.Amount0;
        var liquidityAm1 = nftRAMAfter.Amount1 * totalLiquidity / poolAfter.Amount1;

        Console.WriteLine($"LiquidityAm0 = {liquidityAm0} == {nftRAMAfter.Liquidity} || LiquidityAm1 = {liquidityAm1} == {nftRAMAfter.Liquidity} | ratio:{nftRAMAfter.Amount0 / nftRAMAfter.Amount1}");
        Console.WriteLine($"LiquidityAm0 = {nftRAMAfter.Amount0} * {totalLiquidity} / {poolAfter.Amount1} = {nftRAMAfter.Amount0 * totalLiquidity / poolAfter.Amount0}");
        Console.WriteLine($"LiquidityAm0 = {nftRAMAfter.Amount0} * {poolAfter.TotalLiquidity} / {poolAfter.Amount1} = {nftRAMAfter.Amount0 * poolAfter.TotalLiquidity / poolAfter.Amount0}");

        Assert.True(liquidityAm0 == nftRAMAfter.Liquidity, "Liquidity Amount0 -> not calculated properly");
        Assert.True(liquidityAm1 == nftRAMAfter.Liquidity, "Liquidity Amount1 -> not calculated properly");
        Assert.True(totalLiquidity == poolAfter.TotalLiquidity, $"Liquidity not true.");
    }

    [Fact]
    public void RemoveLiquiditySmall()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.InitFunds();
        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens

        poolOwner.FundUser(soul: 50000, kcal: 100);
        poolOwner.Fund(eth.Symbol, poolAmount2);
        poolOwner.Fund(neo.Symbol, poolAmount4);
        poolOwner.Fund(gas.Symbol, poolAmount5);
        
        
        // Get Initial Pool State the Liquidity
        var pool = poolOwner.GetPool(soul.Symbol, eth.Symbol);

        BigInteger poolRatio = UnitConversion.ConvertDecimals(pool.Amount0, 8, DomainSettings.FiatTokenDecimals) / UnitConversion.ConvertDecimals(pool.Amount1, 18, DomainSettings.FiatTokenDecimals);
        var amount0 = UnitConversion.ToBigInteger(5000, soul.Decimals);
        var amount1 = UnitConversion.ConvertDecimals((amount0 / poolRatio), DomainSettings.FiatTokenDecimals, 18);

        BigInteger totalAm0 = pool.Amount0;
        BigInteger totalAm1 = pool.Amount1;
        BigInteger totalLiquidity = pool.TotalLiquidity;

        // Add Liquidity to the pool
        poolOwner.AddLiquidity(soul.Symbol, amount0, eth.Symbol, 0);
        Assert.True(core.simulator.LastBlockWasSuccessful());

        var lpAdded = (amount0 * totalLiquidity) / totalAm0;
        totalLiquidity += lpAdded;
        totalAm0 += amount0;

        var poolBefore = poolOwner.GetPool(soul.Symbol, eth.Symbol);

        var nftRAMBefore = poolOwner.GetPoolRAM(soul.Symbol, eth.Symbol);

        var _amount0 = (nftRAMBefore.Liquidity) * poolBefore.Amount0 / poolBefore.TotalLiquidity;
        var _amount1 = (nftRAMBefore.Liquidity) * poolBefore.Amount1 / poolBefore.TotalLiquidity;

        Assert.True(_amount0 + UnitConversion.ToBigInteger(0.00000001m, 8)  >= nftRAMBefore.Amount0, $"Amount0 not calculated properly | {_amount0} != {nftRAMBefore.Amount0}");
        Assert.True(_amount1 + UnitConversion.ToBigInteger(0.00000001m, 10) >= nftRAMBefore.Amount1, $"Amount1 not calculated properly | {_amount1} != {nftRAMBefore.Amount1}");
    }

    [Fact]
    public void RemoveLiquidityToVirtualPool()
    {
        Filter.Test(() =>
        {
            CoreClass core = new CoreClass();

            // Setup symbols
            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = DomainSettings.FuelTokenSymbol;

            core.InitFunds();
            core.Migrate();
            core.SetupVirtualPools();

            // Create users
            var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);

            // Give Users tokens
            poolOwner.FundUser(soul: 50000, kcal: 20000);
            poolOwner.Fund(eth.Symbol, poolAmount2);
            poolOwner.Fund(neo.Symbol, poolAmount4);
            poolOwner.Fund(gas.Symbol, poolAmount5);
            poolOwner.Fund(cool.Symbol, UnitConversion.ToBigInteger(50000, cool.Decimals));

            // Get Initial Pool State the Liquidity
            var pool = poolOwner.GetPool(kcal.Symbol, cool.Symbol);

            decimal poolRatio = UnitConversion.ToDecimal(UnitConversion.ConvertDecimals(pool.Amount0, kcal.Decimals, DomainSettings.FiatTokenDecimals), 8) / UnitConversion.ToDecimal(UnitConversion.ConvertDecimals(pool.Amount1, cool.Decimals, DomainSettings.FiatTokenDecimals), 8);
            var amount0 = UnitConversion.ToBigInteger(1000, kcal.Decimals);
            var amount1 = UnitConversion.ToBigInteger((UnitConversion.ToDecimal(amount0, 8) / poolRatio), cool.Decimals);
            Console.WriteLine($"ratio:{poolRatio} | amount0:{amount0} | amount1:{amount1}");
            Console.WriteLine($"BeforeTouchingPool: {pool.Amount0} {pool.Symbol0} | {pool.Amount1} {pool.Symbol1} | PoolRatio:{UnitConversion.ConvertDecimals(pool.Amount0, kcal.Decimals, DomainSettings.FiatTokenDecimals) / UnitConversion.ConvertDecimals(pool.Amount1, cool.Decimals, DomainSettings.FiatTokenDecimals)}\n");

            BigInteger totalAm0 = pool.Amount0;
            BigInteger totalAm1 = pool.Amount1;
            BigInteger totalAm0SameDecimals = UnitConversion.ConvertDecimals(totalAm0, kcal.Decimals, 8);
            BigInteger totalAm1SameDecimals = UnitConversion.ConvertDecimals(totalAm1, cool.Decimals, 8);
            BigInteger totalLiquidity = (BigInteger)Math.Sqrt((double)(totalAm0SameDecimals * totalAm1SameDecimals)) * UnitConversion.GetUnitValue(DomainSettings.MAX_TOKEN_DECIMALS);
            Assert.True(totalLiquidity == pool.TotalLiquidity);

            // Add Liquidity to the pool
            poolOwner.AddLiquidity(kcal.Symbol, amount0, cool.Symbol, amount1);
            Assert.True(core.simulator.LastBlockWasSuccessful());

            var lpAdded = (amount0 * totalLiquidity) / totalAm0;
            totalLiquidity += lpAdded;
            totalAm0 += amount0;

            var poolBefore = poolOwner.GetPool(kcal.Symbol, cool.Symbol);
            var nftRAMBefore = poolOwner.GetPoolRAM(kcal.Symbol, cool.Symbol);

            // Remove Liquidity
            var amount0Remove = UnitConversion.ToBigInteger(500, kcal.Decimals);
            var amount1Remove = UnitConversion.ToBigInteger((UnitConversion.ToDecimal(UnitConversion.ConvertDecimals(amount0Remove, 10, 8), 8) / poolRatio), cool.Decimals);

            poolOwner.RemoveLiquidity(kcal.Symbol, amount0Remove, cool.Symbol, amount1Remove);
            Assert.True(core.simulator.LastBlockWasSuccessful());

            var poolAfter = poolOwner.GetPool(kcal.Symbol, cool.Symbol);
            var nftRAMAfter = poolOwner.GetPoolRAM(kcal.Symbol, cool.Symbol);

            Console.WriteLine($"AfterRemove: {poolAfter.Amount0} {poolAfter.Symbol0} | {poolAfter.Amount1} {poolAfter.Symbol1} | PoolRatio:{UnitConversion.ConvertDecimals(poolAfter.Amount0, kcal.Decimals, DomainSettings.FiatTokenDecimals) / UnitConversion.ConvertDecimals(poolAfter.Amount1, cool.Decimals, DomainSettings.FiatTokenDecimals)}\n");
            BigInteger newLP = ((nftRAMBefore.Amount0 - amount0Remove) * (totalLiquidity - nftRAMBefore.Liquidity)) / (totalAm0 - nftRAMBefore.Amount0);
            var lpRemoved = ((nftRAMBefore.Amount0 - amount0Remove) * (totalLiquidity - nftRAMBefore.Liquidity)) / (totalAm0 - nftRAMBefore.Amount0);
            totalLiquidity = totalLiquidity - nftRAMBefore.Liquidity + newLP;
            totalAm0 -= (amount0Remove);

            // Get My NFT DATA 

            // Validation
            Assert.False(nftRAMBefore.Amount0 == nftRAMAfter.Amount0, "Amount0 does not differ.");
            Assert.False(nftRAMBefore.Amount1 == nftRAMAfter.Amount1, "Amount1 does not differ.");
            Assert.False(nftRAMBefore.Liquidity == nftRAMAfter.Liquidity, "Liquidity does not differ.");

            Assert.True(nftRAMBefore.Amount0 - amount0Remove == nftRAMAfter.Amount0, "Amount0 not true.");
            Assert.True(nftRAMBefore.Amount1 - amount1Remove == nftRAMAfter.Amount1, "Amount1 not true.");

            // Get Amount by Liquidity
            // Liqudity Formula  Liquidity = (amount0 * pool.TotalLiquidity) / pool.Amount0;
            // Amount Formula  amount = Liquidity  * pool.Amount0 / pool.TotalLiquidity;
            var _amount0 = (nftRAMAfter.Liquidity) * poolAfter.Amount0 / poolAfter.TotalLiquidity;
            var _amount1 = (nftRAMAfter.Liquidity) * poolAfter.Amount1 / poolAfter.TotalLiquidity;

            Console.WriteLine($"am0 = {_amount0} == {nftRAMAfter.Amount0} || am1 = {_amount1} == {nftRAMAfter.Amount1}");
            Assert.True(_amount0 == nftRAMAfter.Amount0, $"Amount0 not calculated properly ({_amount0} != {nftRAMAfter.Amount0})");
            Assert.True(_amount1 == nftRAMAfter.Amount1, "Amount1 not calculated properly");

            // Get Liquidity by amount
            var liquidityAm0 = nftRAMAfter.Amount0 * totalLiquidity / poolAfter.Amount0;
            var liquidityAm1 = nftRAMAfter.Amount1 * totalLiquidity / poolAfter.Amount1;

            Console.WriteLine($"LiquidityAm0 = {liquidityAm0} == {nftRAMAfter.Liquidity} || LiquidityAm1 = {liquidityAm1} == {nftRAMAfter.Liquidity}");

            Assert.True(liquidityAm0 == nftRAMAfter.Liquidity, "Liquidity Amount0 -> not calculated properly");
            Assert.True(liquidityAm1 == nftRAMAfter.Liquidity, "Liquidity Amount1 -> not calculated properly");
            Assert.True(totalLiquidity == poolAfter.TotalLiquidity, "Liquidity not true.");
        });
    }

    [Fact]
    public void TestAddLPSwapRemoveLP()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.InitFunds();
        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var poolOwner2 = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 50000, kcal: 20000);
        poolOwner.Fund(eth.Symbol, poolAmount2);
        poolOwner.Fund(neo.Symbol, poolAmount4);
        poolOwner.Fund(gas.Symbol, poolAmount5);
        poolOwner.Fund(cool.Symbol, UnitConversion.ToBigInteger(50000, cool.Decimals));
        
        poolOwner2.FundUser(soul: 50000, kcal: 20000);
        var balanceBefore = poolOwner2.GetBalance(kcal.Symbol);
        
        var poolBefore = poolOwner.GetPool(soul.Symbol, kcal.Symbol);
        BigInteger poolSameDecimalsAmount0 =  UnitConversion.ConvertDecimals(poolBefore.Amount0, soul.Decimals, DomainSettings.FiatTokenDecimals);
        BigInteger poolSameDecimalsAmount1 =  UnitConversion.ConvertDecimals(poolBefore.Amount1, kcal.Decimals, DomainSettings.FiatTokenDecimals);
        decimal poolRatio = decimal.Round(UnitConversion.ToDecimal(poolSameDecimalsAmount0, 8) / UnitConversion.ToDecimal(poolSameDecimalsAmount1,8), 4);
        BigInteger amount0 = UnitConversion.ToBigInteger(1000, soul.Decimals);
        BigInteger amount1 = UnitConversion.ToBigInteger((UnitConversion.ToDecimal(amount0, 8)/poolRatio), kcal.Decimals);
        BigInteger tradeAmount = UnitConversion.ToBigInteger(100, kcal.Decimals);
        
        poolOwner.AddLiquidity(soul.Symbol, amount0, kcal.Symbol, 0);
        Assert.True(core.simulator.LastBlockWasSuccessful());
        var poolAfter = poolOwner.GetPool(soul.Symbol, kcal.Symbol);
        var nftBefore = poolOwner.GetPoolRAM(soul.Symbol, kcal.Symbol);
        // CALCULATE BASED ON THIS liquidity = (AMOUNTX  * LP_TOTAL )/  AMOUNTX
        var nft_lp = poolBefore.TotalLiquidity * amount0 / poolSameDecimalsAmount0;
        var amount0_lp = nft_lp * poolBefore.Amount0  / (poolBefore.TotalLiquidity) ;
        var amount1_lp = nftBefore.Liquidity * poolBefore.Amount1 / (poolBefore.TotalLiquidity) ;
        Assert.True(nftBefore.Amount0 == amount0, $"{UnitConversion.ToDecimal(nftBefore.Amount0, 8)} == {amount0}");
        Assert.True(nftBefore.Amount1 == amount1, $"{UnitConversion.ToDecimal(nftBefore.Amount1, 10)} == {UnitConversion.ToDecimal(amount1, 10)}");
        Assert.True(nftBefore.Liquidity == nft_lp, $"LP_NFT:{nftBefore.Liquidity} == {nft_lp}");
        Assert.True(nftBefore.Amount0 == amount0_lp, $"Am0:{UnitConversion.ToDecimal(nftBefore.Amount0, 8)} == {UnitConversion.ToDecimal(amount0_lp, 8)} || {nftBefore.Amount0} == {amount0_lp}");
        Assert.True(nftBefore.Amount1 == amount1_lp, $"Am1:{UnitConversion.ToDecimal(nftBefore.Amount1, 10)} == {UnitConversion.ToDecimal(amount1_lp, 10)}");
        
        var rate = poolOwner2.GetRate(kcal.Symbol, soul.Symbol, tradeAmount);
        Assert.True(rate > 0, $"{rate}");
        var fees = poolOwner2.SwapTokens(kcal.Symbol, soul.Symbol, tradeAmount);
        Assert.True(core.simulator.LastBlockWasSuccessful());

        var balance = poolOwner2.GetBalance(kcal.Symbol);
        Assert.True(balance ==  balanceBefore - fees - tradeAmount );

        var unclaimed = poolOwner.GetUnclaimedFees(soul.Symbol, kcal.Symbol);
        Assert.True(unclaimed.Item2 > 0);
        
        
        // FAIL - Try to remove all that you had before
        poolOwner.RemoveLiquidity(soul.Symbol, amount0*2, kcal.Symbol, 0);
        Assert.False(core.simulator.LastBlockWasSuccessful());
        
        var nftAfter = poolOwner.GetPoolRAM(soul.Symbol, kcal.Symbol);
    }

    
    /*
     [Fact]
    // TODO: Get the pool initial values and calculate the target rate with those values insted of the static ones.
    public void GetRatesForSwap()
    {
        

        var scriptPool = new ScriptBuilder().CallContract("swap", "GetPool", soul.Symbol, eth.Symbol).EndScript();
        var resultPool = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptPool);
        var pool = poolOwner.GetPool(soul.Symbol, eth.Symbol);

        BigInteger amount = UnitConversion.ToBigInteger(5, 8);
        BigInteger targetRate = (pool.Amount1 * (1 - 3 / 100) * amount / (pool.Amount0 + (1 - 3 / 100) * amount));

        var script = new ScriptBuilder().CallContract("swap", "GetRates", soul.Symbol, amount).EndScript();

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

        Assert.True(rate == UnitConversion.ToDecimal(targetRate, DomainSettings.FuelTokenDecimals), $"{rate} != {targetRate}");
    }*/

    [Fact]
    // TODO: Get the pool initial values and calculate the target rate with those values insted of the static ones.
    public void GetRateForSwap()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.InitFunds();
        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 500, kcal: 100);
        poolOwner.Fund(eth.Symbol, poolAmount2);
        poolOwner.Fund(neo.Symbol, poolAmount4);
        poolOwner.Fund(gas.Symbol, poolAmount5);
        
        var pool = poolOwner.GetPool(soul.Symbol, eth.Symbol);

        BigInteger amount = UnitConversion.ToBigInteger(5, 8);
        BigInteger targetRate = pool.Amount1 * (1 - 3 / 100) * amount / (pool.Amount0 + (1 - 3 / 100) * amount);
        
        var rate = poolOwner.GetRate(soul.Symbol, eth.Symbol, amount);

        Assert.True(targetRate == rate);
    }

    [Fact]
    public void SwapTokens()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.InitFunds();
        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var poolOwner2 = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 5000, kcal: 100);
        poolOwner.Fund(eth.Symbol, poolAmount2);
        poolOwner.Fund(neo.Symbol, poolAmount4);
        poolOwner.Fund(gas.Symbol, poolAmount5);

        poolOwner2.FundUser(soul: 5000, kcal: 100);

        BigInteger swapValue = UnitConversion.ToBigInteger(100, 8);
        
        var beforeTXBalanceKCAL = poolOwner2.GetBalance(kcal.Symbol);

        // Get Rate
        var rate = poolOwner2.GetRate(soul.Symbol, eth.Symbol, swapValue);

        Console.WriteLine($"{swapValue} {soul.Symbol} for {rate} {eth.Symbol}");

        // Make Swap SOUL / ETH
        var txFees = poolOwner2.SwapTokens(soul.Symbol, eth.Symbol, swapValue);
        Assert.True(core.simulator.LastBlockWasSuccessful());
        var afterTXBalanceKCAL = poolOwner2.GetBalance(kcal.Symbol);
        var kcalfee = beforeTXBalanceKCAL - afterTXBalanceKCAL;
        Console.WriteLine($"KCAL Fee: {kcalfee}");

        // Check trade
        var originalBalance = poolOwner2.GetBalance(eth.Symbol);
        Assert.True(rate == originalBalance, $"{rate} != {originalBalance}");

        // Make Swap SOUL / KCAL
        rate = poolOwner2.GetRate(soul.Symbol, kcal.Symbol, swapValue);

        Console.WriteLine($"{swapValue} {soul.Symbol} for {rate} {kcal.Symbol}");
        
        txFees += poolOwner2.SwapTokens(soul.Symbol, kcal.Symbol, swapValue);
        Assert.True(core.simulator.LastBlockWasSuccessful());

        originalBalance = poolOwner2.GetBalance(kcal.Symbol);

        Assert.True(rate == originalBalance-afterTXBalanceKCAL+kcalfee, $"{rate} != {originalBalance-afterTXBalanceKCAL+kcalfee}");
    }

    [Fact]
    public void SwapTokensReverse()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.InitFunds();
        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var poolOwner2 = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 50000, kcal: 20000);
        poolOwner.Fund(eth.Symbol, poolAmount2);
        poolOwner.Fund(neo.Symbol, poolAmount4);
        poolOwner.Fund(gas.Symbol, poolAmount5);
        
        poolOwner2.FundUser(soul: 50000, kcal: 1200);
        poolOwner2.Fund(eth.Symbol, poolAmount2);

        
        BigInteger swapValueKCAL = UnitConversion.ToBigInteger(1000, kcal.Decimals);
        BigInteger swapValueETH = UnitConversion.ToBigInteger(1, eth.Decimals);
        BigInteger amount0 = UnitConversion.ToBigInteger(1000, soul.Decimals);

        var beforeTXBalanceSOUL = poolOwner2.GetBalance(soul.Symbol);
        var beforeTXBalanceKCAL = poolOwner2.GetBalance(kcal.Symbol);
        var beforeTXBalanceETH = poolOwner2.GetBalance(eth.Symbol);

        // Add Liquidity to the pool SOUL / KCAL
        poolOwner.AddLiquidity(soul.Symbol, amount0, kcal.Symbol, 0);
        Assert.True(core.simulator.LastBlockWasSuccessful());

        // SOUL / ETH

        // Get Rate
        var rate = poolOwner2.GetRate(eth.Symbol, soul.Symbol, swapValueETH);

        Console.WriteLine($"{UnitConversion.ToDecimal(swapValueETH, eth.Decimals)} {eth.Symbol} for {UnitConversion.ToDecimal(rate, soul.Decimals)} {soul.Symbol}");
        // Make Swap SOUL / ETH
        poolOwner2.SwapTokens(eth.Symbol, soul.Symbol, swapValueETH);
        Assert.True(core.simulator.LastBlockWasSuccessful());
        
        var afterTXBalanceKCAL = poolOwner2.GetBalance(kcal.Symbol);
        var kcalfee = beforeTXBalanceKCAL - afterTXBalanceKCAL;
        Console.WriteLine($"KCAL Fee: {UnitConversion.ToDecimal(kcalfee, kcal.Decimals)}");

        // Check trade
        var afterTXBalanceSOUL = poolOwner2.GetBalance(soul.Symbol);
        var afterTXBalanceETH = poolOwner2.GetBalance(eth.Symbol);

        Assert.True(beforeTXBalanceSOUL + rate == afterTXBalanceSOUL, $"{beforeTXBalanceSOUL+rate} != {afterTXBalanceSOUL}");
        Assert.True(beforeTXBalanceETH - swapValueETH == afterTXBalanceETH, $"{beforeTXBalanceETH - swapValueETH} != {afterTXBalanceETH}");

        // Make Swap SOUL / KCAL
        rate = poolOwner2.GetRate(kcal.Symbol, soul.Symbol, swapValueKCAL);

        Console.WriteLine($"{UnitConversion.ToDecimal(swapValueKCAL, kcal.Decimals)} {kcal.Symbol} for {UnitConversion.ToDecimal(rate, soul.Decimals)} {soul.Symbol}");

        poolOwner2.SwapTokens(kcal.Symbol, soul.Symbol, swapValueKCAL);
        Assert.True(core.simulator.LastBlockWasSuccessful());

        var afterTXBalanceSOULEND = poolOwner2.GetBalance(soul.Symbol);
        var afterTXBalanceKCALEND = poolOwner2.GetBalance(kcal.Symbol);

        Assert.True(afterTXBalanceSOUL + rate == afterTXBalanceSOULEND, $"{rate} != {afterTXBalanceSOULEND}");
        Assert.True(afterTXBalanceKCALEND == afterTXBalanceKCAL - kcalfee - swapValueKCAL, $"{afterTXBalanceKCALEND} != {afterTXBalanceKCAL - kcalfee - swapValueKCAL}");
    }


    [Fact]
    public void SwapVirtual()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.InitFunds();
        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var poolOwner2 = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 50000, kcal: 100);
        poolOwner.Fund(eth.Symbol, poolAmount2);
        poolOwner.Fund(neo.Symbol, poolAmount4);
        poolOwner.Fund(gas.Symbol, poolAmount5);
        
        poolOwner2.FundUser(soul: 50000, kcal: 1200);
        
        BigInteger swapValueKCAL = UnitConversion.ToBigInteger(1000, kcal.Decimals);
        BigInteger swapValueETH = UnitConversion.ToBigInteger(1, eth.Decimals);

        var beforeTXBalanceKCAL = poolOwner2.GetBalance( kcal.Symbol);
        var beforeTXBalanceETH = poolOwner2.GetBalance( eth.Symbol );

        // Get Rate
        var rate = poolOwner2.GetRate(kcal.Symbol, eth.Symbol, swapValueKCAL);

        Console.WriteLine($"{UnitConversion.ToDecimal(swapValueKCAL, kcal.Decimals)} {eth.Symbol} for {UnitConversion.ToDecimal(rate, eth.Decimals)} {eth.Symbol}");
        // Make Swap KCAL / ETH
        poolOwner2.SwapTokens(kcal.Symbol, eth.Symbol, swapValueKCAL);
        Assert.True(core.simulator.LastBlockWasSuccessful());
        
        var afterTXBalanceKCAL =  poolOwner2.GetBalance(kcal.Symbol);
        var afterTXBalanceETH =  poolOwner2.GetBalance(eth.Symbol);
        var kcalfee = beforeTXBalanceKCAL - afterTXBalanceKCAL - swapValueKCAL;
        
        Assert.True(afterTXBalanceETH == beforeTXBalanceETH+rate, $"{afterTXBalanceETH} != {beforeTXBalanceETH+rate}");
        Assert.True(beforeTXBalanceKCAL - kcalfee - swapValueKCAL == afterTXBalanceKCAL, $"{beforeTXBalanceKCAL - kcalfee - swapValueKCAL} != {afterTXBalanceKCAL}");
    }

    [Fact]
    public void SwapFee()
    {
        Filter.Test(() =>
        {
            CoreClass core = new CoreClass();

            // Setup symbols
            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = DomainSettings.FuelTokenSymbol;

            core.InitFunds();
            core.Migrate();

            // Create users
            var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
            var poolOwner2 = new ExchangeUser(baseSymbol, quoteSymbol, core);

            // Give Users tokens
            poolOwner.FundUser(soul: 50000, kcal: 100);
            poolOwner.Fund(eth.Symbol, poolAmount2);
            poolOwner.Fund(neo.Symbol, poolAmount4);
            poolOwner.Fund(gas.Symbol, poolAmount5);

            poolOwner2.FundUser(soul: 50000, kcal: 0.1m);
            poolOwner2.Fund(eth.Symbol, poolAmount2);

            var initialKcal = 1000000;

            BigInteger swapValueSOUL = UnitConversion.ToBigInteger(1, soul.Decimals);
            BigInteger swapValueKCAL = UnitConversion.ToBigInteger(1, kcal.Decimals);
            BigInteger swapFee =
                UnitConversion.ConvertDecimals(swapValueSOUL, soul.Decimals, DomainSettings.FiatTokenDecimals);

            // Get Balances before starting trades
            var beforeTXBalanceSOUL = poolOwner2.GetBalance(soul.Symbol);
            var beforeTXBalanceKCAL = poolOwner2.GetBalance(kcal.Symbol);
            var kcalToSwap = swapValueSOUL;
            kcalToSwap -= UnitConversion.ConvertDecimals(beforeTXBalanceKCAL, DomainSettings.FuelTokenDecimals,
                DomainSettings.FiatTokenDecimals);

            // Kcal to trade for
            Console.WriteLine(
                $"Soul amount: {kcalToSwap} | {beforeTXBalanceKCAL} | {swapValueSOUL} - {UnitConversion.ConvertDecimals(beforeTXBalanceKCAL, DomainSettings.FuelTokenDecimals, DomainSettings.FiatTokenDecimals)} = {kcalToSwap}");

            // Get Pool
            var pool = poolOwner.GetPool(eth.Symbol, soul.Symbol);

            var rateByPool = pool.Amount0 * (1 - 97 / 100) * kcalToSwap / (pool.Amount1 + (1 - 97 / 100) * kcalToSwap);
            rateByPool = UnitConversion.ConvertDecimals(rateByPool, kcal.Decimals, 8);

            // Get Rate
            var rate = poolOwner2.GetRate(soul.Symbol, kcal.Symbol, kcalToSwap);

            Console.WriteLine(
                $"{UnitConversion.ToDecimal(kcalToSwap, DomainSettings.FiatTokenDecimals)} {soul.Symbol} for {UnitConversion.ToDecimal(rate, kcal.Decimals)} {kcal.Symbol} | Swap ->  {UnitConversion.ToDecimal(rateByPool, kcal.Decimals)}");

            // Make Swap SOUL / KCAL (SwapFee)
            poolOwner2.SwapFee(soul.Symbol, swapValueSOUL);
            Assert.True(core.simulator.LastBlockWasSuccessful());

            // Get the balances
            var afterTXBalanceSOUL = poolOwner2.GetBalance(soul.Symbol);
            var afterTXBalanceKCAL = poolOwner2.GetBalance(kcal.Symbol);

            var kcalfee = afterTXBalanceKCAL - beforeTXBalanceKCAL - rate;
            Console.WriteLine($"Fee:{UnitConversion.ToDecimal(kcalfee, kcal.Decimals)}");

            Console.WriteLine($"{beforeTXBalanceSOUL} != {afterTXBalanceSOUL} | {afterTXBalanceKCAL}");

            Assert.True(
                afterTXBalanceSOUL ==
                beforeTXBalanceSOUL -
                (kcalToSwap) /* + UnitConversion.ConvertDecimals(500,  kcal.Decimals, DomainSettings.FiatTokenDecimals))*/,
                $"SOUL {afterTXBalanceSOUL} != {beforeTXBalanceSOUL - (kcalToSwap + UnitConversion.ConvertDecimals(500, kcal.Decimals, DomainSettings.FiatTokenDecimals))}");
            Assert.True(beforeTXBalanceKCAL + kcalfee + rate == afterTXBalanceKCAL,
                $"KCAL {beforeTXBalanceKCAL + kcalfee + rate} != {afterTXBalanceKCAL}");
        });
    }

    [Fact]
    public void GetUnclaimed()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.InitFunds();
        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var poolOwner2 = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 55000, kcal: 16000);
        poolOwner.Fund(eth.Symbol, poolAmount2);
        poolOwner.Fund(neo.Symbol, poolAmount4);
        poolOwner.Fund(gas.Symbol, poolAmount5);
        
        poolOwner2.FundUser(soul: 50000, kcal: 10000);


        BigInteger swapValue = UnitConversion.ToBigInteger(10, soul.Decimals);

        // Add Liquidity to the pool
        poolOwner.AddLiquidity(soul.Symbol, UnitConversion.ToBigInteger(1000, soul.Decimals), kcal.Symbol, 0);
        Assert.True(core.simulator.LastBlockWasSuccessful());

        // Get Rate
        var unclaimed = poolOwner.GetUnclaimedFees(soul.Symbol, kcal.Symbol);
        Assert.True(unclaimed.Item1 == 0, "Unclaimed Failed");
        
        // Make a swap and check the fees
        poolOwner2.SwapTokens(soul.Symbol, kcal.Symbol, swapValue);
        Assert.True(core.simulator.LastBlockWasSuccessful());

        var unclaimedFees = poolOwner.GetUnclaimedFees(soul.Symbol, kcal.Symbol);

        Assert.True(unclaimedFees.Item1 > 0, $"{unclaimedFees} > 0");
        // TODO: Add more tests (Swap on the pool and check the fees)
    }


    [Fact]
    public void GetFees()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.InitFunds();
        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var poolOwner2 = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 50000, kcal: 100);
        poolOwner.Fund(eth.Symbol, poolAmount2);
        poolOwner.Fund(neo.Symbol, poolAmount4);
        poolOwner.Fund(gas.Symbol, poolAmount5);
        
        poolOwner2.FundUser(soul: 50000, kcal: 100);
        poolOwner2.Fund(eth.Symbol, poolAmount2);

        
        var addLPAmount = UnitConversion.ToBigInteger(100, soul.Decimals);
        var swapValueSOUL = UnitConversion.ToBigInteger(10, soul.Decimals);

        var balance = poolOwner.GetBalance(eth.Symbol);

        // Add Liquidity to the pool
        poolOwner.AddLiquidity(soul.Symbol, addLPAmount, eth.Symbol,  0);
        Assert.True(core.simulator.LastBlockWasSuccessful());
        
        // Get Pool
        var pool = poolOwner.GetPool(soul.Symbol, eth.Symbol);

        // Get RAM
        var nftRAMBefore = poolOwner.GetPoolRAM(soul.Symbol, eth.Symbol);

        // Make a Swap
        var rate = poolOwner.GetRate(soul.Symbol, eth.Symbol, swapValueSOUL);

        Console.WriteLine($"{UnitConversion.ToDecimal(swapValueSOUL, soul.Decimals)} {soul.Symbol} for {UnitConversion.ToDecimal(rate, eth.Decimals)} {eth.Symbol}");
        
        // Make Swap SOUL / ETH
        poolOwner2.SwapTokens(soul.Symbol, eth.Symbol, swapValueSOUL);
        Assert.True(core.simulator.LastBlockWasSuccessful());
        
        // Get Rate
        var unclaimed = poolOwner.GetUnclaimedFees(soul.Symbol, eth.Symbol);
        BigInteger UserPercent = 75;
        BigInteger GovernancePercent = 25;

        BigInteger totalFees = rate * 3 /100;
        BigInteger feeForUsers = totalFees * 100 / UserPercent;
        BigInteger feeForOwner = totalFees * 100 / GovernancePercent;
        BigInteger feeAmount = nftRAMBefore.Liquidity * UnitConversion.GetUnitValue(DomainSettings.MAX_TOKEN_DECIMALS) / pool.TotalLiquidity;
        var calculatedFees = feeForUsers * feeAmount / UnitConversion.GetUnitValue(DomainSettings.MAX_TOKEN_DECIMALS);

        Assert.True(unclaimed.Item1 == calculatedFees, $"Unclaimed Failed | {unclaimed.Item1} != {calculatedFees}");
    }

    [Fact]
    public void GetClaimFees()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.InitFunds();
        core.Migrate();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var poolOwner2 = new ExchangeUser(baseSymbol, quoteSymbol, core);
        
        // Give Users tokens
        poolOwner.FundUser(soul: 50000, kcal: 100000);
        poolOwner.Fund(eth.Symbol, poolAmount2);
        poolOwner.Fund(neo.Symbol, poolAmount4);
        poolOwner.Fund(gas.Symbol, poolAmount5);

        poolOwner2.FundUser(soul: 50000, kcal: 100);
        poolOwner2.Fund(eth.Symbol, poolAmount2);
        
        var swapValueSOUL = UnitConversion.ToBigInteger(10, soul.Decimals);

        // Add Liquidity to the pool
        poolOwner.AddLiquidity(soul.Symbol, UnitConversion.ToBigInteger(10000, soul.Decimals), kcal.Symbol, 0);
        Assert.True(core.simulator.LastBlockWasSuccessful());

        // Get Pool
        var pool = poolOwner.GetPool(soul.Symbol, kcal.Symbol);

        // Get RAM
        var nftRAMBefore = poolOwner.GetPoolRAM(soul.Symbol, kcal.Symbol);

        // Make a Swap
        var rate = poolOwner2.GetRate(soul.Symbol, kcal.Symbol, swapValueSOUL);
        Console.WriteLine($"{UnitConversion.ToDecimal(swapValueSOUL, 8)} {soul.Symbol} for {UnitConversion.ToDecimal(rate, kcal.Decimals)} {kcal.Symbol}");
        
        // Make Swap SOUL / ETH
        poolOwner2.SwapTokens(soul.Symbol, kcal.Symbol, swapValueSOUL);
        Assert.True(core.simulator.LastBlockWasSuccessful());
        
        // Get Unclaimed Fees
        var unclaimed = poolOwner.GetUnclaimedFees(soul.Symbol, kcal.Symbol);
        BigInteger UserPercent = 75;
        BigInteger GovernancePercent = 25;

        BigInteger totalFees = rate * 3 /100;
        BigInteger feeForUsers = totalFees * 100 / UserPercent;
        BigInteger feeForOwner = totalFees * 100 / GovernancePercent;
        BigInteger feeAmount = nftRAMBefore.Liquidity * UnitConversion.GetUnitValue(DomainSettings.MAX_TOKEN_DECIMALS) / pool.TotalLiquidity;
        var calculatedFees = feeForUsers * feeAmount / UnitConversion.GetUnitValue(DomainSettings.MAX_TOKEN_DECIMALS);

        Console.WriteLine($"{calculatedFees} {kcal.Symbol}");
        Assert.True(unclaimed.Item1 == calculatedFees, $"Unclaimed Failed | {unclaimed.Item1} != {calculatedFees}");
        Assert.True(unclaimed.Item2 == 0, $"Unclaimed Failed | {unclaimed.Item2} != {0}");

        // Claim Fees
        // Get User Balance Before Claiming Fees
        var beforeTXBalanceSOUL = poolOwner.GetBalance( soul.Symbol );
        var beforeTXBalanceKCAL = poolOwner.GetBalance( kcal.Symbol );

        poolOwner.ClaimFees(soul.Symbol, kcal.Symbol);
        Assert.True(core.simulator.LastBlockWasSuccessful());

        // Get User Balance After Claiming Fees
        var afterTXBalanceSOUL = poolOwner.GetBalance(soul.Symbol);
        var afterTXBalanceKCAL = poolOwner.GetBalance(kcal.Symbol);
        
        var unclaimedAfter = poolOwner.GetUnclaimedFees(soul.Symbol, kcal.Symbol);

        Assert.True(afterTXBalanceSOUL == beforeTXBalanceSOUL+calculatedFees, $"Soul Claimed Failed | {afterTXBalanceSOUL} != {beforeTXBalanceSOUL}");
        Assert.True(beforeTXBalanceKCAL != afterTXBalanceKCAL, $"Kcal for TX Failed | {beforeTXBalanceKCAL} != {afterTXBalanceKCAL}");
        Assert.True(unclaimedAfter.Item1 == 0, $"Kcal for TX Failed | {unclaimedAfter} != {0}");
        Assert.True(unclaimedAfter.Item2 == 0, $"Kcal for TX Failed | {unclaimedAfter} != {0}");
    }

    [Fact]
    public void CosmicSwap()
    {
        Filter.Test(() =>
        {
            CoreClass core = new CoreClass();

            // Setup symbols
            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = DomainSettings.FuelTokenSymbol;

            core.InitFunds();
            core.Migrate();

            // Create users
            var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);

            // Give Users tokens
            poolOwner.FundUser(soul: 500, 0);

            var originalBalance = poolOwner.GetBalance(DomainSettings.FuelTokenSymbol);

            var swapAmount = UnitConversion.ToBigInteger(15, DomainSettings.StakingTokenDecimals);

            var kcalRate = core.simulator.InvokeContract(NativeContractKind.Exchange, nameof(ExchangeContract.GetRate),
                baseSymbol, quoteSymbol, swapAmount).AsNumber();

            core.simulator.BeginBlock();
            var tx = core.simulator.GenerateCustomTransaction(poolOwner.userKeys, ProofOfWork.Minimal, () =>
                ScriptUtils.BeginScript()
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.SwapFee),
                        poolOwner.userKeys.Address, baseSymbol, swapAmount)
                    .AllowGas(poolOwner.userKeys.Address, Address.Null, core.simulator.MinimumFee, 999)
                    .SpendGas(poolOwner.userKeys.Address)
                    .EndScript()
            );
            //core.simulator.GenerateSwapFee(poolOwner.userKeys, core.nexus.RootChain, DomainSettings.StakingTokenSymbol, swapAmount);
            core.simulator.EndBlock();
            Assert.True(core.simulator.LastBlockWasSuccessful());
            var txCost = core.simulator.Nexus.RootChain.GetTransactionFee(tx);

            var finalBalance = poolOwner.GetBalance(DomainSettings.FuelTokenSymbol);
            Assert.True(finalBalance >= originalBalance + kcalRate - txCost,
                $"{finalBalance} > {originalBalance + kcalRate - txCost}");
        });
    }
    /*

    [Fact]
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
        Assert.True(balance == 0);

        balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, swapToken, testUser.Address);
        Assert.True(balance > 0);

        var settleHash = (Hash)nexus.RootChain.InvokeContract(nexus.RootStorage, "interop", nameof(InteropContract.GetSettlement), "neo", neoTxHash).ToObject();
        Assert.True(settleHash == tx.Hash);

        var fuelToken = nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
        var leftoverBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, transcodedAddress);
        //Assert.True(leftoverBalance == 0);
    }

    [Fact]
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
        simulator.MintTokens(owner, testUser.Address, eth.Symbol, poolAmount1);
        simulator.MintTokens(owner, potAddress, eth.Symbol, poolAmount1);
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

        Assert.True(currentBalance < oldBalance);
        Assert.True(currentBalance == 0);

        Assert.True(currentSupply < oldSupply);
    }

    [Fact]
    public void QuoteConversions()
    {
        

        Assert.True(nexus.PlatformExists(nexus.RootStorage, "neo"));
        Assert.True(nexus.TokenExists(nexus.RootStorage, "NEO"));

        var context = new StorageChangeSetContext(nexus.RootStorage);
        var runtime = new RuntimeVM(-1, new byte[0], 0, nexus.RootChain, Address.Null, Timestamp.Now, null, context, new OracleSimulator(nexus), ChainTask.Null, true);

        var temp = runtime.GetTokenQuote("NEO", "KCAL", 1);
        var price = UnitConversion.ToDecimal(temp, DomainSettings.FuelTokenDecimals);
        Assert.True(price == 100);

        temp = runtime.GetTokenQuote("KCAL", "NEO", UnitConversion.ToBigInteger(100, DomainSettings.FuelTokenDecimals));
        price = UnitConversion.ToDecimal(temp, 0);
        Assert.True(price == 1);

        temp = runtime.GetTokenQuote("SOUL", "KCAL", UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals));
        price = UnitConversion.ToDecimal(temp, DomainSettings.FuelTokenDecimals);
        Assert.True(price == 5);
    }*/


    [Fact]
    public void TXShouldNotCrash()
    {
        CoreClass core = new CoreClass();
        
        // Setup symbols
        var baseSymbol = DomainSettings.StakingTokenSymbol;
        var quoteSymbol = DomainSettings.FuelTokenSymbol;

        core.InitFunds();
        
        // Create users
        var poolOwner = new ExchangeUser(baseSymbol, quoteSymbol, core);
        var user = PhantasmaKeys.Generate();
        
        // Give Users tokens
        core.simulator.BeginBlock();
        core.simulator.GenerateTransfer(core.owner, user.Address, core.simulator.Nexus.RootChain, neo.Symbol, UnitConversion.ToBigInteger(100m, neo.Decimals));
        core.simulator.GenerateTransfer(core.owner, SmartContract.GetAddressForNative(NativeContractKind.Swap), core.simulator.Nexus.RootChain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(100m, kcal.Decimals));
        core.simulator.EndBlock();
        Assert.True(core.simulator.LastBlockWasSuccessful());
        
        
        
        // SwapFee
        core.simulator.BeginBlock();
        core.simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
             ScriptUtils.BeginScript()
                 .CallContract(NativeContractKind.Swap, nameof(SwapContract.SwapFee), user.Address, neo.Symbol, UnitConversion.ToBigInteger(0.5m, neo.Decimals))
                 .AllowGas(user.Address, Address.Null, 1, 9999)
                .SpendGas(user.Address)
                .EndScript()
        );
        core.simulator.EndBlock();
        Assert.False(core.simulator.LastBlockWasSuccessful());

    }
    

    #endregion

    #region AuxFunctions

    private static int GetDecimals(string symbol)
    {
        switch (symbol)
        {
            case "SOUL": return 8;
            case "KCAL": return 10;
            case "MADT": return 18;
            case "MIDT": return 1;
            case "NDT": return 0;
            default: throw new System.Exception("Unknown decimals for " + symbol);
        }
    }
    
    public class CoreClass
    {
        public struct ExchangeTokenInfo
        {
            public string Symbol;
            public string Name;
            public BigInteger MaxSupply;
            public int Decimals;
            public TokenFlags Flags;
            public ExchangeTokenInfo(string symbol, string name, BigInteger maxSupply, int decimals,
                TokenFlags flags)
            {
                this.Symbol = symbol;
                this.Name = name;
                this.MaxSupply = maxSupply;
                this.Decimals = decimals;
                this.Flags = flags;
            }
        }
        
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
                InitFunds();
        }

        private void InitExchange()
        {
            owner = PhantasmaKeys.Generate();
            var owner1 = PhantasmaKeys.Generate();
            var owner2 = PhantasmaKeys.Generate();
            var owner3 = PhantasmaKeys.Generate();
            simulator = new NexusSimulator(new []{owner, owner1, owner2, owner3});
            nexus = simulator.Nexus;
            simulator.GetFundsInTheFuture(owner);

            
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, ExchangeAddress, nexus.RootChain, soul.Symbol, UnitConversion.ToBigInteger(50, soul.Decimals));
            /*simulator.GenerateCustomTransaction(owner, ProofOfWork.Minimal,
                () => ScriptUtils.BeginScript()
                    .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), ExchangeAddress,  UnitConversion.ToBigInteger(50, soul.Decimals))
                    .SpendGas(owner.Address)
                    .EndScript());*/
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());
            
            var balanceBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, owner.Address);
            simulator.GetFundsInTheFuture(owner);
            simulator.GetFundsInTheFuture(owner);
            //simulator.TransferOwnerAssetsToAddress(owner.Address);
            var balanceAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, owner.Address);


            CreateTokens();
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
                        supply = UnitConversion.ToBigInteger(100000000, decimals);
                        flags = TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite | TokenFlags.Divisible;
                        break;

                    case nonDivisibleTokenSymbol:
                        decimals = 0;
                        supply = UnitConversion.ToBigInteger(100000000, decimals);
                        flags = TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite;
                        break;
                }

                simulator.GenerateToken(owner, symbol, $"{symbol}Token", supply, decimals, flags);
                simulator.MintTokens(owner, owner.Address, symbol, supply);
            }

            simulator.EndBlock();
        }
        
        public void InitFunds()
        {
            var tokens = new[] { eth, bnb, neo, gas, cool };
            simulator.BeginBlock();
            foreach (var token in tokens)
            {
                simulator.GenerateToken(owner, token.Symbol, token.Name, token.MaxSupply, token.Decimals, flags);
                simulator.MintTokens(owner, owner.Address, token.Symbol, token.MaxSupply);
            }
            simulator.EndBlock();
            
            simulator.GetFundsInTheFuture(owner);
            
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, ExchangeAddress, nexus.RootChain, soul.Symbol, poolAmount0*2);
            simulator.GenerateTransfer(owner, ExchangeAddress, nexus.RootChain, kcal.Symbol, poolAmount1*2);
            simulator.GenerateTransfer(owner, ExchangeAddress, nexus.RootChain, eth.Symbol, poolAmount2);
            simulator.GenerateTransfer(owner, ExchangeAddress, nexus.RootChain, bnb.Symbol, poolAmount3);
            simulator.GenerateTransfer(owner, ExchangeAddress, nexus.RootChain, neo.Symbol, poolAmount4);
            simulator.GenerateTransfer(owner, ExchangeAddress, nexus.RootChain, gas.Symbol, poolAmount5);
            
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());
            //Migrate();
        }
        
        public void DeployLPToken()
        {
            var lpContractPVM = Base16.Decode(LP_ContractCompiled_PVM);
            var lpContractABI = Base16.Decode(LP_ContractCompiled_ABI);
            
            var contractName = "LP";
            Address LPAddress = SmartContract.GetAddressFromContractName(contractName);
            
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.Minimal,
                () => ScriptUtils.BeginScript()
                    .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, 10000000)
                    .CallInterop("Nexus.CreateToken", owner.Address, lpContractPVM, lpContractABI)
                    .SpendGas(owner.Address)
                    .EndScript());
            simulator.GenerateTransfer(owner, LPAddress, nexus.RootChain, soul.Symbol, UnitConversion.ToBigInteger(50, soul.Decimals));
            simulator.GenerateCustomTransaction(owner, ProofOfWork.Minimal,
                () => ScriptUtils.BeginScript()
                    .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), LPAddress,  UnitConversion.ToBigInteger(50, soul.Decimals))
                    .SpendGas(owner.Address)
                    .EndScript());
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful(), "Deploying LP Contract Failed");
            
            return;
            /*var PathToFile = Path.GetFullPath("./../../../../Phantasma.Business/src/Blockchain/Contracts/");
            var filePath = PathToFile + "LP";
            
            // read the contracts script
            var contractScript = File.ReadAllBytes(filePath+".pvm");

            // read the contracts abi
            var abiBytes = File.ReadAllBytes(filePath+".abi");*/
        }
        
        public void Migrate()
        {
            //DeployLPToken();
            // Migrate Call Old Way
            Address LPAddress = SmartContract.GetAddressFromContractName(DomainSettings.LiquidityTokenSymbol);
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.Minimal,
                () => ScriptUtils.BeginScript()
                    .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                    .CallContract("LP", "SendFundsAndStake", owner.Address, UnitConversion.ToBigInteger(50, DomainSettings.StakingTokenDecimals))
                    .SpendGas(owner.Address)
                    .EndScript());
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful(), "Stake LP Contract Failed");
            
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(owner, ProofOfWork.Minimal, () =>
                ScriptUtils
                    .BeginScript()
                    .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                    .CallContract("LP", "upgradeToDex", owner.Address)
                    .SpendGas(owner.Address)
                    .EndScript());
            
            var block = simulator.EndBlock().First();
            Assert.True(simulator.LastBlockWasSuccessful(), "Migrate Call failed");
            var resultBytes = block.GetResultForTransaction(tx.Hash);
        }

        public void SetupVirtualPools()
        {
            CreatePool(owner, kcal.Symbol, UnitConversion.ToBigInteger(1000, kcal.Decimals), cool.Symbol, UnitConversion.ToBigInteger(5000, cool.Decimals));
            CreatePool(owner, eth.Symbol, UnitConversion.ToBigInteger(2, eth.Decimals), cool.Symbol, UnitConversion.ToBigInteger(5000, cool.Decimals));
            CreatePool(owner, kcal.Symbol, UnitConversion.ToBigInteger(50000, kcal.Decimals), eth.Symbol, UnitConversion.ToBigInteger(5, eth.Decimals));
        }

        public BigInteger CreatePool(PhantasmaKeys user, string symbol0, BigInteger amount0, string symbol1, BigInteger amount1)
        {
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.Minimal, () =>
                ScriptUtils
                    .BeginScript()
                    .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.CreatePool), user.Address, symbol0, amount0, symbol1, amount1)
                    .SpendGas(user.Address)
                    .EndScript());
            var block = simulator.EndBlock().First();
            var resultBytes = block.GetResultForTransaction(tx.Hash);
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
            return txCost;
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

        public BigInteger OpenLimitOrder(string baseSymbol, string quoteSymbol, BigInteger orderSize, BigInteger orderPrice, ExchangeOrderSide side, bool IoC = false)
        {
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.Minimal, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(user.Address, Address.Null, core.simulator.MinimumFee, core.simulator.MinimumGasLimit)
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.OpenLimitOrder), user.Address, user.Address, baseSymbol, quoteSymbol, orderSize, orderPrice, side, IoC)
                    .SpendGas(user.Address)
                    .EndScript());
            simulator.EndBlock();
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
            return txCost;
        }
        
        public decimal OpenLimitOrder(decimal orderSize, decimal orderPrice, ExchangeOrderSide side, bool IoC = false)
        {
            return OpenLimitOrder(UnitConversion.ToBigInteger(orderSize, baseToken.Decimals), UnitConversion.ToBigInteger(orderPrice, quoteToken.Decimals), side, IoC);
        }

        //Opens a limit order and returns how many tokens the user purchased/sold
        public decimal OpenLimitOrder(BigInteger orderSize, BigInteger orderPrice, ExchangeOrderSide side, bool IoC = false)
        {
            var nexus = simulator.Nexus;       

            var baseSymbol = baseToken.Symbol;
            var baseDecimals = baseToken.Decimals;
            var quoteSymbol = quoteToken.Symbol;
            var quoteDecimals = quoteToken.Decimals;

            var orderSizeBigint = orderSize;
            var orderPriceBigint = orderPrice;

            var OpenerBaseTokensInitial = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, baseToken, user.Address);
            var OpenerQuoteTokensInitial = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, quoteToken, user.Address);

            BigInteger OpenerBaseTokensDelta = 0;
            BigInteger OpenerQuoteTokensDelta = 0;

            //get the starting balance for every address on the opposite side of the orderbook, so we can compare it to the final balance of each of those addresses
            var otherSide = side == ExchangeOrderSide.Buy ? ExchangeOrderSide.Sell : ExchangeOrderSide.Buy;
            var startingOppositeOrderbook = (ExchangeOrder[])simulator.InvokeContract( NativeContractKind.Exchange, "GetOrderBook", baseSymbol, quoteSymbol, otherSide).ToObject();
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
            var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.Minimal, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(user.Address, Address.Null, core.simulator.MinimumFee, core.simulator.MinimumGasLimit)
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.OpenLimitOrder), user.Address, user.Address, baseSymbol, quoteSymbol, orderSizeBigint, orderPriceBigint, side, IoC)
                    .SpendGas(user.Address)
                    .EndScript());
            simulator.EndBlock();

            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            BigInteger escrowedAmount = 0;

            //take into account the transfer of the owner's wallet to the chain address
            if (side == ExchangeOrderSide.Buy)
            {
                escrowedAmount = UnitConversion.ConvertDecimals(orderSize, baseDecimals, quoteDecimals) * orderPrice;
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
            Assert.True(wasNewOrderCreated, "Order was not created");

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
                Assert.True(wasNewOrderCancelled, "Non closed IoC order did not get cancelled");
            }
            else
            //if the new order was closed
            if (wasNewOrderClosed)
            {
                //and check that the order no longer exists on the orderbook
                try
                {
                    simulator.InvokeContract( NativeContractKind.Exchange, "GetExchangeOrder", createdOrderUid);
                    Assert.True(false, "Closed order exists on the orderbooks");
                }
                catch (Exception e)
                {
                    //purposefully empty, this is the expected code-path
                }
            }
            else //if the order was not IoC and it wasn't closed, then:
            {
                Assert.True(IoC == false, "All IoC orders should have been triggered by the previous ifs");

                //check that it still exists on the orderbook
                try
                {
                    createdOrderPostFill = (ExchangeOrder)simulator.InvokeContract( NativeContractKind.Exchange, "GetExchangeOrder", createdOrderUid).ToObject();
                }
                catch (Exception e)
                {
                    Assert.True(false, "Non-IoC unclosed order does not exist on the orderbooks");
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
                    //Assert.True(OtherAddressesTokensInitial.ContainsKey(tokenExchangeEvent.Address), "Address that was not on this orderbook received tokens");

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
                        //Assert.True(Abs(OpenerQuoteTokensDelta) == escrowedUsage - (quoteSymbol == DomainSettings.FuelTokenSymbol ? txCost : 0));
                        break;

                    case ExchangeOrderSide.Sell:
                        //Assert.True(Abs(OpenerBaseTokensDelta) == escrowedUsage - (baseSymbol == DomainSettings.FuelTokenSymbol ? txCost : 0));
                        break;
                }
            }
            else //if the user order was not closed and it wasnt IoC, it should have the correct unfilled amount
            {
                BigInteger actualRemainingEscrow;
                if (expectedRemainingEscrow == 0)
                {
                    Assert.True(wasNewOrderClosed, "Order wasn't closed but we expect no leftover escrow");
                    try
                    {
                        //should throw an exception because order should not exist
                        simulator.InvokeContract( NativeContractKind.Exchange, "GetOrderLeftoverEscrow", createdOrderUid);
                        actualRemainingEscrow = -1;
                    }
                    catch (Exception e)
                    {
                        actualRemainingEscrow = 0;
                    }
                }
                else
                {
                    actualRemainingEscrow = simulator.InvokeContract( NativeContractKind.Exchange, "GetOrderLeftoverEscrow", createdOrderUid).AsNumber();
                }
                
                Assert.True(expectedRemainingEscrow == actualRemainingEscrow);
            }


            //get the actual final balance of all addresses involved and make sure it matches the expected deltas
            var OpenerBaseTokensFinal = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, baseToken, user.Address);
            var OpenerQuoteTokensFinal = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, quoteToken, user.Address);

            Assert.True(OpenerBaseTokensFinal == OpenerBaseTokensDelta + OpenerBaseTokensInitial);
            Assert.True(OpenerQuoteTokensFinal == OpenerQuoteTokensDelta + OpenerQuoteTokensInitial);

            foreach (var entry in OtherAddressesTokensInitial)
            {
                var otherAddressInitialTokens = entry.Value;
                BigInteger delta = 0;

                if (OtherAddressesTokensDelta.ContainsKey(entry.Key))
                    delta = OtherAddressesTokensDelta[entry.Key];

                var targetSymbol = otherSide == ExchangeOrderSide.Buy ? baseSymbol : quoteSymbol;
                var targetToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, targetSymbol);

                var otherAddressFinalTokens = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, targetToken, entry.Key);

                Assert.True(otherAddressFinalTokens == delta + otherAddressInitialTokens);
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
            var startingOppositeOrderbook = (ExchangeOrder[])simulator.InvokeContract( NativeContractKind.Exchange, "GetOrderBook", baseSymbol, quoteSymbol, otherSide).ToObject();
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
                ScriptUtils.BeginScript()
                    .AllowGas(user.Address, Address.Null, core.simulator.MinimumFee, core.simulator.MinimumGasLimit)
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.OpenMarketOrder), user.Address, user.Address, baseSymbol, quoteSymbol, orderSizeBigint, side)
                    .SpendGas(user.Address)
                    .EndScript());
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
            Assert.True(wasNewOrderCreated, "No orders were created");

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
                Assert.True(wasNewOrderCancelled, "Non closed order did not get cancelled");
            }
            else
                //if the new order was closed
            if (wasNewOrderClosed)
            {
                Assert.True(wasNewOrderCancelled == false, "Closed order also got cancelled");
            }

            //check that the order no longer exists on the orderbook
            try
            {
                simulator.InvokeContract( NativeContractKind.Exchange, "GetExchangeOrder", createdOrderUid);
                Assert.True(false, "Market order exists on the orderbooks");
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
                    //Assert.True(OtherAddressesTokensInitial.ContainsKey(tokenExchangeEvent.Address), "Address that was not on this orderbook received tokens");

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
                    //Assert.True(Abs(OpenerQuoteTokensDelta) == expectedRemainingEscrow - (quoteSymbol == DomainSettings.FuelTokenSymbol ? txCost : 0));
                    break;

                case ExchangeOrderSide.Sell:
                    //Assert.True(Abs(OpenerBaseTokensDelta) == expectedRemainingEscrow - (baseSymbol == DomainSettings.FuelTokenSymbol ? txCost : 0));
                    break;
            }

            //get the actual final balance of all addresses involved and make sure it matches the expected deltas
            var OpenerBaseTokensFinal = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, baseToken, user.Address);
            var OpenerQuoteTokensFinal = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, quoteToken, user.Address);

            Console.WriteLine($"final: {OpenerBaseTokensFinal} == {OpenerBaseTokensDelta} + {OpenerBaseTokensInitial}");
            Assert.True(OpenerBaseTokensFinal == OpenerBaseTokensDelta + OpenerBaseTokensInitial);
            Assert.True(OpenerQuoteTokensFinal == OpenerQuoteTokensDelta + OpenerQuoteTokensInitial);

            foreach (var entry in OtherAddressesTokensInitial)
            {
                var otherAddressInitialTokens = entry.Value;
                BigInteger delta = 0;

                if (OtherAddressesTokensDelta.ContainsKey(entry.Key))
                    delta = OtherAddressesTokensDelta[entry.Key];

                var targetSymbol = otherSide == ExchangeOrderSide.Buy ? baseSymbol : quoteSymbol;
                var targetToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, targetSymbol);

                var otherAddressFinalTokens = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, targetToken, entry.Key);

                Assert.True(otherAddressFinalTokens == delta + otherAddressInitialTokens);
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
                    .AllowGas(user.Address, Address.Null, core.simulator.MinimumFee, core.simulator.MinimumGasLimit)
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
                    .AllowGas(user.Address, Address.Null, core.simulator.MinimumFee, core.simulator.MinimumGasLimit)
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
                    .AllowGas(user.Address, Address.Null, core.simulator.MinimumFee, core.simulator.MinimumGasLimit)
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.CancelOTCOrder), user.Address, uid)
                    .SpendGas(user.Address)
                    .EndScript());
            simulator.EndBlock();
        }

        // Get OTC Orders
        public ExchangeOrder[] GetOTC()
        {
            return (ExchangeOrder[])simulator.InvokeContract( NativeContractKind.Exchange, nameof(ExchangeContract.GetOTC)).ToObject();
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
                    .AllowGas(user.Address, Address.Null, core.simulator.MinimumFee, core.simulator.MinimumGasLimit)
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.AddLiquidity), user.Address, baseSymbol, amount0, pairSymbol, amount1)
                    .SpendGas(user.Address)
                    .EndScript()
            );
            var block = simulator.EndBlock().First();

            // Get Tx Cost
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
            return txCost;
        }
        
        public BigInteger RemoveLiquidity(string symbol0, BigInteger amount0, string symbol1, BigInteger amount1)
        {
            // SOUL / KCAL
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.Minimal, () =>
                ScriptUtils
                    .BeginScript()
                    .AllowGas(user.Address, Address.Null, core.simulator.MinimumFee, core.simulator.MinimumGasLimit)
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.RemoveLiquidity), user.Address, symbol0, amount0, symbol1, amount1)
                    .SpendGas(user.Address)
                    .EndScript());
            var block = simulator.EndBlock().First();
            var resultBytes = block.GetResultForTransaction(tx.Hash);
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
            return txCost;
        }

        public BigInteger CreatePool(string symbol0, BigInteger amount0, string symbol1, BigInteger amount1)
        {
            return core.CreatePool(user, symbol0, amount0, symbol1, amount1);
            /*simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.Minimal, () =>
                ScriptUtils
                    .BeginScript()
                    .AllowGas(user.Address, Address.Null, 1, 9999)
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.CreatePool), user.Address, symbol0, amount0, symbol1, amount1)
                    .SpendGas(user.Address)
                    .EndScript());
            var block = simulator.EndBlock().First();
            var resultBytes = block.GetResultForTransaction(tx.Hash);
            
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
            return txCost;*/
        }

        public BigInteger SwapTokens(string symbol0, string symbol1, BigInteger amount)
        {
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.Minimal, () =>
                ScriptUtils
                    .BeginScript()
                    .AllowGas(user.Address, Address.Null, core.simulator.MinimumFee, core.simulator.MinimumGasLimit)
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.SwapTokens), user.Address, symbol0, symbol1, amount)
                    .SpendGas(user.Address)
                    .EndScript()
            );
            var block = simulator.EndBlock().First();
            
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
            return txCost;
        }

        public BigInteger SwapFee(string symbol0, BigInteger amount)
        {
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
                ScriptUtils
                    .BeginScript()
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.SwapFee), user.Address, symbol0, amount)
                    .AllowGas(user.Address, Address.Null, core.simulator.MinimumFee, 999)
                    .SpendGas(user.Address)
                    .EndScript()
            );
            var block = simulator.EndBlock().First();
            
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
            return txCost;
        }

        public BigInteger ClaimFees(string symbol0, string symbol1)
        {
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.Minimal, () =>
                ScriptUtils
                    .BeginScript()
                    .AllowGas(user.Address, Address.Null, core.simulator.MinimumFee, core.simulator.MinimumGasLimit)
                    .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.ClaimFees), user.Address, symbol0, symbol1)
                    .SpendGas(user.Address)
                    .EndScript()
            );
            var block = simulator.EndBlock().First();
            
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
            return txCost;
        }

        public Pool GetPool(string symbol0, string symbol1)
        {
            var script = new ScriptBuilder()
                .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.GetPool), symbol0, symbol1)
                .EndScript();
            var result = simulator.InvokeScript(script);
            if (result == null) return new Pool();
            var pool = result.AsStruct<Pool>();
            return pool;
        }

        public LPTokenContentRAM GetPoolRAM(string symbol0, string symbol1)
        {
            var script = new ScriptBuilder()
                .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.GetMyPoolRAM), user.Address, symbol0, symbol1)
                .EndScript();
            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script, simulator.CurrentTime);
            if (result == null) return new LPTokenContentRAM();
            var nftRAM = result.AsStruct<LPTokenContentRAM>();
            return nftRAM;
        }

        public (BigInteger, BigInteger) GetUnclaimedFees(string symbol0, string symbol1)
        {
            var script = new ScriptBuilder()
                .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.GetUnclaimedFees), user.Address, symbol0, symbol1)
                .EndScript();
            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script, simulator.CurrentTime);
            var unclaimed = result.AsStruct<(BigInteger,BigInteger)>();
            return unclaimed;
        }


        public BigInteger GetRate(string symbol0, string symbol1, BigInteger amount)
        {
            var script = new ScriptBuilder()
                .CallContract(NativeContractKind.Exchange, nameof(ExchangeContract.GetRate), symbol0, symbol1, amount)
                .EndScript();

            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script, simulator.CurrentTime);

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
            simulator.GenerateTransfer(core.owner, user.Address, chain, DomainSettings.StakingTokenSymbol, UnitConversion.ToBigInteger(soul, DomainSettings.StakingTokenDecimals));
            if (kcal != 0)
                simulator.GenerateTransfer(core.owner, user.Address, chain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(kcal, DomainSettings.FuelTokenDecimals));
            simulator.EndBlock();
        }
        
        public void FundBaseToken(BigInteger quantity, bool fundFuel = false) => FundUser(true, quantity, fundFuel);
        public void FundQuoteToken(BigInteger quantity, bool fundFuel = false) => FundUser(false, quantity, fundFuel);

        public void CreateToken(CoreClass.ExchangeTokenInfo token)
        {
            simulator.BeginBlock();
            simulator.GenerateToken(core.owner, token.Symbol, token.Name, token.MaxSupply, token.Decimals, flags);
            simulator.MintTokens(core.owner, core.owner.Address, token.Symbol, token.MaxSupply);
            simulator.EndBlock();
        }

        //transfers the given quantity of a specified token to this user, plus some fuel to pay for transactions
        private void FundUser(bool fundBase, BigInteger quantity, bool fundFuel = false)
        {
            var token = fundBase ? baseToken : quoteToken;

            var chain = simulator.Nexus.RootChain as Chain;

            simulator.BeginBlock();
            simulator.GenerateTransfer(core.owner, user.Address, chain, token.Symbol, quantity);

            if (fundFuel)
                simulator.GenerateTransfer(core.owner, user.Address, chain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals));

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
