using System;
using System.IO;
using System.Numerics;
using System.Text;
using Moq;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.VM;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;
using Phantasma.Infrastructure.RocksDB;
using Shouldly;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain;

public class RuntimeTests
{
    private string PartitionPath { get; set; }
    private IToken FungibleToken { get; set; }
    private IToken NonFungibleToken { get; set; }
    private IToken NonTransferableToken { get; set; }
    private INexus Nexus { get; set; }
    private StorageChangeSetContext Context { get; set; }
    private PhantasmaKeys TokenOwner { get; set; }
    private PhantasmaKeys User1 { get; set; }
    private PhantasmaKeys User2 { get; set; }
    private IChain Chain { get; set; }

    [Fact]
    public void simple_transfer_test_runtime()
    {
        var runtime = CreateRuntime(FungibleToken);
        runtime.TransferTokens(FungibleToken.Symbol, User1.Address, User2.Address, 10);
    }

    [Fact]
    public void simple_transfer_test_runtime_fail_symbol()
    {
        var runtime = CreateRuntime_TransferTokens();
        Should.Throw<VMException>(() => runtime.TransferTokens(
                    new string('A', 500),
                    User1.Address,
                    User2.Address,
                    10), "symbol exceeds max length @ ExpectNameLength");
    }

    [Fact]
    public void simple_transfer_test_runtime_fail_source_null()
    {
        var runtime = CreateRuntime_TransferTokens();
        Should.Throw<VMException>(() => runtime.TransferTokens(
                    FungibleToken.Symbol,
                    Address.Null,
                    User2.Address,
                    10), "invalid source");
    }

    [Fact]
    public void simple_transfer_test_runtime_fail_token_exists()
    {
        var runtime = CreateRuntime_TransferTokens(false);
        Should.Throw<VMException>(() => runtime.TransferTokens(
                    FungibleToken.Symbol,
                    User1.Address,
                    User2.Address,
                    10), "invalid token");
    }

    [Fact]
    public void simple_transfer_test_runtime_fail_amount()
    {
        var runtime = CreateRuntime_TransferTokens();
        Should.Throw<VMException>(() => runtime.TransferTokens(
                    FungibleToken.Symbol,
                    User1.Address,
                    User2.Address,
                    -10), "amount must be greater than zero");
    }

    [Fact]
    public void simple_transfer_test_runtime_fail_non_fungible()
    {
        var runtime = CreateRuntime(this.NonFungibleToken);
        Should.Throw<VMException>(() => runtime.TransferTokens(
                    FungibleToken.Symbol,
                    User1.Address,
                    User2.Address,
                    10), "token must be fungible");
    }

    [Fact]
    public void simple_transfer_test_runtime_fail_non_transferable()
    {
        var runtime = CreateRuntime(this.NonTransferableToken);
        Should.Throw<VMException>(() => runtime.TransferTokens(
                    FungibleToken.Symbol,
                    User1.Address,
                    User2.Address,
                    10), "token must be transferable");
    }

    [Fact]
    public void expect_success()
    {
        var runtime = CreateRuntime_TransferTokens();
        runtime.Expect(true, "This is never shown");
    }

    [Fact]
    public void expect_fail()
    {
        var runtime = CreateRuntime_TransferTokens();
        Should.Throw<VMException>(() => runtime.Expect(false, "Expect failed"), "Expect failed");
    }

    [Fact]
    public void execute_runtime_fail_gas_limit_exceeded()
    {
        var sb = new ScriptBuilder();
        for (var i = 0; i < 3000; i++)
        {
            sb.EmitLoad(1, new BigInteger(1));
        }
        sb.Emit(Opcode.RET);
        var script = sb.EndScript();

        var runtime = CreateRuntime(this.NonTransferableToken, true, script);
        runtime.Execute();
        // gas cost LOAD -> 5, RET -> 0 == 15000, allowed 10000
        runtime.ExceptionMessage.ShouldBe("VM gas limit exceeded (10000)/(10005)");
    }

    [Fact]
    public void execute_runtime_fail_gas_limit_exceeded_with_tx()
    {
        var sb = new ScriptBuilder();
        sb.AllowGas();
        for (var i = 0; i < 3000; i++)
        {
            sb.EmitLoad(1, new BigInteger(1));
        }
        sb.SpendGas();
        sb.Emit(Opcode.RET);
        var script = sb.EndScript();

        var tx = new Transaction(
            "mainnet",
            DomainSettings.RootChainName,
            script,
            User1.Address,
            User1.Address,
            10,
            3,
            Timestamp.Now + TimeSpan.FromDays(300),
            "UnitTest");

        tx.Sign(User1);

        var runtime = CreateRuntime(this.FungibleToken, true, tx.Script, tx);
        runtime.Execute();
        runtime.ExceptionMessage.ShouldBe("VM gas limit exceeded (30)/(121)");
    }

