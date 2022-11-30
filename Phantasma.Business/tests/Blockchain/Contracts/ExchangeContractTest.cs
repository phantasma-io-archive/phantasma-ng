using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Moq;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;
using Phantasma.Infrastructure.RocksDB;
using Shouldly;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain.Contracts;


[Collection(nameof(SystemTestCollectionDefinition))]
public class ExchangeContractTest : IDisposable
{

    #region Exchange

    

    #endregion
    
    #region OTC
    /*[Fact]
    public void invoke_CreateOTCOrder_success()
    {
        var runtime = CreateRuntime_MockedChain();

        runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.OpenOTCOrder), "");

        var count = 0;
        foreach (var evt in runtime.Events)
        {
            count++;
            evt.Kind.ShouldBe(EventKind.AddressRegister);
            evt.Address.ShouldBe(User1.Address);
            var name = evt.GetContent<string>();
            name.ShouldBe("somename1");
        }
        count.ShouldBe(1);
    }
    
    public void invoke_CreateOTCOrder_unsuccessful()
    {
        var runtime = CreateRuntime_MockedChain();

        runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.OpenOTCOrder), "");

        var count = 0;
        foreach (var evt in runtime.Events)
        {
            count++;
            evt.Kind.ShouldBe(EventKind.AddressRegister);
            evt.Address.ShouldBe(User1.Address);
            var name = evt.GetContent<string>();
            name.ShouldBe("somename1");
        }
        count.ShouldBe(1);
    }
    
    public void invoke_GetOTC_success()
    {
        var runtime = CreateRuntime_MockedChain();

        runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.GetOTC));

        var count = 0;
        foreach (var evt in runtime.Events)
        {
            count++;
            evt.Kind.ShouldBe(EventKind.AddressRegister);
            evt.Address.ShouldBe(User1.Address);
            var name = evt.GetContent<string>();
            name.ShouldBe("somename1");
        }
        count.ShouldBe(1);
    }
    
    public void invoke_TakeOrder_success()
    {
        var runtime = CreateRuntime_MockedChain();

        runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.TakeOrder), "");

        var count = 0;
        foreach (var evt in runtime.Events)
        {
            count++;
            evt.Kind.ShouldBe(EventKind.AddressRegister);
            evt.Address.ShouldBe(User1.Address);
            var name = evt.GetContent<string>();
            name.ShouldBe("somename1");
        }
        count.ShouldBe(1);
    }
    
    public void invoke_TakeOrder_unsuccessful()
    {
        var runtime = CreateRuntime_MockedChain();

        runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.TakeOrder), "");

        var count = 0;
        foreach (var evt in runtime.Events)
        {
            count++;
            evt.Kind.ShouldBe(EventKind.AddressRegister);
            evt.Address.ShouldBe(User1.Address);
            var name = evt.GetContent<string>();
            name.ShouldBe("somename1");
        }
        count.ShouldBe(1);
    }
    
    public void invoke_CancelOTCOrder_success()
    {
        var runtime = CreateRuntime_MockedChain();

        runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.CancelOTCOrder), "");

        var count = 0;
        foreach (var evt in runtime.Events)
        {
            count++;
            evt.Kind.ShouldBe(EventKind.AddressRegister);
            evt.Address.ShouldBe(User1.Address);
            var name = evt.GetContent<string>();
            name.ShouldBe("somename1");
        }
        count.ShouldBe(1);
    }
    
    public void invoke_CancelOTCOrder_unsuccessful()
    {
        var runtime = CreateRuntime_MockedChain();

        runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.CancelOTCOrder), "");

        var count = 0;
        foreach (var evt in runtime.Events)
        {
            count++;
            evt.Kind.ShouldBe(EventKind.AddressRegister);
            evt.Address.ShouldBe(User1.Address);
            var name = evt.GetContent<string>();
            name.ShouldBe("somename1");
        }
        count.ShouldBe(1);
    }
    
    
    #endregion
 
    #region Dex
    [Fact]
    public void invoke_MigrateToV3_success()
    {
        var runtime = CreateRuntime_MockedChain();

        runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.MigrateToV3), "");

        var count = 0;
        foreach (var evt in runtime.Events)
        {
            count++;
            //evt.Kind.ShouldBe(EventKind.AddressRegister);
            //evt.Address.ShouldBe(User1.Address);
            //var name = evt.GetContent<string>();
            //name.ShouldBe("somename1");
        }
        //count.ShouldBe(1);
    }
    
    [Fact]
    public void invoke_CreatePool_success()
    {
        var runtime = CreateRuntime_MockedChain();

        runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.CreatePool), "");

        var count = 0;
        foreach (var evt in runtime.Events)
        {
            count++;
            evt.Kind.ShouldBe(EventKind.AddressRegister);
            evt.Address.ShouldBe(User1.Address);
            var name = evt.GetContent<string>();
            name.ShouldBe("somename1");
        }
        count.ShouldBe(1);
    }
    
    [Fact]
    public void invoke_CreatePool_unsuccessful()
    {
        var runtime = CreateRuntime_MockedChain();

        runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.CreatePool), User1.Address, "somename1");

        var count = 0;
        foreach (var evt in runtime.Events)
        {
            count++;
            evt.Kind.ShouldBe(EventKind.AddressRegister);
            evt.Address.ShouldBe(User1.Address);
            var name = evt.GetContent<string>();
            name.ShouldBe("somename1");
        }
        count.ShouldBe(1);
    }
    
    [Fact]
    public void invoke_AddLiquidity_success()
    {
        var runtime = CreateRuntime_MockedChain();

        runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.AddLiquidity), User1.Address, "somename1");

        var count = 0;
        foreach (var evt in runtime.Events)
        {
            count++;
            evt.Kind.ShouldBe(EventKind.AddressRegister);
            evt.Address.ShouldBe(User1.Address);
            var name = evt.GetContent<string>();
            name.ShouldBe("somename1");
        }
        count.ShouldBe(1);
    }
    
    [Fact]
    public void invoke_AddLiquidity_unsuccessful()
    {
        var runtime = CreateRuntime_MockedChain();

        runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.AddLiquidity), User1.Address, "somename1");

        var count = 0;
        foreach (var evt in runtime.Events)
        {
            count++;
            evt.Kind.ShouldBe(EventKind.AddressRegister);
            evt.Address.ShouldBe(User1.Address);
            var name = evt.GetContent<string>();
            name.ShouldBe("somename1");
        }
        count.ShouldBe(1);
    }
    
    [Fact]
    public void invoke_RemoveLiquidity_success()
    {
        var runtime = CreateRuntime_MockedChain();

        runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.AddLiquidity), User1.Address, "somename1");

        var count = 0;
        foreach (var evt in runtime.Events)
        {
            count++;
            evt.Kind.ShouldBe(EventKind.AddressRegister);
            evt.Address.ShouldBe(User1.Address);
            var name = evt.GetContent<string>();
            name.ShouldBe("somename1");
        }
        count.ShouldBe(1);
    }
    
    [Fact]
    public void invoke_RemoveLiquidity_unsuccessful()
    {
        var runtime = CreateRuntime_MockedChain();

        runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.AddLiquidity), User1.Address, "somename1");

        var count = 0;
        foreach (var evt in runtime.Events)
        {
            count++;
            evt.Kind.ShouldBe(EventKind.AddressRegister);
            evt.Address.ShouldBe(User1.Address);
            var name = evt.GetContent<string>();
            name.ShouldBe("somename1");
        }
        count.ShouldBe(1);
    }
    
    [Fact]
    public void invoke_GetRate_unsuccessful()
    {
        var runtime = CreateRuntime_MockedChain();

        runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.GetRate), "SOUL", "KCAL", 10000);

    }
    
    // ClaimFees
    // GetUnclaimedFees
    // GetPools
    // GetPool
    // GetTradingVolumeToday
    // GetTradingVolumes
    // MigrateToV3
    // GetRate
    // SwapFiat
    // SwapReverse
    */
    #endregion
    
