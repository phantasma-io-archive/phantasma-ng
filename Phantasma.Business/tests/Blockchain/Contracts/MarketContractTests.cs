using NSubstitute;
using Phantasma.Business.Blockchain;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;
using Phantasma.Infrastructure.RocksDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Moq;
using Phantasma.Business.VM;
using Shouldly;
using Xunit;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Business.Blockchain.VM;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Market;
using Phantasma.Core.Domain.Execution;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Token;
using Phantasma.Core.Domain.TransactionData;
using Phantasma.Core.Domain.VM;

namespace Phantasma.Business.Tests.Blockchain.Contracts;

public struct XToken : IToken
{
    public string Name { get; }
    public string Symbol { get; }
    public Address Owner { get; }
    public TokenFlags Flags { get; }
    public BigInteger MaxSupply { get; }
    public int Decimals { get; }
    public byte[] Script { get; }
    public ContractInterface ABI { get; }
}


[Collection(nameof(SystemTestCollectionDefinition))]
public class MarketContractTests : IDisposable
{
    private PhantasmaKeys Validator { get; set; }
    private PhantasmaKeys TokenOwner { get; set; }
    private PhantasmaKeys User1 { get; set; }
    private PhantasmaKeys User2 { get; set; }

    private string PartitionPath { get; set; }
    private IToken FungibleToken { get; set; }
    private IToken NonFungibleToken { get; set; }
    private IToken NonTransferableToken { get; set; }
    private IChain Chain { get; set; }
    private Dictionary<string, BigInteger> Mints { get; set; }
    
    [Fact]
    public void Hack_Steal_The_Funds_success()
    {
        var runtime = CreateRuntime_MockedChain();
        //Address from, string baseSymbol, string quoteSymbol, BigInteger tokenID, BigInteger price, BigInteger endPrice, Timestamp startDate, Timestamp endDate, BigInteger extensionPeriod, BigInteger typeAuction, BigInteger listingFee, Address listingFeeAddress)
        
        runtime.CallNativeContext(NativeContractKind.Market, nameof(MarketContract.ListToken), User1.Address, NonFungibleToken.Symbol, FungibleToken.Symbol, 0, 1, 100, (Timestamp)DateTime.UtcNow, (Timestamp)DateTime.UtcNow.AddDays(1), 1, 2, 1, User2.Address);

        //(Address from, string symbol, BigInteger tokenID, BigInteger price, BigInteger buyingFee, Address buyingFeeAddress)
        Should.Throw<Exception>(() =>
        {
            runtime.CallNativeContext(NativeContractKind.Market, nameof(MarketContract.BidToken), User1.Address,
                NonFungibleToken.Symbol, 0, 50, 1, User2.Address);
        });
        
        Should.Throw<Exception>(() =>
        {
            runtime.CallNativeContext(NativeContractKind.Market, nameof(MarketContract.BidToken), User1.Address,
                NonFungibleToken.Symbol, 0, 99, -99, User2.Address);
        });

        // GetAuction(string symbol, BigInteger tokenID)
        var auction = runtime.CallNativeContext(NativeContractKind.Market, nameof(MarketContract.GetAuction),
            NonFungibleToken.Symbol, 0).AsStruct<MarketAuction>();
        
        auction.Price.ShouldBe(1);
    }

    private IRuntime CreateRuntime_MockedChain()
    {
        var tx = new Transaction(
            "mainnet",
            DomainSettings.RootChainName,
            new byte[1] { 0 },
            Timestamp.Now + TimeSpan.FromDays(300),
            "UnitTest");

        tx.Sign(User1);
        tx.Sign(User2);
        tx.Sign(TokenOwner);
        
        return CreateRuntime(false, FungibleToken, null, tx, true, true);
    }
    
    private IRuntime CreateRuntime(
            bool delayPayment,
            IToken token,
            ExecutionContext context = null,
            Transaction tx = null,
            bool mockChain = false,
            bool tokenExists = true)
    {
        var contract = (SmartContract)Activator.CreateInstance<MarketContract>();
        var nexusMoq = new Mock<INexus>();
        var runtimeMoq = new Mock<IRuntime>();

        // setup TransferTokens
        nexusMoq.Setup( n => n.TransferTokens(
                    It.IsAny<IRuntime>(),
                    It.IsAny<IToken>(),
                    It.IsAny<Address>(),
                    It.IsAny<Address>(),
                    It.IsAny<BigInteger>(),
                    It.IsAny<bool>())
                );

        nexusMoq.Setup( n => n.MintTokens(
                    It.IsAny<IRuntime>(),
                    It.IsAny<IToken>(),
                    It.IsAny<Address>(),
                    It.IsAny<Address>(),
                    It.IsAny<string>(),
                    It.IsAny<BigInteger>())
                )
            .Callback<IRuntime, IToken, Address, Address, string, BigInteger>(
                    (runtime, token, from, to, chain, amount) =>
                    {
                        this.Mints.Add(token.Symbol, amount);
                    });
        nexusMoq.Setup(n => n.ReadNFT(
                It.IsAny<StorageContext>(), 
                It.IsAny<string>(), 
                It.IsAny<BigInteger>()))
            .Returns((StorageContext c, string name, BigInteger id) =>
            {
                if (name == NonFungibleToken.Symbol)
                    return new TokenContent(0,0,"main",User1.Address, User1.Address, new Byte[]{ 0 }, new Byte[]{0}, Timestamp.Now, null, TokenSeriesMode.Unique);
                else
                    return new TokenContent();
            });
        
        nexusMoq.Setup(n => n.ReadNFT(
                It.IsAny<IRuntime>(),
                It.IsAny<string>(), 
                It.IsAny<BigInteger>()))
            .Returns((IRuntime c, string name, BigInteger id) =>
            {
                if (name == NonFungibleToken.Symbol)
                    return new TokenContent(0,0,this.Chain.Name,User1.Address, User1.Address, new Byte[]{ 0 }, new Byte[]{0}, Timestamp.Now, null, TokenSeriesMode.Unique);
                else
                    return new TokenContent();
            });

        nexusMoq.Setup( n => n.HasGenesis()).Returns(true);

        nexusMoq.Setup( n => n.GetStakeFromAddress(
                    It.IsAny<StorageContext>(),
                    It.IsAny<Address>(), It.IsAny<Timestamp>()))
            .Returns(UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals));