    private IRuntime CreateRuntime_TransferTokens(bool tokenExists = true)
    {
        return CreateRuntime(FungibleToken, tokenExists);
    }

    private IRuntime CreateRuntime(IToken token , bool tokenExists = true, byte[] script = null, Transaction tx = null, bool mockChain = true)
    {
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

        nexusMoq.Setup( n => n.GetTokenInfo(
                    It.IsAny<StorageContext>(),
                    It.IsAny<string>())
                ).Returns(token);

        nexusMoq.Setup( n => n.TokenExists(
                    It.IsAny<StorageContext>(),
                    It.IsAny<string>())
                ).Returns(tokenExists);

        nexusMoq.Setup( n => n.HasGenesis()).Returns(true);
        nexusMoq.Setup( n => n.MaxGas).Returns(10000);
        nexusMoq.Setup( n => n.RootStorage).Returns(this.Context);

        nexusMoq.Setup( n => n.GetChainByName(
                    It.IsAny<string>())
                ).Returns(this.Chain);

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

            chainMoq.Setup( c => c.GetTokenBalance(It.IsAny<StorageContext>(), It.IsAny<IToken>(), It.IsAny<Address>())
                    ).Returns(10000000000);

            chainMoq.Setup( c => c.GenerateUID(It.IsAny<StorageContext>())).Returns(1200);

            chainMoq.Setup( c => c.GetLastActivityOfAddress(It.IsAny<Address>())).Returns(new Timestamp(1601092859));


            var contract = (NativeContract)Activator.CreateInstance(typeof(GasContract));
            var context = new ChainExecutionContext(contract);

            chainMoq.Setup( c => c.GetContractByName(It.IsAny<StorageContext>(), It.IsAny<string>())
                    ).Returns((StorageContext storage, string name) => 
                        {
                            //Console.WriteLine("get contract: " + name);
                            return contract;

                        });

            chainMoq.Setup( c => c.GetContractContext(It.IsAny<StorageContext>(), It.IsAny<SmartContract>())
                    ).Returns((StorageContext storage, SmartContract contract) => 
                        {
                            //Console.WriteLine("get context: " + contract.Name);
                            return context;

                        });

            chainMoq.Setup( c => c.GetNameFromAddress(It.IsAny<StorageContext>(), It.IsAny<Address>())
                    ).Returns( (StorageContext context, Address address) => 
                        {
                            return address.ToString();
                        });

            // set chain mock
            this.Chain = chainMoq.Object;
        }

        if (script is null)
        {
            script = new byte[1] { 0 };
        }

        var runtime = new RuntimeVM(
                0,
                script,
                0,
                this.Chain,
                this.TokenOwner.Address,
                Timestamp.Now,
                tx,
                this.Context,
                null,
                ChainTask.Null
                );

        runtime.SetCurrentContext(runtime.EntryContext);

        return runtime;
    }

    public RuntimeTests()
    {
        this.TokenOwner = PhantasmaKeys.Generate();
        this.User1 = PhantasmaKeys.Generate();
        this.User2 = PhantasmaKeys.Generate();
        
        this.PartitionPath = Path.Combine(Path.GetTempPath(), "PhantasmaUnitTest", $"{Guid.NewGuid():N}") + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(this.PartitionPath);

        this.Nexus = new Nexus("unittest", 10000, (name) => new DBPartition(PartitionPath + name));
        var maxSupply = 10000000;

        var ftFlags = TokenFlags.Burnable | TokenFlags.Divisible | TokenFlags.Fungible | TokenFlags.Mintable | TokenFlags.Stakable | TokenFlags.Transferable;
        this.FungibleToken = new TokenInfo("EXX", "Example Token", TokenOwner.Address, 0, 8, ftFlags, new byte[1] { 0 }, new ContractInterface());

        var nftFlags = TokenFlags.Burnable | TokenFlags.Mintable | TokenFlags.Stakable | TokenFlags.Transferable;
        this.NonFungibleToken = new TokenInfo("EXNFT", "Example NFT", TokenOwner.Address, 0, 0, nftFlags, new byte[1] { 0 }, new ContractInterface());

        var ntFlags = TokenFlags.Burnable | TokenFlags.Divisible | TokenFlags.Fungible | TokenFlags.Mintable | TokenFlags.Stakable;
        this.NonTransferableToken = new TokenInfo("EXNT", "Example Token non transferable", TokenOwner.Address, 0, 8, ntFlags, new byte[1] { 0 }, new ContractInterface());

        var storage = (StorageContext)new KeyStoreStorage(Nexus.GetChainStorage("main"));
        this.Context = new StorageChangeSetContext(storage);

        this.Chain = new Chain((Nexus)this.Nexus, "main");

        // setup balances
        var balances = new BalanceSheet(FungibleToken);
        balances.Add(this.Context, User1.Address, maxSupply);
    }
}
