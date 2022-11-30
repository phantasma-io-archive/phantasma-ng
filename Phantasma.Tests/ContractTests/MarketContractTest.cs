using System;
using System.Linq;
using System.Numerics;
using Phantasma.Simulator;
using Phantasma.Core.Types;
using Phantasma.Core.Cryptography;
using Phantasma.Business.Blockchain;
using Phantasma.Core.Domain;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Numerics;

using Xunit;
namespace Phantasma.Business.Tests.Blockchain.Contracts;

[Collection("MarketLegacyContractTest")]
public class MarketContractTest
{
    string sysAddress;
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

    public MarketContractTest()
    {
        Initialize();
    }

    public void Initialize()
    {
        sysAddress = "S3d79FvexQeerRioAY3pGYpNPFx7oJkMV4KazdTHdGDA5iy";
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
    public void TestMarketContractBasic()
    {
        var chain = nexus.RootChain;

        var symbol = "COOL";

        var testUser = PhantasmaKeys.Generate();

        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain as Chain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Transferable);
        simulator.EndBlock();

        var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
        Assert.True(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the user pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.True(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

        var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        // Mint a new CoolToken 
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
        simulator.EndBlock();

        // obtain tokenID
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        var tokenID = ownedTokenList.First();

        var auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        var previousAuctionCount = auctions.Length;

        // verify nft presence on the user post-mint
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        tokenID = ownedTokenList.First();

        var price = 1000;

        Timestamp endDate = simulator.CurrentTime + TimeSpan.FromDays(2);

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
        ScriptUtils.
              BeginScript().
              AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
              CallContract(NativeContractKind.Market, "SellToken", testUser.Address, token.Symbol, DomainSettings.FuelTokenSymbol, tokenID, price, endDate).
              SpendGas(testUser.Address).
              EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        Assert.True(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
              BeginScript().
              AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
              CallContract(NativeContractKind.Market, "BuyToken", owner.Address, token.Symbol, auctions[previousAuctionCount].TokenID).
              SpendGas(owner.Address).
              EndScript()
        );
        simulator.EndBlock();

        auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        Assert.True(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

        // verify that the nft was really moved
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.True(ownedTokenList.Count() == 0, "How does the seller still have one?");

        ownedTokenList = ownerships.Get(chain.Storage, owner.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the buyer does not have what he bought?");
    }

    [Fact]
    public void TestMarketContractAuctionDutch()
    {
        var chain = nexus.RootChain;

        var symbol = "COOL";

        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Transferable);
        simulator.EndBlock();

        var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
        Assert.True(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the user pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

        var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        // Mint a new CoolToken 
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, user.Address, symbol, tokenROM, tokenRAM, 0);
        simulator.EndBlock();

        // obtain tokenID
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        var tokenID = ownedTokenList.First();

        var auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        var previousAuctionCount = auctions.Length;

        // verify nft presence on the user post-mint
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        tokenID = ownedTokenList.First();

        var price = 1500;
        var endPrice = 500;
        var extensionPeriod = 300;
        var listingFee = 0;
        var buyingFee = 0;
        var auctionType = 3; 
        Timestamp startDate = simulator.CurrentTime + TimeSpan.FromDays(1);
        Timestamp endDate = simulator.CurrentTime + TimeSpan.FromDays(3);

        // verify balance before
        var tokenToSell = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
        var balanceOwnerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);

        // list token as dutch auction
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
        ScriptUtils.
              BeginScript().
              AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
              CallContract(NativeContractKind.Market, nameof(MarketContract.ListToken), user.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, price, endPrice, startDate, endDate, extensionPeriod, auctionType, listingFee, Address.Null).
              SpendGas(user.Address).
              EndScript()
        );
        simulator.EndBlock();

        // verify auction is here
        auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        Assert.True(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, endPrice, buyingFee, Address.Null).
            SpendGas(owner.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());


        // move time half way through auction
        simulator.TimeSkipDays(2);

        // make one self bid (should fail)
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), user.Address, token.Symbol, tokenID, price + 1000, buyingFee, Address.Null).
            SpendGas(user.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());