        nexusMoq.Setup( n => n.GetTokenInfo(
                    It.IsAny<StorageContext>(),
                    It.IsAny<string>())
                ).Returns((StorageContext sC, string name) =>
        {
            if (name == token.Symbol)
                return token;
            else
                return NonFungibleToken;
        });

        nexusMoq.Setup( n => n.TokenExists(
                    It.IsAny<StorageContext>(),
                    It.IsAny<string>())
                ).Returns(tokenExists);

        nexusMoq.Setup( n => n.GetChainByName(
                    It.IsAny<string>())
                ).Returns((string name) => 
                    {
                        if (string.IsNullOrEmpty(name))
                        {
                            return null;
                        }
                        else
                        {
                            return this.Chain;
                        }
                    });

        nexusMoq.Setup( n => n.GetParentChainByName(
                    It.IsAny<string>())
                ).Returns<IChain>(null);

        nexusMoq.Setup( n => n.GetProtocolVersion(
                    It.IsAny<StorageContext>())
                ).Returns(0);

        nexusMoq.Setup( n => n.CreateOrganization(
                    It.IsAny<StorageContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<byte[]>()));

        if (!mockChain)
        {
            this.Chain = new Chain(nexusMoq.Object, "main");
        }
        else
        {
            var chainMoq = new Mock<IChain>();
            chainMoq.Setup( c => c.GetTransactionHashesForAddress(
                        It.IsAny<Address>())
                    ).Returns(new Hash[]
                        { 
                            Hash.FromString("0"),
                            Hash.FromString("1"),
                            Hash.FromString("2"),
                            Hash.FromString("3"),
                            Hash.FromString("4"),
                        });
            
            chainMoq.Setup( c => c.Nexus
                    ).Returns(nexusMoq.Object);

            chainMoq.Setup( c => c.GetContractByName(It.IsAny<StorageContext>(), It.IsAny<string>())
                    ).Returns(contract);

            chainMoq.Setup( c => c.GetContractContext(It.IsAny<StorageContext>(), It.IsAny<SmartContract>())
                    ).Returns(new ChainExecutionContext(contract));

            chainMoq.Setup( c => c.GenerateUID(It.IsAny<StorageContext>())).Returns(1200);

            chainMoq.Setup( c => c.GetNameFromAddress(It.IsAny<StorageContext>(), It.IsAny<Address>(), It.IsAny<Timestamp>())
                    ).Returns( (StorageContext context, Address address, Timestamp time) => 
                        {
                            return address.ToString();
                        });
            

            // set chain mock
            this.Chain = chainMoq.Object;
        }

        var runtime = new RuntimeVM(
                0,
                new byte[3] { (byte)Opcode.NOP, (byte)Opcode.NOP , (byte)Opcode.RET },
                0,
                this.Chain,
                this.Validator.Address,
                Timestamp.Now,
                tx,
                new StorageChangeSetContext(new MemoryStorageContext()),
                null,
                ChainTask.Null,
                delayPayment
                );


        if (context is not null)
        {
            runtime.RegisterContext(token.Symbol, context);
            runtime.SetCurrentContext(context);
        }

        runtime.SwitchContext(runtime.EntryContext, 0);

        return runtime;
    }

    public MarketContractTests()
    {
        this.Mints = new ();
        this.TokenOwner = PhantasmaKeys.Generate();
        this.User1 = PhantasmaKeys.Generate();
        this.User2 = PhantasmaKeys.Generate();
        this.Validator = PhantasmaKeys.Generate();
        
        this.PartitionPath = Path.Combine(Path.GetTempPath(), "PhantasmaUnitTest", $"{Guid.NewGuid():N}") + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(this.PartitionPath);

        var maxSupply = 10000000;

        var ftFlags = TokenFlags.Burnable | TokenFlags.Divisible | TokenFlags.Fungible | TokenFlags.Mintable | TokenFlags.Stakable | TokenFlags.Transferable;
        //this.FungibleToken = new TokenInfo("EXX", "Example Token", TokenOwner.Address, 0, 8, ftFlags, new byte[1] { 0 }, new ContractInterface());

        this.FungibleToken = new TokenInfo("SYM", "Example Token", TokenOwner.Address, 0, 8, ftFlags, new byte[1] { 0 }, new ContractInterface());

        
        var nftFlags = TokenFlags.Burnable | TokenFlags.Mintable | TokenFlags.Stakable | TokenFlags.Transferable;
        this.NonFungibleToken = new TokenInfo("Y", "Example NFT", User1.Address, 0, 0, nftFlags, new byte[1] { 0 }, new ContractInterface());

        var ntFlags = TokenFlags.Burnable | TokenFlags.Divisible | TokenFlags.Fungible | TokenFlags.Mintable | TokenFlags.Stakable;
        this.NonTransferableToken = new TokenInfo("EXNT", "Example Token non transferable", TokenOwner.Address, 0, 8, ntFlags, new byte[1] { 0 }, new ContractInterface());
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(this.PartitionPath, true);
        }
        catch (IOException)
        {
            Console.WriteLine("Unable to clean test directory");
        }
    }
}