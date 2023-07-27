using System;
using System.Linq;
using System.Numerics;
using Xunit;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Business.Blockchain.Tokens.Structs;
using Phantasma.Core.Cryptography.Enums;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Token;
using Phantasma.Core.Domain.Token.Enums;
using Phantasma.Core.Domain.TransactionData;
using Phantasma.Core.Types.Structs;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Domain.VM.Enums;

namespace Phantasma.Business.Tests.Blockchain;

[Collection(nameof(SystemTestCollectionDefinition))]
public class NFTTests
{
    PhantasmaKeys user;
    PhantasmaKeys owner;
    Nexus nexus;
    NexusSimulator simulator;
    int amountRequested;
    int gas;
    BigInteger initialAmount;
    BigInteger initialFuel;
    BigInteger startBalance;
    BigInteger currentSupply;

    public NFTTests()
    {
        Initialize();
    }

    public void Initialize()
    {
        user = PhantasmaKeys.Generate();
        owner = PhantasmaKeys.Generate();
        amountRequested = 100000000;
        gas = 99999;
        initialAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);
        initialFuel = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
        InitializeSimulator();

        startBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, user.Address);
    }
        
    protected void InitializeSimulator()
    {
        simulator = new NexusSimulator(owner);
        nexus = simulator.Nexus;
        nexus.SetOracleReader(new OracleSimulator(nexus));
        currentSupply = nexus.RootChain.GetTokenSupply(nexus.RootStorage, DomainSettings.StakingTokenSymbol);
        simulator.GetFundsInTheFuture(owner, 1);
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
    public void NftMint()
    {
        var chain = nexus.RootChain;

        var symbol = "COOL";

        var testUser = PhantasmaKeys.Generate();

        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Transferable);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
        Assert.True(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the user pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.True(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

        var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        // Mint a new CoolToken to test address
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        // obtain tokenID
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");

        // verify nft presence on the user post-mint
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");

        // check used storage
        var tokenAddress = TokenUtils.GetContractAddress(symbol);
        var usedStorage = (int)simulator.InvokeContract("storage", nameof(StorageContract.GetUsedSpace), tokenAddress).AsNumber();
        var minExpectedSize = tokenROM.Length + tokenRAM.Length;
        Assert.True(usedStorage >= minExpectedSize);

        //verify that the present nft is the same we actually tried to create
        var tokenID = ownedTokenList.First();
        var nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenID);
        Assert.True(nft.ROM.SequenceEqual(tokenROM) && nft.RAM.SequenceEqual(tokenRAM),
            "And why is this NFT different than expected? Not the same data");

        var currentSupply = chain.GetTokenSupply(chain.Storage, symbol);
        Assert.True(currentSupply == 1, "why supply did not increase?");

        var testScript = new ScriptBuilder().CallNFT(symbol, 0, "getName", tokenID).EndScript();
        var temp = simulator.InvokeScript(testScript);
        var testResult = temp.AsString();
        Assert.True(testResult == "CoolToken");
    }

    [Fact]
    public void NftBurn()
    {
        var chain = nexus.RootChain;

        var symbol = "COOL";

        BigInteger seriesID = 123;

        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Burnable, null, null, null, (uint)seriesID);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var series = nexus.GetTokenSeries(nexus.RootStorage, symbol, seriesID);

        Assert.True(series.MintCount == 0, "nothing should be minted yet");

        // Send some KCAL and SOUL to the test user (required for gas used in "burn" transaction)
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, user.Address, chain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals));
        simulator.GenerateTransfer(owner, user.Address, chain, DomainSettings.StakingTokenSymbol, UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals));
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
        Assert.True(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the user pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(!ownedTokenList.Any(), "How does the user already have a CoolToken?");

        var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        // Mint a new CoolToken to test address
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, user.Address, symbol, tokenROM, tokenRAM, seriesID);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        // obtain tokenID
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        var tokenID = ownedTokenList.First();

        // verify nft presence on the user post-mint
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the user not have one now?");

        var ownerAddress = ownerships.GetOwner(chain.Storage, tokenID);
        Assert.True(ownerAddress == user.Address);

        //verify that the present nft is the same we actually tried to create
        var tokenId = ownedTokenList.ElementAt(0);
        var nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenId);
        Assert.True(nft.ROM.SequenceEqual(tokenROM) || nft.RAM.SequenceEqual(tokenRAM),
            "And why is this NFT different than expected? Not the same data");

        Assert.True(nft.Infusion.Length == 0); // nothing should be infused yet

        var infuseSymbol = DomainSettings.StakingTokenSymbol;
        var infuseAmount = UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals);

        var prevBalance = nexus.RootChain.GetTokenBalance(nexus.RootChain.Storage, infuseSymbol, user.Address);

        // Infuse some KCAL to the CoolToken
        var prevInfused = nexus.RootChain.GetTokenBalance(nexus.RootChain.Storage, infuseSymbol, DomainSettings.InfusionAddress);
        
        simulator.BeginBlock();
        simulator.InfuseNonFungibleToken(user, symbol, tokenId, infuseSymbol, infuseAmount);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenId);
        Assert.True(nft.Infusion.Length == 1); // should have something infused now

        var infusedBalance = nexus.RootChain.GetTokenBalance(nexus.RootChain.Storage, infuseSymbol, DomainSettings.InfusionAddress);
        infuseAmount += prevInfused; // Inflation per year
        Assert.Equal(infusedBalance, infuseAmount); // should match

        var curBalance = nexus.RootChain.GetTokenBalance(nexus.RootChain.Storage, infuseSymbol, user.Address);
        Assert.Equal(curBalance + infuseAmount - prevInfused, prevBalance); // should match

        prevBalance = curBalance;

        // burn the token
        simulator.BeginBlock();
        simulator.GenerateNftBurn(user, chain, symbol, tokenId);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        //verify the user no longer has the token
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(!ownedTokenList.Any(), "How does the user still have it post-burn?");

        // verify that the user received the infused assets
        curBalance = nexus.RootChain.GetTokenBalance(nexus.RootChain.Storage, infuseSymbol, user.Address);
        Assert.Equal(curBalance, prevBalance + infusedBalance - prevInfused); // should match

        var burnedSupply = nexus.GetBurnedTokenSupply(nexus.RootStorage, symbol);
        Assert.Equal(burnedSupply, 1);

        var burnedSeriesSupply = nexus.GetBurnedTokenSupplyForSeries(nexus.RootStorage, symbol, seriesID);
        Assert.Equal(burnedSeriesSupply, 1);
    }

    [Fact]
    public void NftTransfer()
    {
        var chain = nexus.RootChain;

        var nftKey = PhantasmaKeys.Generate();
        var symbol = "COOL";
        var nftName = "CoolToken";

        var sender = PhantasmaKeys.Generate();
        var receiver = PhantasmaKeys.Generate();

        // Send some SOUL to the test user (required for gas used in "transfer" transaction)
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, sender.Address, chain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(1, DomainSettings.FuelTokenDecimals));
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        simulator.GenerateToken(owner, symbol, nftName, 0, 0, TokenFlags.Transferable);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
        Assert.True(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the sender pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownedTokenList = ownerships.Get(chain.Storage, sender.Address);
        Assert.True(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

        var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        // Mint a new CoolToken 
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, sender.Address, symbol, tokenROM, tokenRAM, 0);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        // obtain tokenID
        ownedTokenList = ownerships.Get(chain.Storage, sender.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        var tokenID = ownedTokenList.First();

        // verify nft presence on the sender post-mint
        ownedTokenList = ownerships.Get(chain.Storage, sender.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");

        //verify that the present nft is the same we actually tried to create
        var tokenId = ownedTokenList.ElementAt(0);
        var nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenId);
        Assert.True(nft.ROM.SequenceEqual(tokenROM) || nft.RAM.SequenceEqual(tokenRAM),
            "And why is this NFT different than expected? Not the same data");

        // verify nft presence on the receiver pre-transfer
        ownedTokenList = ownerships.Get(chain.Storage, receiver.Address);
        Assert.True(!ownedTokenList.Any(), "How does the receiver already have a CoolToken?");

        // transfer that nft from sender to receiver
        simulator.BeginBlock();
        var txA = simulator.GenerateNftTransfer(sender, receiver.Address, chain, symbol, tokenId);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful(), simulator.FailedTxReason);

        // verify nft presence on the receiver post-transfer
        ownedTokenList = ownerships.Get(chain.Storage, receiver.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the receiver not have one now?");

        //verify that the transfered nft is the same we actually tried to create
        tokenId = ownedTokenList.ElementAt(0);
        nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenId);
        Assert.True(nft.ROM.SequenceEqual(tokenROM) || nft.RAM.SequenceEqual(tokenRAM),
            "And why is this NFT different than expected? Not the same data");
    }

    [Fact]
    public void NftMassMint()
    {
        var chain = nexus.RootChain;

        var symbol = "COOL";

        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Transferable);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var tokenAddress = TokenUtils.GetContractAddress(symbol);
        var storageStakeAmount = UnitConversion.ToBigInteger(100000, DomainSettings.StakingTokenDecimals);

        // Add some storage to the NFT contract
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, tokenAddress, chain, DomainSettings.StakingTokenSymbol, storageStakeAmount);

        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().
                AllowGas(owner.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit).
                CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), tokenAddress, storageStakeAmount).
                SpendGas(owner.Address).
                EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        Assert.True(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the user pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(!ownedTokenList.Any(), "How does the sender already have nfts?");

        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        var nftCount = 1000;

        var initialKCAL = nexus.RootChain.GetTokenBalance(nexus.RootStorage, DomainSettings.FuelTokenSymbol, owner.Address);

        // Mint several nfts to test limit per tx
        simulator.BeginBlock();
        for (int i = 1; i <= nftCount; i++)
        {
            var tokenROM = BitConverter.GetBytes(i);
            simulator.MintNonFungibleToken(owner, user.Address, symbol, tokenROM, tokenRAM, 0);
        }
        var block = simulator.EndBlock().First();
        Assert.True(simulator.LastBlockWasSuccessful());

        Assert.True(block.TransactionCount == nftCount);

        // obtain tokenID
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        var ownedTotal = ownedTokenList.Count();
        Assert.True(ownedTotal == nftCount);

        var currentKCAL = nexus.RootChain.GetTokenBalance(nexus.RootStorage, DomainSettings.FuelTokenSymbol, owner.Address);

        var fee = initialKCAL - currentKCAL;

        var convertedFee = UnitConversion.ToDecimal(fee, DomainSettings.FuelTokenDecimals);

        Assert.True(fee > 0);
    }

    [Fact(Skip = "side chain transfers of NFTs do currently not work, because Storage contract is not deployed on the side chain.")]
    public void SidechainNftTransfer()
    {
        var sourceChain = nexus.RootChain;

        var symbol = "COOL";

        var sender = PhantasmaKeys.Generate();
        var receiver = PhantasmaKeys.Generate();

        var fullAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
        var smallAmount = fullAmount / 2;
        Assert.True(smallAmount > 0);

        // Send some SOUL to the test user (required for gas used in "transfer" transaction)
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, sender.Address, sourceChain, DomainSettings.FuelTokenSymbol, fullAmount);
        simulator.GenerateChain(owner, DomainSettings.ValidatorsOrganizationName, "main", "test");
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var targetChain = nexus.GetChainByName("test");

        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Transferable);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
        Assert.True(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the sender pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownedTokenList = ownerships.Get(sourceChain.Storage, sender.Address);
        Assert.True(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

        var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        // Mint a new CoolToken 
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, sender.Address, symbol, tokenROM, tokenRAM, 0);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        // obtain tokenID
        ownedTokenList = ownerships.Get(sourceChain.Storage, sender.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        var tokenID = ownedTokenList.First();

        // verify nft presence on the sender post-mint
        ownedTokenList = ownerships.Get(sourceChain.Storage, sender.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");

        //verify that the present nft is the same we actually tried to create
        var tokenId = ownedTokenList.ElementAt(0);
        var nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenId);
        Assert.True(nft.ROM.SequenceEqual(tokenROM) || nft.RAM.SequenceEqual(tokenRAM),
            "And why is this NFT different than expected? Not the same data");

        // verify nft presence on the receiver pre-transfer
        ownedTokenList = ownerships.Get(targetChain.Storage, receiver.Address);
        Assert.True(!ownedTokenList.Any(), "How does the receiver already have a CoolToken?");

        var extraFee = UnitConversion.ToBigInteger(0.001m, DomainSettings.FuelTokenDecimals);

        // transfer that nft from sender to receiver
        simulator.BeginBlock();
        var txA = simulator.GenerateSideChainSend(sender, symbol, sourceChain, receiver.Address, targetChain, tokenId, extraFee);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var blockAHash = nexus.RootChain.GetLastBlockHash();
        var blockA = nexus.RootChain.GetBlockByHash(blockAHash);

        Console.WriteLine("step 1");
        // finish the chain transfer
        simulator.BeginBlock();
        simulator.GenerateSideChainSettlement(receiver, nexus.RootChain, targetChain, txA);
        Assert.True(simulator.EndBlock().Any());

        // verify the sender no longer has it
        ownedTokenList = ownerships.Get(sourceChain.Storage, sender.Address);
        Assert.True(!ownedTokenList.Any(), "How does the sender still have one?");

        // verify nft presence on the receiver post-transfer
        ownedTokenList = ownerships.Get(targetChain.Storage, receiver.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the receiver not have one now?");

        //verify that the transfered nft is the same we actually tried to create
        tokenId = ownedTokenList.ElementAt(0);
        nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenId);
        Assert.True(nft.ROM.SequenceEqual(tokenROM) || nft.RAM.SequenceEqual(tokenRAM),
            "And why is this NFT different than expected? Not the same data");
    }

    [Fact]
    public void NftInfuse()
    {
        var chain = nexus.RootChain;
        
        simulator.GetFundsInTheFuture(owner);

        var symbol = "COOL";
        var symbol2 = "NCOL";

        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Transferable | TokenFlags.Burnable);
        simulator.GenerateTransfer(owner, user.Address, chain, DomainSettings.StakingTokenSymbol, UnitConversion.ToBigInteger(100, DomainSettings.StakingTokenDecimals));
        simulator.GenerateTransfer(owner, user.Address, chain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals));
        simulator.GenerateToken(owner, symbol2, "CoolToken-Item", 0, 0, TokenFlags.Transferable);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        

        var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
        Assert.True(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the user pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownershipsNCOL = new OwnershipSheet(symbol2);
        var ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        var ownedTokenListNCOL = ownershipsNCOL.Get(chain.Storage, user.Address);
        Assert.True(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

        var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        var tokenROM2 = new byte[] { 0x2, 0x3, 0x6, 0x7 };
        var tokenRAM2 = new byte[] { 0x2, 0x5, 0x7, 0x6 };
        
        // Mint a new CoolToken to test address
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, user.Address, symbol, tokenROM, tokenRAM, 0);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        // obtain tokenID
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");

        // verify nft presence on the user post-mint
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");

        // check used storage
        var tokenAddress = TokenUtils.GetContractAddress(symbol);
        var usedStorage = (int)simulator.InvokeContract("storage", nameof(StorageContract.GetUsedSpace), tokenAddress).AsNumber();
        var minExpectedSize = tokenROM.Length + tokenRAM.Length;
        Assert.True(usedStorage >= minExpectedSize, $"Used storage is less than expected, expected {minExpectedSize}, got {usedStorage}");

        //verify that the present nft is the same we actually tried to create
        var tokenID = ownedTokenList.First();
        var nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenID);
        Assert.True(nft.ROM.SequenceEqual(tokenROM) && nft.RAM.SequenceEqual(tokenRAM),
            "And why is this NFT different than expected? Not the same data");

        var currentSupply = chain.GetTokenSupply(chain.Storage, symbol);
        Assert.True(currentSupply == 1, "why supply did not increase?");

        var testScript = new ScriptBuilder().CallNFT(symbol, 0, "getName", tokenID).EndScript();
        var temp = simulator.InvokeScript(testScript);
        var testResult = temp.AsString();
        Assert.True(testResult == "CoolToken");
        
        // Infuse the NFT with an Tokens
        simulator.BeginBlock();
        simulator.InfuseNonFungibleToken(user, symbol, tokenID, "SOUL", UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals));
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        // Verify NFT
        ownedTokenList = ownerships.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        
        var nftInfused = nexus.ReadNFT(nexus.RootStorage, symbol, tokenID);
        Assert.True(nftInfused.Infusion.Length == 1, "nftInfused.Infusion.Length != 1");
        
        
        Assert.True(nftInfused.Infusion[0].Symbol == DomainSettings.StakingTokenSymbol);
        Assert.True(nftInfused.Infusion[0].Value == UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals));
        
        // Infuse the NFT with an NFT
        
        // Mint a new NCOL to test address
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, user.Address, symbol2, tokenROM2, tokenRAM2, 0);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        // Verify NFT is minted
        ownedTokenListNCOL = ownershipsNCOL.Get(chain.Storage, user.Address);
        Assert.True(ownedTokenListNCOL.Count() == 1, "How does the sender not have one now?");
        
        var tokenIDAfter = ownedTokenListNCOL.First();
        var nftCreated = nexus.ReadNFT(nexus.RootStorage, symbol2, tokenIDAfter);
        Assert.True(nftCreated.ROM.SequenceEqual(tokenROM2) && nftCreated.RAM.SequenceEqual(tokenRAM2),
            "And why is this NFT different than expected? Not the same data");
        Assert.True(tokenIDAfter != tokenID);
        
        
        // Infuse NFT
        simulator.BeginBlock();
        simulator.InfuseNonFungibleToken(user, symbol, tokenID, symbol2, tokenIDAfter);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        // Verify Infusion
        var nftInfusedAfter = nexus.ReadNFT(nexus.RootStorage, symbol, tokenID);
        Assert.True(nftInfusedAfter.Infusion.Length == 2, "nftInfused.Infusion.Length != 2");

        var expectedAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);

        Assert.True(nftInfusedAfter.Infusion[0].Symbol == DomainSettings.StakingTokenSymbol);
        Assert.True(nftInfusedAfter.Infusion[0].Value == expectedAmount);
        Assert.True(nftInfusedAfter.Infusion[1].Symbol == symbol2);
        Assert.True(nftInfusedAfter.Infusion[1].Value == tokenIDAfter);

        {
            // test if extcalls ReadToken return infusion list
            var fieldKey = "infusion";
            var script = new ScriptBuilder().CallInterop("Runtime.ReadToken", symbol, tokenID, fieldKey).EndScript();

            var scriptResult = nexus.RootChain.InvokeScript(nexus.RootStorage, script, Timestamp.Now);
            var infusionField = scriptResult.GetField(fieldKey);
            var infusionArray = infusionField.ToArray<VMObject>();
            Assert.True(infusionArray.Length == 2);

            var firstElement = VMObject.CastTo(infusionArray[0], VMType.Struct);
            var firstSymbol = firstElement.GetField("Symbol").AsString();
            Assert.True(firstSymbol == DomainSettings.StakingTokenSymbol);
            var firstAmount = firstElement.GetField("Value").AsNumber();
            Assert.True(firstAmount == expectedAmount);

            var secondElement = VMObject.CastTo(infusionArray[1], VMType.Struct);
            var secondSymbol = secondElement.GetField("Symbol").AsString();
            Assert.True(secondSymbol == symbol2);
            var secondID = secondElement.GetField("Value").AsNumber();
            Assert.True(secondID == tokenIDAfter);
        }

        {
            // test if extcalls ReadInfusions return infusion list
            var script = new ScriptBuilder().CallInterop("Runtime.ReadInfusions", symbol, tokenID).EndScript();

            var scriptResult = nexus.RootChain.InvokeScript(nexus.RootStorage, script, Timestamp.Now);
            var infusionArray = scriptResult.ToArray<VMObject>();
            Assert.True(infusionArray.Length == 2);

            var firstElement = VMObject.CastTo(infusionArray[0], VMType.Struct);
            var firstSymbol = firstElement.GetField("Symbol").AsString();
            Assert.True(firstSymbol == DomainSettings.StakingTokenSymbol);
            var firstAmount = firstElement.GetField("Value").AsNumber();
            Assert.True(firstAmount == expectedAmount);

            var secondElement = VMObject.CastTo(infusionArray[1], VMType.Struct);
            var secondSymbol = secondElement.GetField("Symbol").AsString();
            Assert.True(secondSymbol == symbol2);
            var secondID = secondElement.GetField("Value").AsNumber();
            Assert.True(secondID == tokenIDAfter);
        }
    }
}