        // make one bid
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, 0, buyingFee, Address.Null).
            SpendGas(owner.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());


        // verify auctions empty
        auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        Assert.True(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

        // verify that the nft was really moved
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 0, "How does the seller still have one?");

        ownedTokenList = ownerships.Get(chain.Storage, owner.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the buyer does not have what he bought?");

        // verify balance after
        var balanceOwnerAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);
        Assert.True(balanceOwnerAfter == balanceOwnerBefore - 1000, " balanceOwnerBefore: " + balanceOwnerBefore + " balanceOwnerAfter: " + balanceOwnerAfter);
    }

    [Fact]
    public void TestMarketContractAuctionClassic()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var chain = nexus.RootChain;

        var symbol = "COOL";

        var testUser = PhantasmaKeys.Generate();
        var testUser2 = PhantasmaKeys.Generate();

        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain as Chain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain as Chain, DomainSettings.StakingTokenSymbol, 1000000);
        simulator.GenerateTransfer(owner, testUser2.Address, nexus.RootChain as Chain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, testUser2.Address, nexus.RootChain as Chain, DomainSettings.StakingTokenSymbol, 1000000);
        simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Transferable);
        simulator.EndBlock();

        var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
        Assert.True(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the user pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.True(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

        var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        // Mint a new CoolToken 
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
        simulator.EndBlock();

        // obtain tokenID
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        var tokenID = ownedTokenList.First();

        var auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        var previousAuctionCount = auctions.Length;

        // verify nft presence on the user post-mint
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        tokenID = ownedTokenList.First();

        var price = 1500;
        var endPrice = 0;
        var bidPrice = 2500;
        var extensionPeriod = 3600;
        var listingFee = 0;
        var buyingFee = 0;
        var auctionType = 1; 
        Timestamp startDate = simulator.CurrentTime + TimeSpan.FromDays(1);
        Timestamp endDate = simulator.CurrentTime + TimeSpan.FromDays(3);

        // verify balance before
        var tokenToSell = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
        var balanceOwnerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);

        // list token with a fee but no fee address (should fail)
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.ListToken), testUser.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, price, endPrice, startDate, endDate, extensionPeriod, auctionType, 2, Address.Null).
            SpendGas(testUser.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());

        // list token as Classic auction
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
        ScriptUtils.
              BeginScript().
              AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
              CallContract(NativeContractKind.Market, nameof(MarketContract.ListToken), testUser.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, price, endPrice, startDate, endDate, extensionPeriod, auctionType, listingFee, Address.Null).
              SpendGas(testUser.Address).
              EndScript()
        );
        simulator.EndBlock();

        // verify auction is here
        auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        Assert.True(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

        // make one bid before auction starts (should fail)
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, endPrice, buyingFee, Address.Null).
            SpendGas(owner.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());


        // move time post start date
        simulator.TimeSkipDays(2);

        // make one bid lower (should fail)
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, price - 100, buyingFee, Address.Null).
            SpendGas(owner.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());


        // make one bid
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
              BeginScript().
              AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
              CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, bidPrice, buyingFee, Address.Null).
              SpendGas(owner.Address).
              EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());


        // make one more bid from same address
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, bidPrice + 100, buyingFee, Address.Null).
            SpendGas(owner.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());


        // cancel auction after it received bids (should fail)
        simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.
                BeginScript().
                AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
                CallContract(NativeContractKind.Market, nameof(MarketContract.CancelSale), token.Symbol, tokenID).
                SpendGas(testUser.Address).
                EndScript()
            );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());


        // make one bid lower (should fail)
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser2, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(testUser2.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), testUser2.Address, token.Symbol, tokenID, bidPrice - 100, buyingFee, Address.Null).
            SpendGas(testUser2.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());


        // move time 5 minutes before end of auction
        simulator.TimeSkipHours(23);
        simulator.TimeSkipMinutes(55);

        // make one bid < 1% more (should fail)
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser2, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(testUser2.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), testUser2.Address, token.Symbol, tokenID, bidPrice + 101, buyingFee, Address.Null).
            SpendGas(testUser2.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());


        // make one bid which will trigger extend time
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser2, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(testUser2.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), testUser2.Address, token.Symbol, tokenID, bidPrice + 200, buyingFee, Address.Null).
            SpendGas(testUser2.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());


        // move time 45 minutes to check if time was properly extended
        simulator.TimeSkipMinutes(45);

        // make one more outbid, which would have claimed without extension
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, bidPrice + 300, buyingFee, Address.Null).
            SpendGas(owner.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());


        // check if auction is still there post extension
        auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        Assert.True(auctions.Length == previousAuctionCount + 1, "auction ids should not be empty at this point");

        // move time post end date
        simulator.TimeSkipDays(2);

        // make one bid to claim nft
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, 0, buyingFee, Address.Null).
            SpendGas(owner.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());


        // verify auctions empty
        auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        Assert.True(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

        // verify that the nft was really moved
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.True(ownedTokenList.Count() == 0, "How does the seller still have one?");

        ownedTokenList = ownerships.Get(chain.Storage, owner.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the buyer does not have what he bought?");

        // verify balance after
        var balanceOwnerAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);
        Assert.True(balanceOwnerAfter == balanceOwnerBefore - (bidPrice + 300), " balanceOwnerBefore: " + balanceOwnerBefore + " bidPrice + 200: " + bidPrice + 300 + " balanceOwnerAfter: " + balanceOwnerAfter);
    }

    [Fact]
    public void TestMarketContractAuctionClassicNoWinner()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var chain = nexus.RootChain;

        var symbol = "COOL";

        var testUser = PhantasmaKeys.Generate();
        var testUser2 = PhantasmaKeys.Generate();

        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain as Chain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain as Chain, DomainSettings.StakingTokenSymbol, 1000000);
        simulator.GenerateTransfer(owner, testUser2.Address, nexus.RootChain as Chain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, testUser2.Address, nexus.RootChain as Chain, DomainSettings.StakingTokenSymbol, 1000000);
        simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Transferable);
        simulator.EndBlock();

        var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
        Assert.True(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the user pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.True(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

        var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        // Mint a new CoolToken 
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
        simulator.EndBlock();

        // obtain tokenID
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        var tokenID = ownedTokenList.First();

        var auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        var previousAuctionCount = auctions.Length;

        // verify nft presence on the user post-mint
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        tokenID = ownedTokenList.First();

        var price = 1500;
        var endPrice = 0;
        var bidPrice = 2500;
        var extensionPeriod = 3600;
        var listingFee = 0;
        var buyingFee = 0;
        var auctionType = 1; 
        Timestamp startDate = simulator.CurrentTime + TimeSpan.FromDays(1);
        Timestamp endDate = simulator.CurrentTime + TimeSpan.FromDays(3);

        // verify balance before
        var tokenToSell = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
        var balanceOwnerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);

        // list token with a negative price (should fail)
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.ListToken), testUser.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, -3000, endPrice, startDate, endDate, extensionPeriod, auctionType, listingFee, Address.Null).
            SpendGas(testUser.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());


        // list token as Classic auction
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
        ScriptUtils.
              BeginScript().
              AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
              CallContract(NativeContractKind.Market, nameof(MarketContract.ListToken), testUser.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, price, endPrice, startDate, endDate, extensionPeriod, auctionType, listingFee, Address.Null).
              SpendGas(testUser.Address).
              EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());


        // verify auction is here
        auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        Assert.True(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

        // move time post auction end date
        simulator.TimeSkipDays(4);

        // make one bid to claim nft
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, 0, buyingFee, Address.Null).
            SpendGas(owner.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());


        // verify auctions empty
        auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        Assert.True(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

        // verify that the nft is back to the original owner
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the seller did not get back his NFT?");
    }

    [Fact]
    public void TestMarketContractAuctionReserve()
    {
        var chain = nexus.RootChain;

        var symbol = "COOL";

        var testUser = PhantasmaKeys.Generate();
        var testUser2 = PhantasmaKeys.Generate();

        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain as Chain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain as Chain, DomainSettings.StakingTokenSymbol, 1000000);
        simulator.GenerateTransfer(owner, testUser2.Address, nexus.RootChain as Chain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, testUser2.Address, nexus.RootChain as Chain, DomainSettings.StakingTokenSymbol, 1000000);
        simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Transferable);
        simulator.EndBlock();

        var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
        Assert.True(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the user pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.True(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

        var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        // Mint a new CoolToken 
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
        simulator.EndBlock();

        // obtain tokenID
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        var tokenID = ownedTokenList.First();

        var auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        var previousAuctionCount = auctions.Length;

        // verify nft presence on the user post-mint
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        tokenID = ownedTokenList.First();

        var price = 1500;
        var endPrice = 0;
        var bidPrice = 2500;
        var bidPrice2 = 3500;
        var bidPrice3 = 4500;
        var extensionPeriod = 300;
        var listingFee = 0;
        var buyingFee = 0;
        var auctionType = 2; 
        Timestamp startDate = simulator.CurrentTime + TimeSpan.FromDays(1);
        Timestamp endDate = simulator.CurrentTime + TimeSpan.FromDays(3);

        // verify balance before
        var tokenToSell = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
        var balanceOwnerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);
        var balanceTestUserBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, testUser.Address);
        var balanceTestUser2Before = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, testUser2.Address);

        // list token as reserve auction
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
        ScriptUtils.
              BeginScript().
              AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
              CallContract(NativeContractKind.Market, nameof(MarketContract.ListToken), testUser.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, price, endPrice, startDate, endDate, extensionPeriod, auctionType, listingFee, Address.Null).
              SpendGas(testUser.Address).
              EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());


        // verify auction is here
        auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        Assert.True(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

        // make one bid below reserve price (should fail)
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, endPrice, buyingFee, Address.Null).
            SpendGas(owner.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());


        // make one bid above reserve price
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
              BeginScript().
              AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
              CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, bidPrice, buyingFee, Address.Null).
              SpendGas(owner.Address).
              EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());


        // make one bid lower (should fail)
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, bidPrice - 100, buyingFee, Address.Null).
            SpendGas(owner.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());

        // make one other address outbids previous one
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser2, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(testUser2.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), testUser2.Address, token.Symbol, tokenID, bidPrice2, buyingFee, Address.Null).
            SpendGas(testUser2.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());


        // move time between bids
        simulator.TimeSkipHours(2);

        // make original address outbids previous one
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, bidPrice3, buyingFee, Address.Null).
            SpendGas(owner.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());


        // move time post end date
        simulator.TimeSkipDays(2);

        // claim nft
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
              BeginScript().
              AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
              CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, 0, buyingFee, Address.Null).
              SpendGas(owner.Address).
              EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());


        // verify auctions empty
        auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        Assert.True(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

        // verify that the nft was really moved
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.True(ownedTokenList.Count() == 0, "How does the seller still have one?");

        ownedTokenList = ownerships.Get(chain.Storage, owner.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the buyer does not have what he bought?");

        // verify balance after
        var balanceOwnerAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);
        var balanceTestUserAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, testUser.Address);
        var balanceTestUser2After = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, testUser2.Address);
        Assert.True(balanceOwnerAfter == balanceOwnerBefore - bidPrice3, " balanceOwnerBefore: " + balanceOwnerBefore + " bidPrice: " + bidPrice3 + " balanceOwnerAfter: " + balanceOwnerAfter);
        Assert.True(balanceTestUserAfter == balanceTestUserBefore + bidPrice3, " balanceOwnerBefore: " + balanceTestUserBefore + " bidPrice: " + bidPrice3 + " balanceOwnerAfter: " + balanceTestUserAfter);
        Assert.True(balanceTestUser2After == balanceTestUser2Before, " balanceTestUser2After: " + balanceTestUser2After + " balanceTestUser2Before: " + balanceTestUser2Before);
    }

    [Fact]
    public void TestMarketContractAuctionFixed()
    {
        var chain = nexus.RootChain;

        var symbol = "COOL";


        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Transferable);
        simulator.EndBlock();

        var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
        Assert.True(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the user pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

        var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        // Mint a new CoolToken 
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, user.Address, symbol, tokenROM, tokenRAM, 0);
        simulator.EndBlock();

        // obtain tokenID
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        var tokenID = ownedTokenList.First();

        var auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        var previousAuctionCount = auctions.Length;

        // verify nft presence on the user post-mint
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        tokenID = ownedTokenList.First();

        var price = 1500;
        var endPrice = 0;
        var bidPrice = 0;
        var extensionPeriod = 0;
        var listingFee = 0;
        var buyingFee = 0;
        var auctionType = 0; 
        Timestamp startDate = simulator.CurrentTime + TimeSpan.FromDays(1);
        Timestamp endDate = simulator.CurrentTime + TimeSpan.FromDays(3);

        // verify balance before
        var tokenToSell = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
        var balanceOwnerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);

        // list token as fixed auction
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
        ScriptUtils.
              BeginScript().
              AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
              CallContract(NativeContractKind.Market, nameof(MarketContract.ListToken), user.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, price, endPrice, startDate, endDate, extensionPeriod, auctionType, listingFee, Address.Null).
              SpendGas(user.Address).
              EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());


        // verify auction is here
        auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        Assert.True(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

        // make one bid before auction starts (should fail)
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, endPrice, buyingFee, Address.Null).
            SpendGas(owner.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());

        // move time post start date
        simulator.TimeSkipDays(2);

        // make one bid lower (should fail)
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, price - 100, buyingFee, Address.Null).
            SpendGas(owner.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());


        // make one higher (should fail)
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, bidPrice + 100, buyingFee, Address.Null).
            SpendGas(owner.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());

        // make one bid - also claims it
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
              BeginScript().
              AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
              CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, price, buyingFee, Address.Null).
              SpendGas(owner.Address).
              EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());


        // verify auctions empty
        auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        Assert.True(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

        // verify that the nft was really moved
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 0, "How does the seller still have one?");

        ownedTokenList = ownerships.Get(chain.Storage, owner.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the buyer does not have what he bought?");

        // verify balance after
        var balanceOwnerAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);
        Assert.True(balanceOwnerAfter == balanceOwnerBefore - price, " balanceOwnerBefore: " + balanceOwnerBefore + " price: " + price + " balanceOwnerAfter: " + balanceOwnerAfter);
    }

    [Fact]
    public void TestMarketContractAuctionEdit()
    {
        var chain = nexus.RootChain;

        var symbol = "COOL";

        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        //simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain as Chain, DomainSettings.FuelTokenSymbol, 1000000);
        //simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain as Chain, DomainSettings.StakingTokenSymbol, 1000000);
        simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Transferable);
        simulator.EndBlock();

        var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
        Assert.True(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the user pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

        var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        // Mint a new CoolToken 
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, user.Address, symbol, tokenROM, tokenRAM, 0);
        simulator.EndBlock();

        // obtain tokenID
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        var tokenID = ownedTokenList.First();

        var auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        var previousAuctionCount = auctions.Length;

        // verify nft presence on the user post-mint
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        tokenID = ownedTokenList.First();

        var price = 1500;
        var endPrice = 0;
        var extensionPeriod = 0;
        var listingFee = 0;
        var buyingFee = 0;
        var auctionType = 0; 
        Timestamp startDate = simulator.CurrentTime + TimeSpan.FromDays(1);
        Timestamp endDate = simulator.CurrentTime + TimeSpan.FromDays(3);
        Timestamp endDateWrong = simulator.CurrentTime + TimeSpan.FromDays(1);

        // verify balance before
        var tokenToSell = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
        var balanceOwnerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);

        // list token as fixed auction
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
        ScriptUtils.
              BeginScript().
              AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
              CallContract(NativeContractKind.Market, nameof(MarketContract.ListToken), user.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, price, endPrice, startDate, endDate, extensionPeriod, auctionType, listingFee, Address.Null).
              SpendGas(user.Address).
              EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());


        // verify auction is here
        auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        Assert.True(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

        // edit auction price with wrong symbol (should fail)
        simulator.BeginBlock();
            simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.
                BeginScript().
                AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
                CallContract(NativeContractKind.Market, nameof(MarketContract.EditAuction), user.Address, DomainSettings.StakingTokenSymbol, DomainSettings.StakingTokenSymbol, tokenID, 2500, endPrice, startDate, endDate, extensionPeriod).
                SpendGas(user.Address).
                EndScript()
            );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());

        // edit auction price with correct symbol
        simulator.BeginBlock();
            simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.
                BeginScript().
                AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
                CallContract(NativeContractKind.Market, nameof(MarketContract.EditAuction), user.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, 2500, 0, Timestamp.Null, Timestamp.Null, 0).
                SpendGas(user.Address).
                EndScript()
            );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        // edit auction price with incorrect end date (should fail)
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.
                BeginScript().
                AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
                CallContract(NativeContractKind.Market, nameof(MarketContract.EditAuction), user.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, 0, 0, startDate, endDateWrong, 0).
                SpendGas(user.Address).
                EndScript()
            );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());
        
        // move time post start date
        simulator.TimeSkipDays(2);

        // make one bid - also claims it
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
              BeginScript().
              AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
              CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, 2500, buyingFee, Address.Null).
              SpendGas(owner.Address).
              EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());


        // verify auctions empty
        auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        Assert.True(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

        // verify that the nft was really moved
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 0, "How does the seller still have one?");

        ownedTokenList = ownerships.Get(chain.Storage, owner.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the buyer does not have what he bought?");

        // verify balance after
        var balanceOwnerAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);
        Assert.True(balanceOwnerAfter == balanceOwnerBefore - 2500, " balanceOwnerBefore: " + balanceOwnerBefore + " price: " + 2500 + " balanceOwnerAfter: " + balanceOwnerAfter);
    }

    [Fact]
    public void TestMarketContractAuctionCancel()
    {
        var chain = nexus.RootChain;

        var symbol = "COOL";

        //var testUser = PhantasmaKeys.Generate();

        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        //simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain as Chain, DomainSettings.FuelTokenSymbol, 10000000000);
        //simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain as Chain, DomainSettings.StakingTokenSymbol, 1000000);
        simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Transferable);
        simulator.EndBlock();

        var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
        Assert.True(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the user pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

        var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        // Mint a new CoolToken 
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, user.Address, symbol, tokenROM, tokenRAM, 0);
        simulator.EndBlock();

        // obtain tokenID
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        var tokenID = ownedTokenList.First();

        var auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        var previousAuctionCount = auctions.Length;

        // verify nft presence on the user post-mint
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        tokenID = ownedTokenList.First();

        var price = 1500;
        var endPrice = 0;
        var extensionPeriod = 300;
        var listingFee = 0;
        var auctionType = 1; 
        Timestamp startDate = simulator.CurrentTime + TimeSpan.FromDays(1);
        Timestamp endDate = simulator.CurrentTime + TimeSpan.FromDays(3);

        // verify balance before
        var tokenToSell = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
        var balanceOwnerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);

        // list token as classic auction
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
        ScriptUtils.
              BeginScript().
              AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
              CallContract(NativeContractKind.Market, nameof(MarketContract.ListToken), user.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, price, endPrice, startDate, endDate, extensionPeriod, auctionType, listingFee, Address.Null).
              SpendGas(user.Address).
              EndScript()
        );
        simulator.EndBlock();

        // verify auction is here
        auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        Assert.True(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

        // move half way through sale
        simulator.TimeSkipDays(1);

        // cancel auction from wrong owner (should fail)
        simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
                CallContract(NativeContractKind.Market, nameof(MarketContract.CancelSale), token.Symbol, tokenID).
                SpendGas(owner.Address).
                EndScript()
            );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());


        // move past end date
        simulator.TimeSkipDays(2);

        // cancel auction price from current owner
        simulator.BeginBlock();
            simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.
                BeginScript().
                AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
                CallContract(NativeContractKind.Market, nameof(MarketContract.CancelSale), token.Symbol, tokenID).
                SpendGas(user.Address).
                EndScript()
            );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());


        // verify auctions empty
        auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        Assert.True(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

        // verify that the nft was not moved
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the seller still have zero?");

        ownedTokenList = ownerships.Get(chain.Storage, owner.Address);
        Assert.True(ownedTokenList.Count() == 0, "How does the buyer has it while it was cancelled?");

        // verify balance after
        var balanceOwnerAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);
        Assert.True(balanceOwnerAfter == balanceOwnerBefore, " balanceOwnerBefore: " + balanceOwnerBefore + " balanceOwnerAfter: " + balanceOwnerAfter);

    }

    [Fact]
    public void TestMarketContractAuctionFeesDivisible()
    {
        var chain = nexus.RootChain;

        var symbol = "COOL";

        var buyingFeeUser = PhantasmaKeys.Generate();
        var listingFeeUser = PhantasmaKeys.Generate();

        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Transferable);
        simulator.EndBlock();

        var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
        Assert.True(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the user pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

        var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        // Mint a new CoolToken 
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, user.Address, symbol, tokenROM, tokenRAM, 0);
        simulator.EndBlock();

        // obtain tokenID
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        var tokenID = ownedTokenList.First();

        var auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        var previousAuctionCount = auctions.Length;

        // verify nft presence on the user post-mint
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        tokenID = ownedTokenList.First();

        var price = 1500;
        var endPrice = 0;
        var bidPrice = 0;
        var extensionPeriod = 0;
        var listingFee = 4;
        var buyingFee = 5;
        var auctionType = 0; 
        var listingFeeAddress = listingFeeUser.Address;
        var buyingFeeAddress = buyingFeeUser.Address;
        Timestamp startDate = simulator.CurrentTime + TimeSpan.FromDays(1);
        Timestamp endDate = simulator.CurrentTime + TimeSpan.FromDays(3);

        // verify balance before
        var tokenTicker = DomainSettings.StakingTokenSymbol;
        var tokenToSell = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, tokenTicker);
        var balanceOwnerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, owner.Address);
        var balanceSellerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, user.Address);
        var balanceListFeeBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, listingFeeAddress);
        var balanceBuyFeeBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, buyingFeeAddress);

        // list token as fixed auction
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
        ScriptUtils.
              BeginScript().
              AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
              CallContract(NativeContractKind.Market, nameof(MarketContract.ListToken), user.Address, token.Symbol, tokenTicker, tokenID, price, endPrice, startDate, endDate, extensionPeriod, auctionType, listingFee, listingFeeAddress).
              SpendGas(user.Address).
              EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());


        // verify auction is here
        auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        Assert.True(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

        // make one bid before auction starts (should fail)
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, endPrice, buyingFee, buyingFeeAddress).
            SpendGas(owner.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());


        // move time post start date
        simulator.TimeSkipDays(2);

        // make one bid lower (should fail)
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, price - 100, buyingFee, buyingFeeAddress).
            SpendGas(owner.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());


        // make one higher (should fail)
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, bidPrice + 100, buyingFee, buyingFeeAddress).
            SpendGas(owner.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());


        // make one bid - also claims it
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
              BeginScript().
              AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
              CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, price, buyingFee, buyingFeeAddress).
              SpendGas(owner.Address).
              EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());


        // verify auctions empty
        auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        Assert.True(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

        // verify that the nft was really moved
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 0, "How does the seller still have one?");

        ownedTokenList = ownerships.Get(chain.Storage, owner.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the buyer does not have what he bought?");

        // verify balance after
        var balanceOwnerAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, owner.Address);
        var balanceSellerAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, user.Address);
        var balanceListFeeAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, listingFeeAddress);
        var balanceBuyFeeAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, buyingFeeAddress);
        Assert.True(balanceListFeeAfter == balanceListFeeBefore + (listingFee * price / 100), " balanceSellerBefore: " + balanceSellerBefore + " balanceSellerAfter: " + balanceSellerAfter + " balanceOwnerBefore: " + balanceOwnerBefore + " balanceOwnerAfter: " + balanceOwnerAfter + " balanceListFeeBefore: " + balanceListFeeBefore + " balanceListFeeAfter: " + balanceListFeeAfter + " balanceBuyFeeBefore: " + balanceBuyFeeBefore + " balanceBuyFeeAfter: " + balanceBuyFeeAfter);
        Assert.True(balanceBuyFeeAfter == balanceBuyFeeBefore + (buyingFee * price / 100), " balanceSellerBefore: " + balanceSellerBefore + " balanceSellerAfter: " + balanceSellerAfter + " balanceOwnerBefore: " + balanceOwnerBefore + " balanceOwnerAfter: " + balanceOwnerAfter + " balanceListFeeBefore: " + balanceListFeeBefore + " balanceListFeeAfter: " + balanceListFeeAfter + " balanceBuyFeeBefore: " + balanceBuyFeeBefore + " balanceBuyFeeAfter: " + balanceBuyFeeAfter);
        Assert.True(balanceSellerAfter == balanceSellerBefore + price, " balanceSellerBefore: " + balanceSellerBefore + " balanceSellerAfter: " + balanceSellerAfter + " balanceOwnerBefore: " + balanceOwnerBefore + " balanceOwnerAfter: " + balanceOwnerAfter + " balanceListFeeBefore: " + balanceListFeeBefore + " balanceListFeeAfter: " + balanceListFeeAfter + " balanceBuyFeeBefore: " + balanceBuyFeeBefore + " balanceBuyFeeAfter: " + balanceBuyFeeAfter);
        Assert.True(balanceOwnerAfter == balanceOwnerBefore - price - (listingFee * price / 100) - (buyingFee * price / 100), " balanceSellerBefore: " + balanceSellerBefore + " balanceSellerAfter: " + balanceSellerAfter + " balanceOwnerBefore: " + balanceOwnerBefore + " balanceOwnerAfter: " + balanceOwnerAfter + " balanceListFeeBefore: " + balanceListFeeBefore + " balanceListFeeAfter: " + balanceListFeeAfter + " balanceBuyFeeBefore: " + balanceBuyFeeBefore + " balanceBuyFeeAfter: " + balanceBuyFeeAfter);
    }

    [Fact]
    public void TestMarketContractAuctionFeesIndivisible()
    {
        var chain = nexus.RootChain;
        simulator.GetFundsInTheFuture(owner);

        var symbol = "COOL";
        var tokenTicker = "MKNI";

        var buyingFeeUser = PhantasmaKeys.Generate();
        var listingFeeUser = PhantasmaKeys.Generate();

        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        simulator.GenerateToken(owner, tokenTicker, "MKNI", 10000000, 0, TokenFlags.Transferable | TokenFlags.Fungible);
        simulator.MintTokens(owner, owner.Address, tokenTicker, 10000000);
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain as Chain, tokenTicker, 10000);
        simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Transferable);
        simulator.EndBlock();

        var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
        Assert.True(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the user pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

        var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        // Mint a new CoolToken 
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, user.Address, symbol, tokenROM, tokenRAM, 0);
        simulator.EndBlock();

        // obtain tokenID
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        var tokenID = ownedTokenList.First();

        var auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        var previousAuctionCount = auctions.Length;

        // verify nft presence on the user post-mint
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        tokenID = ownedTokenList.First();

        var price = 3;
        var endPrice = 0;
        var bidPrice = 0;
        var extensionPeriod = 0;
        var listingFee = 2;
        var buyingFee = 3;
        var auctionType = 0; 
        var listingFeeAddress = listingFeeUser.Address;
        var buyingFeeAddress = buyingFeeUser.Address;
        Timestamp startDate = simulator.CurrentTime + TimeSpan.FromDays(1);
        Timestamp endDate = simulator.CurrentTime + TimeSpan.FromDays(3);

        // verify balance before
        var tokenToSell = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, tokenTicker);
        var balanceOwnerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, owner.Address);
        var balanceSellerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, user.Address);
        var balanceListFeeBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, listingFeeAddress);
        var balanceBuyFeeBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, buyingFeeAddress);

        // list token as fixed auction
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
        ScriptUtils.
              BeginScript().
              AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
              CallContract(NativeContractKind.Market, nameof(MarketContract.ListToken), user.Address, token.Symbol, tokenTicker, tokenID, price, endPrice, startDate, endDate, extensionPeriod, auctionType, listingFee, listingFeeAddress).
              SpendGas(user.Address).
              EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        // verify auction is here
        auctions = (MarketAuction[])simulator.InvokeContract(NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        Assert.True(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

        // make one bid before auction starts (should fail)
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, endPrice, buyingFee, buyingFeeAddress).
            SpendGas(owner.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());

        // move time post start date
        simulator.TimeSkipDays(2);

        // make one bid lower (should fail)
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, price - 100, buyingFee, buyingFeeAddress).
            SpendGas(owner.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());


        // make one higher (should fail)
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
            BeginScript().
            AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
            CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, bidPrice + 100, buyingFee, buyingFeeAddress).
            SpendGas(owner.Address).
            EndScript()
        );
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());


        // make one bid - also claims it
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
        ScriptUtils.
              BeginScript().
              AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
              CallContract(NativeContractKind.Market, nameof(MarketContract.BidToken), owner.Address, token.Symbol, tokenID, price, buyingFee, buyingFeeAddress).
              SpendGas(owner.Address).
              EndScript()
        );
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());


        // verify auctions empty
        auctions = (MarketAuction[])simulator.InvokeContract( NativeContractKind.Market, nameof(MarketContract.GetAuctions)).ToObject();
        Assert.True(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

        // verify that the nft was really moved
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 0, "How does the seller still have one?");

        ownedTokenList = ownerships.Get(chain.Storage, owner.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the buyer does not have what he bought?");

        // verify balance after
        var balanceOwnerAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, owner.Address);
        var balanceSellerAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, user.Address);
        var balanceListFeeAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, listingFeeAddress);
        var balanceBuyFeeAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, buyingFeeAddress);
        Assert.True(balanceListFeeAfter == balanceListFeeBefore + 1, " balanceSellerBefore: " + balanceSellerBefore + " balanceSellerAfter: " + balanceSellerAfter + " balanceOwnerBefore: " + balanceOwnerBefore + " balanceOwnerAfter: " + balanceOwnerAfter + " balanceListFeeBefore: " + balanceListFeeBefore + " balanceListFeeAfter: " + balanceListFeeAfter + " balanceBuyFeeBefore: " + balanceBuyFeeBefore + " balanceBuyFeeAfter: " + balanceBuyFeeAfter);
        Assert.True(balanceBuyFeeAfter == balanceBuyFeeBefore + 1, " balanceSellerBefore: " + balanceSellerBefore + " balanceSellerAfter: " + balanceSellerAfter + " balanceOwnerBefore: " + balanceOwnerBefore + " balanceOwnerAfter: " + balanceOwnerAfter + " balanceListFeeBefore: " + balanceListFeeBefore + " balanceListFeeAfter: " + balanceListFeeAfter + " balanceBuyFeeBefore: " + balanceBuyFeeBefore + " balanceBuyFeeAfter: " + balanceBuyFeeAfter);
        Assert.True(balanceSellerAfter == balanceSellerBefore + price, " balanceSellerBefore: " + balanceSellerBefore + " balanceSellerAfter: " + balanceSellerAfter + " balanceOwnerBefore: " + balanceOwnerBefore + " balanceOwnerAfter: " + balanceOwnerAfter + " balanceListFeeBefore: " + balanceListFeeBefore + " balanceListFeeAfter: " + balanceListFeeAfter + " balanceBuyFeeBefore: " + balanceBuyFeeBefore + " balanceBuyFeeAfter: " + balanceBuyFeeAfter);
        Assert.True(balanceOwnerAfter == balanceOwnerBefore - price - 1 - 1, " balanceSellerBefore: " + balanceSellerBefore + " balanceSellerAfter: " + balanceSellerAfter + " balanceOwnerBefore: " + balanceOwnerBefore + " balanceOwnerAfter: " + balanceOwnerAfter + " balanceListFeeBefore: " + balanceListFeeBefore + " balanceListFeeAfter: " + balanceListFeeAfter + " balanceBuyFeeBefore: " + balanceBuyFeeBefore + " balanceBuyFeeAfter: " + balanceBuyFeeAfter);
    }
}
