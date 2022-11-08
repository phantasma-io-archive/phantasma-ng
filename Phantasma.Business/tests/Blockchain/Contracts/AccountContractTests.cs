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

public class AccountContractTests : IDisposable
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
    public void invoke_RegisterName_success()
    {
        var runtime = CreateRuntime_MockedChain();

        runtime.CallNativeContext(NativeContractKind.Account, "registerName", User1.Address, "somename1");

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
    public void invoke_LookupAddress_success()
    {
        var runtime = CreateRuntime_MockedChain();

        runtime.CallNativeContext(NativeContractKind.Account, "registerName", User1.Address, "somename1");
        var result = runtime.CallNativeContext(NativeContractKind.Account, "lookupAddress", User1.Address);

        result.AsString().ShouldBe("somename1");
    }

    [Fact]
    public void invoke_LookupName_success()
    {
        var runtime = CreateRuntime_MockedChain();

        runtime.CallNativeContext(NativeContractKind.Account, "registerName", User1.Address, "somename1");
        var result = runtime.CallNativeContext(NativeContractKind.Account, "lookupName", "somename1");

        result.AsAddress().ShouldBe(User1.Address);
    }

    [Fact]
    public void invoke_UnregisterName_success()
    {
        var runtime = CreateRuntime_MockedChain();

        runtime.CallNativeContext(NativeContractKind.Account, "registerName", User1.Address, "somename1");
        runtime.CallNativeContext(NativeContractKind.Account, "unregisterName", User1.Address);

        var count = 0;
        foreach (var evt in runtime.Events)
        {
            count++;

            switch(evt.Kind)
            {
                case EventKind.AddressRegister:
                    evt.Kind.ShouldBe(EventKind.AddressRegister);
                    evt.Address.ShouldBe(User1.Address);
                    var name = evt.GetContent<string>();
                    name.ShouldBe("somename1");
                    break;

                case EventKind.AddressUnregister:
                    evt.Kind.ShouldBe(EventKind.AddressUnregister);
                    evt.Address.ShouldBe(User1.Address);
                    name = evt.GetContent<string>();
                    name.ShouldBe("somename1");
                    break;

                default:
                    throw new NotImplementedException("Unexpected event");
            }
        }
        count.ShouldBe(2);
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

        return CreateRuntime(false, FungibleToken, null, tx, true, false);
    }

    private IRuntime CreateRuntime_Default(bool delayPayment = false, Transaction tx = null, bool tokenExists = true)
    {
        if (tx == null)
        {
            tx = new Transaction(
                "mainnet",
                DomainSettings.RootChainName,
                new byte[1] { 0 },
                Timestamp.Now + TimeSpan.FromDays(300),
                "UnitTest");

            tx.Sign(User1);
            tx.Sign(User2);
            tx.Sign(TokenOwner);
        }

        return CreateRuntime(delayPayment, FungibleToken, null, tx, false, tokenExists);
    }

    private IRuntime CreateRuntime(
            bool delayPayment,
            IToken token,
            ExecutionContext context = null,
            Transaction tx = null,
            bool mockChain = false,
            bool tokenExists = true)
    {
        var contract = (SmartContract)Activator.CreateInstance<AccountContract>();
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

        nexusMoq.Setup( n => n.HasGenesis()).Returns(true);

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
    }

    public AccountContractTests()
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
        this.FungibleToken = new TokenInfo("EXX", "Example Token", TokenOwner.Address, 0, 8, ftFlags, new byte[1] { 0 }, new ContractInterface());

        var nftFlags = TokenFlags.Burnable | TokenFlags.Mintable | TokenFlags.Stakable | TokenFlags.Transferable;
        this.NonFungibleToken = new TokenInfo("EXNFT", "Example NFT", TokenOwner.Address, 0, 0, nftFlags, new byte[1] { 0 }, new ContractInterface());

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

        this.Mints.Clear();
    }
}
