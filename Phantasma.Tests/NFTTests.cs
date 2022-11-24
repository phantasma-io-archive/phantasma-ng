using System;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Simulator;

namespace Phantasma.LegacyTests;

[TestClass]
public class NFTTests
{
    [TestMethod]
    public void NftMint()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var chain = nexus.RootChain;

        var symbol = "COOL";

        var testUser = PhantasmaKeys.Generate();

        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Transferable);
        simulator.EndBlock();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());

        var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
        Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the user pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

        var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        // Mint a new CoolToken to test address
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
        simulator.EndBlock();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());

        // obtain tokenID
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");

        // verify nft presence on the user post-mint
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");

        // check used storage
        var tokenAddress = TokenUtils.GetContractAddress(symbol);
        var usedStorage = (int)simulator.InvokeContract("storage", nameof(StorageContract.GetUsedSpace), tokenAddress).AsNumber();
        var minExpectedSize = tokenROM.Length + tokenRAM.Length;
        Assert.IsTrue(usedStorage >= minExpectedSize);

        //verify that the present nft is the same we actually tried to create
        var tokenID = ownedTokenList.First();
        var nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenID);
        Assert.IsTrue(nft.ROM.SequenceEqual(tokenROM) && nft.RAM.SequenceEqual(tokenRAM),
            "And why is this NFT different than expected? Not the same data");

        var currentSupply = chain.GetTokenSupply(chain.Storage, symbol);
        Assert.IsTrue(currentSupply == 1, "why supply did not increase?");

        var testScript = new ScriptBuilder().CallNFT(symbol, 0, "getName", tokenID).EndScript();
        var temp = simulator.InvokeScript(testScript);
        var testResult = temp.AsString();
        Assert.IsTrue(testResult == "CoolToken");
    }

    [TestMethod]
    public void NftBurn()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var chain = nexus.RootChain;

        var symbol = "COOL";

        var testUser = PhantasmaKeys.Generate();

        BigInteger seriesID = 123;

        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Burnable, null, null, null, (uint)seriesID);
        simulator.EndBlock();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());

        var series = nexus.GetTokenSeries(nexus.RootStorage, symbol, seriesID);

        Assert.IsTrue(series.MintCount == 0, "nothing should be minted yet");

        // Send some KCAL and SOUL to the test user (required for gas used in "burn" transaction)
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, chain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(1, DomainSettings.FuelTokenDecimals));
        simulator.GenerateTransfer(owner, testUser.Address, chain, DomainSettings.StakingTokenSymbol, UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals));
        simulator.EndBlock();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());

        var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
        Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the user pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.IsTrue(!ownedTokenList.Any(), "How does the user already have a CoolToken?");

        var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        // Mint a new CoolToken to test address
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, seriesID);
        simulator.EndBlock();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());

        // obtain tokenID
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        var tokenID = ownedTokenList.First();

        // verify nft presence on the user post-mint
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.IsTrue(ownedTokenList.Count() == 1, "How does the user not have one now?");

        var ownerAddress = ownerships.GetOwner(chain.Storage, tokenID);
        Assert.IsTrue(ownerAddress == testUser.Address);

        //verify that the present nft is the same we actually tried to create
        var tokenId = ownedTokenList.ElementAt(0);
        var nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenId);
        Assert.IsTrue(nft.ROM.SequenceEqual(tokenROM) || nft.RAM.SequenceEqual(tokenRAM),
            "And why is this NFT different than expected? Not the same data");

        Assert.IsTrue(nft.Infusion.Length == 0); // nothing should be infused yet

        var infuseSymbol = DomainSettings.StakingTokenSymbol;
        var infuseAmount = UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals);

        var prevBalance = nexus.RootChain.GetTokenBalance(nexus.RootChain.Storage, infuseSymbol, testUser.Address);

        // Infuse some KCAL to the CoolToken
        simulator.BeginBlock();
        simulator.InfuseNonFungibleToken(testUser, symbol, tokenId, infuseSymbol, infuseAmount);
        simulator.EndBlock();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());

        nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenId);
        Assert.IsTrue(nft.Infusion.Length == 1); // should have something infused now

        var infusedBalance = nexus.RootChain.GetTokenBalance(nexus.RootChain.Storage, infuseSymbol, DomainSettings.InfusionAddress);
        Assert.IsTrue(infusedBalance == infuseAmount); // should match

        var curBalance = nexus.RootChain.GetTokenBalance(nexus.RootChain.Storage, infuseSymbol, testUser.Address);
        Assert.IsTrue(curBalance + infusedBalance == prevBalance); // should match

        prevBalance = curBalance;

        // burn the token
        simulator.BeginBlock();
        simulator.GenerateNftBurn(testUser, chain, symbol, tokenId);
        simulator.EndBlock();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());

        //verify the user no longer has the token
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.IsTrue(!ownedTokenList.Any(), "How does the user still have it post-burn?");

        // verify that the user received the infused assets
        curBalance = nexus.RootChain.GetTokenBalance(nexus.RootChain.Storage, infuseSymbol, testUser.Address);
        Assert.IsTrue(curBalance == prevBalance + infusedBalance); // should match

        var burnedSupply = nexus.GetBurnedTokenSupply(nexus.RootStorage, symbol);
        Assert.IsTrue(burnedSupply == 1);

        var burnedSeriesSupply = nexus.GetBurnedTokenSupplyForSeries(nexus.RootStorage, symbol, seriesID);
        Assert.IsTrue(burnedSeriesSupply == 1);
    }

    [TestMethod]
    public void NftTransfer()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

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
        Assert.IsTrue(simulator.LastBlockWasSuccessful());

        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        simulator.GenerateToken(owner, symbol, nftName, 0, 0, TokenFlags.Transferable);
        simulator.EndBlock();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());

        var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
        Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the sender pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownedTokenList = ownerships.Get(chain.Storage, sender.Address);
        Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

        var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        // Mint a new CoolToken 
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, sender.Address, symbol, tokenROM, tokenRAM, 0);
        simulator.EndBlock();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());

        // obtain tokenID
        ownedTokenList = ownerships.Get(chain.Storage, sender.Address);
        Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        var tokenID = ownedTokenList.First();

        // verify nft presence on the sender post-mint
        ownedTokenList = ownerships.Get(chain.Storage, sender.Address);
        Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");

        //verify that the present nft is the same we actually tried to create
        var tokenId = ownedTokenList.ElementAt(0);
        var nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenId);
        Assert.IsTrue(nft.ROM.SequenceEqual(tokenROM) || nft.RAM.SequenceEqual(tokenRAM),
            "And why is this NFT different than expected? Not the same data");

        // verify nft presence on the receiver pre-transfer
        ownedTokenList = ownerships.Get(chain.Storage, receiver.Address);
        Assert.IsTrue(!ownedTokenList.Any(), "How does the receiver already have a CoolToken?");

        // transfer that nft from sender to receiver
        simulator.BeginBlock();
        var txA = simulator.GenerateNftTransfer(sender, receiver.Address, chain, symbol, tokenId);
        simulator.EndBlock();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());

        // verify nft presence on the receiver post-transfer
        ownedTokenList = ownerships.Get(chain.Storage, receiver.Address);
        Assert.IsTrue(ownedTokenList.Count() == 1, "How does the receiver not have one now?");

        //verify that the transfered nft is the same we actually tried to create
        tokenId = ownedTokenList.ElementAt(0);
        nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenId);
        Assert.IsTrue(nft.ROM.SequenceEqual(tokenROM) || nft.RAM.SequenceEqual(tokenRAM),
            "And why is this NFT different than expected? Not the same data");
    }

    [TestMethod]
    public void NftMassMint()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var chain = nexus.RootChain;

        var symbol = "COOL";

        var testUser = PhantasmaKeys.Generate();

        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Transferable);
        simulator.EndBlock();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());

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
        Assert.IsTrue(simulator.LastBlockWasSuccessful());

        Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the user pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have nfts?");

        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        var nftCount = 1000;

        var initialKCAL = nexus.RootChain.GetTokenBalance(nexus.RootStorage, DomainSettings.FuelTokenSymbol, owner.Address);

        // Mint several nfts to test limit per tx
        simulator.BeginBlock();
        for (int i = 1; i <= nftCount; i++)
        {
            var tokenROM = BitConverter.GetBytes(i);
            simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
        }
        var block = simulator.EndBlock().First();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());

        Assert.IsTrue(block.TransactionCount == nftCount);

        // obtain tokenID
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        var ownedTotal = ownedTokenList.Count();
        Assert.IsTrue(ownedTotal == nftCount);

        var currentKCAL = nexus.RootChain.GetTokenBalance(nexus.RootStorage, DomainSettings.FuelTokenSymbol, owner.Address);

        var fee = initialKCAL - currentKCAL;

        var convertedFee = UnitConversion.ToDecimal(fee, DomainSettings.FuelTokenDecimals);

        Assert.IsTrue(fee > 0);
    }

    [TestMethod]
    [Ignore] //TODO side chain transfers of NFTs do currently not work, because Storage contract is not deployed on the side chain.
    public void SidechainNftTransfer()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var sourceChain = nexus.RootChain;

        var symbol = "COOL";

        var sender = PhantasmaKeys.Generate();
        var receiver = PhantasmaKeys.Generate();

        var fullAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
        var smallAmount = fullAmount / 2;
        Assert.IsTrue(smallAmount > 0);

        // Send some SOUL to the test user (required for gas used in "transfer" transaction)
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, sender.Address, sourceChain, DomainSettings.FuelTokenSymbol, fullAmount);
        simulator.GenerateChain(owner, DomainSettings.ValidatorsOrganizationName, "main", "test");
        simulator.EndBlock();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());

        var targetChain = nexus.GetChainByName("test");

        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Transferable);
        simulator.EndBlock();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());

        var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
        Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the sender pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownedTokenList = ownerships.Get(sourceChain.Storage, sender.Address);
        Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

        var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        // Mint a new CoolToken 
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, sender.Address, symbol, tokenROM, tokenRAM, 0);
        simulator.EndBlock();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());

        // obtain tokenID
        ownedTokenList = ownerships.Get(sourceChain.Storage, sender.Address);
        Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        var tokenID = ownedTokenList.First();

        // verify nft presence on the sender post-mint
        ownedTokenList = ownerships.Get(sourceChain.Storage, sender.Address);
        Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");

        //verify that the present nft is the same we actually tried to create
        var tokenId = ownedTokenList.ElementAt(0);
        var nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenId);
        Assert.IsTrue(nft.ROM.SequenceEqual(tokenROM) || nft.RAM.SequenceEqual(tokenRAM),
            "And why is this NFT different than expected? Not the same data");

        // verify nft presence on the receiver pre-transfer
        ownedTokenList = ownerships.Get(targetChain.Storage, receiver.Address);
        Assert.IsTrue(!ownedTokenList.Any(), "How does the receiver already have a CoolToken?");

        var extraFee = UnitConversion.ToBigInteger(0.001m, DomainSettings.FuelTokenDecimals);

        // transfer that nft from sender to receiver
        simulator.BeginBlock();
        var txA = simulator.GenerateSideChainSend(sender, symbol, sourceChain, receiver.Address, targetChain, tokenId, extraFee);
        simulator.EndBlock();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());

        var blockAHash = nexus.RootChain.GetLastBlockHash();
        var blockA = nexus.RootChain.GetBlockByHash(blockAHash);

        Console.WriteLine("step 1");
        // finish the chain transfer
        simulator.BeginBlock();
        simulator.GenerateSideChainSettlement(receiver, nexus.RootChain, targetChain, txA);
        Assert.IsTrue(simulator.EndBlock().Any());

        // verify the sender no longer has it
        ownedTokenList = ownerships.Get(sourceChain.Storage, sender.Address);
        Assert.IsTrue(!ownedTokenList.Any(), "How does the sender still have one?");

        // verify nft presence on the receiver post-transfer
        ownedTokenList = ownerships.Get(targetChain.Storage, receiver.Address);
        Assert.IsTrue(ownedTokenList.Count() == 1, "How does the receiver not have one now?");

        //verify that the transfered nft is the same we actually tried to create
        tokenId = ownedTokenList.ElementAt(0);
        nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenId);
        Assert.IsTrue(nft.ROM.SequenceEqual(tokenROM) || nft.RAM.SequenceEqual(tokenRAM),
            "And why is this NFT different than expected? Not the same data");
    }

    [TestMethod]
    public void NftInfuse()
    {
        //TODO: Implement
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var chain = nexus.RootChain;
        
        simulator.GetFundsInTheFuture(owner);

        var symbol = "COOL";
        var symbol2 = "NCOL";

        var testUser = PhantasmaKeys.Generate();

        // Create the token CoolToken as an NFT
        simulator.BeginBlock();
        simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Transferable | TokenFlags.Burnable);
        simulator.GenerateTransfer(owner, testUser.Address, chain, DomainSettings.StakingTokenSymbol, UnitConversion.ToBigInteger(100, DomainSettings.StakingTokenDecimals));
        simulator.GenerateTransfer(owner, testUser.Address, chain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals));
        simulator.GenerateToken(owner, symbol2, "CoolToken-Item", 0, 0, TokenFlags.Transferable);
        simulator.EndBlock();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());
        

        var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
        Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

        // verify nft presence on the user pre-mint
        var ownerships = new OwnershipSheet(symbol);
        var ownershipsNCOL = new OwnershipSheet(symbol2);
        var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        var ownedTokenListNCOL = ownershipsNCOL.Get(chain.Storage, testUser.Address);
        Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

        var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
        var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

        var tokenROM2 = new byte[] { 0x2, 0x3, 0x6, 0x7 };
        var tokenRAM2 = new byte[] { 0x2, 0x5, 0x7, 0x6 };
        
        // Mint a new CoolToken to test address
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
        simulator.EndBlock();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());

        // obtain tokenID
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");

        // verify nft presence on the user post-mint
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");

        // check used storage
        var tokenAddress = TokenUtils.GetContractAddress(symbol);
        var usedStorage = (int)simulator.InvokeContract("storage", nameof(StorageContract.GetUsedSpace), tokenAddress).AsNumber();
        var minExpectedSize = tokenROM.Length + tokenRAM.Length;
        Assert.IsTrue(usedStorage >= minExpectedSize);

        //verify that the present nft is the same we actually tried to create
        var tokenID = ownedTokenList.First();
        var nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenID);
        Assert.IsTrue(nft.ROM.SequenceEqual(tokenROM) && nft.RAM.SequenceEqual(tokenRAM),
            "And why is this NFT different than expected? Not the same data");

        var currentSupply = chain.GetTokenSupply(chain.Storage, symbol);
        Assert.IsTrue(currentSupply == 1, "why supply did not increase?");

        var testScript = new ScriptBuilder().CallNFT(symbol, 0, "getName", tokenID).EndScript();
        var temp = simulator.InvokeScript(testScript);
        var testResult = temp.AsString();
        Assert.IsTrue(testResult == "CoolToken");
        
        // Infuse the NFT with an Tokens
        simulator.BeginBlock();
        simulator.InfuseNonFungibleToken(testUser, symbol, tokenID, "SOUL", UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals));
        simulator.EndBlock();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());
        
        // Verify NFT
        ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
        Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
        
        var nftInfused = nexus.ReadNFT(nexus.RootStorage, symbol, tokenID);
        Assert.IsTrue(nftInfused.Infusion.Length == 1, "nftInfused.Infusion.Length != 1");
        
        
        Assert.IsTrue(nftInfused.Infusion[0].Symbol == DomainSettings.StakingTokenSymbol);
        Assert.IsTrue(nftInfused.Infusion[0].Value == UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals));
        
        // Infuse the NFT with an NFT
        
        // Mint a new NCOL to test address
        simulator.BeginBlock();
        simulator.MintNonFungibleToken(owner, testUser.Address, symbol2, tokenROM2, tokenRAM2, 0);
        simulator.EndBlock();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());
        
        // Verify NFT is minted
        ownedTokenListNCOL = ownershipsNCOL.Get(chain.Storage, testUser.Address);
        Assert.IsTrue(ownedTokenListNCOL.Count() == 1, "How does the sender not have one now?");
        
        var tokenIDAfter = ownedTokenListNCOL.First();
        var nftCreated = nexus.ReadNFT(nexus.RootStorage, symbol2, tokenIDAfter);
        Assert.IsTrue(nftCreated.ROM.SequenceEqual(tokenROM2) && nftCreated.RAM.SequenceEqual(tokenRAM2),
            "And why is this NFT different than expected? Not the same data");
        Assert.IsTrue(tokenIDAfter != tokenID);
        
        
        // Infuse NFT
        simulator.BeginBlock();
        simulator.InfuseNonFungibleToken(testUser, symbol, tokenID, symbol2, tokenIDAfter);
        simulator.EndBlock();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());
        
        // Verify Infusion
        var nftInfusedAfter = nexus.ReadNFT(nexus.RootStorage, symbol, tokenID);
        Assert.IsTrue(nftInfusedAfter.Infusion.Length == 2, "nftInfused.Infusion.Length != 2");

        Assert.IsTrue(nftInfusedAfter.Infusion[0].Symbol == DomainSettings.StakingTokenSymbol);
        Assert.IsTrue(nftInfusedAfter.Infusion[0].Value == UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals));
        Assert.IsTrue(nftInfusedAfter.Infusion[1].Symbol == symbol2);
        Assert.IsTrue(nftInfusedAfter.Infusion[1].Value == tokenIDAfter);
    }
}