    #region Mocking
    protected PhantasmaKeys Validator { get; set; }
    protected PhantasmaKeys TokenOwner { get; set; }
    protected PhantasmaKeys User1 { get; set; }
    protected PhantasmaKeys User2 { get; set; }
    protected string PartitionPath { get; set; }
    protected IToken FungibleToken { get; set; }
    protected IToken NonFungibleToken { get; set; }
    protected IToken NonTransferableToken { get; set; }
    protected IChain Chain { get; set; }
    protected Dictionary<string, BigInteger> Mints { get; set; }
    
    /*protected IRuntime CreateRuntime_MockedChain()
    {
        var tx = new Transaction(
                "mainnet",
                DomainSettings.RootChainName,
                new byte[1] { 0 },
                User1.Address,
                User1.Address,
                10000,
                999,
                Timestamp.Now + TimeSpan.FromDays(300),
                "UnitTest");

        tx.Sign(User1);
        tx.Sign(User2);
        tx.Sign(TokenOwner);

        return CreateRuntime(false, FungibleToken, null, tx, true, false);
    }

    protected IRuntime CreateRuntime_Default(bool delayPayment = false, Transaction tx = null, bool tokenExists = true)
    {
        if (tx == null)
        {
            tx = new Transaction(
                "mainnet",
                DomainSettings.RootChainName,
                new byte[1] { 0 },
                User1.Address,
                User1.Address,
                10000,
                999,
                Timestamp.Now + TimeSpan.FromDays(300),
                "UnitTest");

            tx.Sign(User1);
            tx.Sign(User2);
            tx.Sign(TokenOwner);
        }

        return CreateRuntime(delayPayment, FungibleToken, null, tx, false, tokenExists);
    }

    protected IRuntime CreateRuntime(
            bool delayPayment,
            IToken token,
            ExecutionContext context = null,
            Transaction tx = null,
            bool mockChain = false,
            bool tokenExists = true)
    {
        var contract = (SmartContract)Activator.CreateInstance<ExchangeContract>();
        var nexusMoq = new Mock<INexus>();

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

        nexusMoq.Setup( n => n.HasGenesis).Returns(true);
        nexusMoq.Setup( n => n.MaxGas).Returns(100000);

        nexusMoq.Setup( n => n.GetStakeFromAddress(
                    It.IsAny<StorageContext>(),
                    It.IsAny<Address>()))
            .Returns(UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals));

        nexusMoq.Setup( n => n.GetTokenInfo(
                    It.IsAny<StorageContext>(),
                    It.IsAny<string>())
                ).Returns(token);

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
        nexusMoq.Setup(n => n.TokenExists(
            It.IsAny<StorageContext>(),
            It.IsAny<string>())).Returns(true);

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

            chainMoq.Setup( c => c.GetNameFromAddress(It.IsAny<StorageContext>(), It.IsAny<Address>())
                    ).Returns( (StorageContext context, Address address) => 
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
                new StorageChangeSetContext(new KeyStoreStorage(new DBPartition(this.PartitionPath))),
                null,
                ChainTask.Null,
                delayPayment
                );


        if (context is not null)
        {
            runtime.RegisterContext(token.Symbol.ToLower(), context);
            runtime.SetCurrentContext(context);
        }

        runtime.SwitchContext(runtime.EntryContext, 0);

        return runtime;
    }*/
    
    public ExchangeContractTest()
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
        this.FungibleToken = new TokenInfo("SOUL", "Example Token", TokenOwner.Address, 0, 8, ftFlags, new byte[1] { 0 }, new ContractInterface());
        this.FungibleToken = new TokenInfo("KCAL", "Example Token", TokenOwner.Address, 0, 8, ftFlags, new byte[1] { 0 }, new ContractInterface());

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

        this.Mints.Clear();
    }
    #endregion
}
