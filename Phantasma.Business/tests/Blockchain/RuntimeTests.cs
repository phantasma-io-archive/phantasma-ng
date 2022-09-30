using System.IO;
using System.Numerics;
using Moq;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.VM;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Storage.Context;
using Phantasma.Infrastructure.RocksDB;
using Phantasma.Shared.Types;
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
    private Chain Chain { get; set; }

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

    private IRuntime CreateRuntime_TransferTokens(bool tokenExists = true)
    {
        return CreateRuntime(FungibleToken, tokenExists);
    }

    private IRuntime CreateRuntime(IToken token , bool tokenExists = true)
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

        nexusMoq.Setup( n => n.GetChainByName(
                    It.IsAny<string>())
                ).Returns(this.Chain);

        this.Chain = new Chain(nexusMoq.Object, "main");

        return new RuntimeVM(
                0,
                new byte[1] {0},
                0,
                this.Chain,
                this.TokenOwner.Address,
                Timestamp.Now,
                null,
                this.Context,
                null,
                ChainTask.Null
                );
    }

    public RuntimeTests()
    {
        this.TokenOwner = PhantasmaKeys.Generate();
        this.User1 = PhantasmaKeys.Generate();
        this.User2 = PhantasmaKeys.Generate();

        this.PartitionPath = Path.GetTempPath() + "/PhantasmaUnitTest/";
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
