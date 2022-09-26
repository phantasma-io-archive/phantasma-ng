using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Phantasma.Blockchain.Tests;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Storage.Context;
using Phantasma.Infrastructure.RocksDB;
using Shouldly;

namespace Phantasma.Business.Tests.Blockchain;

[TestClass]
public class NexusTests
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
    private List<Event> Events { get; set; } = new ();

    [TestMethod]
    public void simple_transfer_test_success()
    {
        var moq = CreateRuntimeMock_TransferTokens();

        // transfer tokens
        this.Nexus.TransferTokens(moq.Object, FungibleToken, User1.Address, User2.Address, 10);

        // check balance
        var User2SoulBalance = new BalanceSheet(FungibleToken).Get(this.Context, User2.Address);
        User2SoulBalance.ShouldBe(10);

        // check events
        foreach (var evt in this.Events)
        {
            switch (evt.Kind)
            {
                case EventKind.TokenReceive:
                case EventKind.TokenSend:
                    var data = evt.GetContent<TokenEventData>();
                    data.Symbol.ShouldBe(FungibleToken.Symbol);
                    data.Value.ShouldBe(10);
                    data.ChainName.ShouldBe("main");
                    break;
                default:
                    throw new NotImplementedException();

            }
        }
    }

    private Mock<IRuntime> CreateRuntimeMock_TransferTokens(bool isWitness = true, bool allowance = true)
    {
        var runtimeMoq = new Mock<IRuntime>();

        // setup Expect
        runtimeMoq.Setup( r => r.Expect(It.IsAny<bool>(), It.IsAny<string>()));

        // setup Storage
        runtimeMoq.Setup( r => r.Storage).Returns(this.Context);

        // setup Chain
        runtimeMoq.Setup( r => r.Chain).Returns(this.Chain);

        // setup allowance
        runtimeMoq.Setup( r => r.SubtractAllowance(It.IsAny<Address>(), It.IsAny<string>(), It.IsAny<BigInteger>())).Returns(allowance);

        // setup witness 
        runtimeMoq.Setup( r => r.IsWitness(It.IsAny<Address>())).Returns(isWitness);

        // setup Triggers
        runtimeMoq.SetupInvokeTriggerMoq(TriggerResult.Success, TriggerResult.Success);

        // setup Notify
        runtimeMoq.Setup(r => r.Notify(It.IsAny<EventKind>(), It.IsAny<Address>(), It.IsAny<byte[]>()))
            .Callback<EventKind, Address, byte[]>( (evt, address, content) =>
                    {
                    this.Events.Add(new Event(evt, address, "", content));
                    });

        return runtimeMoq;
    }

    [TestInitialize]
    public void Setup()
    {

        this.TokenOwner = PhantasmaKeys.Generate();
        this.User1 = PhantasmaKeys.Generate();
        this.User2 = PhantasmaKeys.Generate();

        this.PartitionPath = Path.GetTempPath() + "/PhantasmaUnitTest/";
        Directory.CreateDirectory(this.PartitionPath);

        this.Nexus = new Nexus("unittest", 10000, (name) => new DBPartition(PartitionPath + name));
        var maxSupply = 10000000;

        var flags = TokenFlags.Burnable | TokenFlags.Divisible | TokenFlags.Fungible | TokenFlags.Mintable | TokenFlags.Stakable | TokenFlags.Transferable;
        this.FungibleToken = new TokenInfo("SOUL", "PhantasmaStake", TokenOwner.Address, 0, 8, flags, new byte[1] { 0 }, new ContractInterface());

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

    [TestCleanup]
    public void TearDown()
    {
        this.Events.Clear();
        this.Context.Clear();
    }
}

