using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Moq;
using Phantasma.Business.Blockchain;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Storage.Context;
using Phantasma.Infrastructure.RocksDB;
using Phantasma.Core.Types;
using Shouldly;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain;

public class OrganizationTests : IDisposable
{
    private StorageContext Context { get; set; }

    private PhantasmaKeys User1 { get; set; }
    private PhantasmaKeys User2 { get; set; }
    private PhantasmaKeys User3 { get; set; }

    private string PartitionPath { get; set; }
    private INexus Nexus { get; set; }
    private Chain Chain { get; set; }

    private List<Event> Events { get; set; } = new ();

    [Fact]
    public void org_create_no_init_test()
    {
        var org = CreateOrganization(false);
        org.Name.ShouldBeNull();
    }

    [Fact]
    public void org_create_init_test()
    {
        var org = CreateOrganization();
        org.Name.ShouldBe("TestOrg");
    }

    [Fact]
    public void org_add_member_test()
    {
        var org = CreateOrganization();
        var runtimeMoq = CreateRuntimeMock();

        var isMember = org.IsMember(this.User1.Address);
        isMember.ShouldBeFalse();

        var addMemberSuccess = org.AddMember(runtimeMoq.Object, this.User1.Address, this.User2.Address);
        addMemberSuccess.ShouldBeTrue();

        isMember = org.IsMember(this.User2.Address);
        isMember.ShouldBeTrue();

        var counter = 0;
        foreach (var evt in this.Events)
        {
            counter++;
            evt.Kind.ShouldBe(EventKind.OrganizationAdd);
            var orgEvtData = evt.GetContent<OrganizationEventData>();
            orgEvtData.Organization.ShouldBe("TestOrg");
            orgEvtData.MemberAddress.ShouldBe(this.User2.Address);
        }
        counter.ShouldBe(1);

        org.Size.ShouldBe(1);
    }

    [Fact]
    public void org_remove_member_test()
    {
        var org = CreateOrganization();
        var runtimeMoq = CreateRuntimeMock();

        var isMember = org.IsMember(this.User1.Address);
        isMember.ShouldBeFalse();

        var addMemberSuccess = org.AddMember(runtimeMoq.Object, this.User1.Address, this.User2.Address);
        addMemberSuccess.ShouldBeTrue();

        isMember = org.IsMember(this.User2.Address);
        isMember.ShouldBeTrue();

        var counter = 0;
        foreach (var evt in this.Events)
        {
            counter++;
            evt.Kind.ShouldBe(EventKind.OrganizationAdd);
            var orgEvtData = evt.GetContent<OrganizationEventData>();
            orgEvtData.Organization.ShouldBe("TestOrg");
            orgEvtData.MemberAddress.ShouldBe(this.User2.Address);
        }
        counter.ShouldBe(1);

        var removeMemberSuccess = org.RemoveMember(runtimeMoq.Object, this.User1.Address, this.User2.Address);
        addMemberSuccess.ShouldBeTrue();

        isMember = org.IsMember(this.User2.Address);
        isMember.ShouldBeFalse();
    }

    [Fact]
    public void org_get_members_test()
    {
        var org = CreateOrganization();
        var runtimeMoq = CreateRuntimeMock();

        var members = org.GetMembers();
        members.Length.ShouldBe(0);

        var addMemberSuccess = org.AddMember(runtimeMoq.Object, this.User1.Address, this.User2.Address);
        addMemberSuccess.ShouldBeTrue();

        members = org.GetMembers();
        members.Length.ShouldBe(1);

        var counter = 0;
        foreach (var evt in this.Events)
        {
            counter++;
            evt.Kind.ShouldBe(EventKind.OrganizationAdd);
            var orgEvtData = evt.GetContent<OrganizationEventData>();
            orgEvtData.Organization.ShouldBe("TestOrg");
            orgEvtData.MemberAddress.ShouldBe(this.User2.Address);
        }
        counter.ShouldBe(1);

        var removeMemberSuccess = org.RemoveMember(runtimeMoq.Object, this.User1.Address, this.User2.Address);
        addMemberSuccess.ShouldBeTrue();

        members = org.GetMembers();
        members.Length.ShouldBe(0);
    }

    [Fact]
    public void org_is_witness_test()
    {
        var org = CreateOrganization();
        var runtimeMoq = CreateRuntimeMock();

        var tx = new Transaction(
            "mainnet",
            DomainSettings.RootChainName,
            new byte[1] { 0 },
            Timestamp.Now + TimeSpan.FromDays(300),
            "UnitTest");

        tx.Sign(this.User1);

        var isWitness = org.IsWitness(tx);
        // org has no members
        isWitness.ShouldBe(false);

        var addMemberSuccess = org.AddMember(runtimeMoq.Object, this.User1.Address, this.User2.Address);
        addMemberSuccess.ShouldBeTrue();

        tx.Sign(this.User2);

        isWitness = org.IsWitness(tx);
        // one member one signer ->  majority
        isWitness.ShouldBe(true);

        addMemberSuccess = org.AddMember(runtimeMoq.Object, this.User1.Address, this.User3.Address);
        addMemberSuccess.ShouldBeTrue();

        tx.Sign(this.User3);

        isWitness = org.IsWitness(tx);
        // all members signed, majority reached
        isWitness.ShouldBe(true);

    }

    [Fact]
    public void org_is_witness_test_2()
    {
        var org = CreateOrganization();
        var runtimeMoq = CreateRuntimeMock();

        var tx = new Transaction(
            "mainnet",
            DomainSettings.RootChainName,
            new byte[1] { 0 },
            Timestamp.Now + TimeSpan.FromDays(300),
            "UnitTest");

        var addMemberSuccess = org.AddMember(runtimeMoq.Object, this.User1.Address, this.User2.Address);
        addMemberSuccess.ShouldBeTrue();

        tx.Sign(this.User1);

        var isWitness = org.IsWitness(tx);
        // org has one member, but signer is no member
        isWitness.ShouldBe(false);

        tx.Sign(this.User2);

        isWitness = org.IsWitness(tx);
        // org has one member, one signer is no member other signer is member
        isWitness.ShouldBe(true);
    }

    private Organization CreateOrganization(bool init = true)
    {
        var org = new Organization("TestOrg", this.Context);
        if (init)
        {
            org.Init("TestOrg", new byte[0]);
        }

        return org;
    }

    private Mock<IRuntime> CreateRuntimeMock(bool isWitness = true, bool allowance = true)
    {
        var runtimeMoq = new Mock<IRuntime>();

        // setup Expect
        runtimeMoq.Setup( r => r.Expect(It.IsAny<bool>(), It.IsAny<string>()))
                .Callback( (bool condition, string msg) => 
                        {
                            if (!condition)
                                throw new Exception(msg);
                        });

        // setup Storage
        runtimeMoq.Setup( r => r.Storage).Returns(this.Context);

        // setup Chain
        runtimeMoq.Setup( r => r.Chain).Returns(this.Chain);

        // setup GetChainByName
        runtimeMoq.Setup( r => r.GetChainByName(It.IsAny<string>())).Returns(this.Chain);

        // setup allowance
        runtimeMoq.Setup( r => r.SubtractAllowance(It.IsAny<Address>(), It.IsAny<string>(), It.IsAny<BigInteger>())).Returns(allowance);

        // setup witness 
        runtimeMoq.Setup(r => r.IsWitness(It.IsAny<Address>())).Returns(isWitness);

        // setup GetRootChain
        runtimeMoq.Setup(r => r.GetRootChain()).Returns(this.Chain);

        // setup IsRootChain
        runtimeMoq.Setup(r => r.IsRootChain()).Returns(true);

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

    public void Dispose()
    {
        this.Context.Clear();

        try
        {
            Directory.Delete(this.PartitionPath, true);
        }
        catch (IOException)
        {
            Console.WriteLine("Unable to clean test directory");
        }
    }

    public OrganizationTests()
    {
        this.User1 = PhantasmaKeys.Generate();
        this.User2 = PhantasmaKeys.Generate();
        this.User3 = PhantasmaKeys.Generate();

        this.PartitionPath = Path.Combine(Path.GetTempPath(), "PhantasmaUnitTest", $"{Guid.NewGuid():N}") + Path.DirectorySeparatorChar;
        if (Directory.Exists(this.PartitionPath))
        {
            Directory.Delete(this.PartitionPath, true);
        }
        Directory.CreateDirectory(this.PartitionPath);

        this.Nexus = new Nexus("unittest", 10000, (name) => new DBPartition(PartitionPath + name));

        var storage = (StorageContext)new KeyStoreStorage(Nexus.GetChainStorage("main"));
        this.Context = new StorageChangeSetContext(storage);

        this.Chain = new Chain((Nexus)this.Nexus, "main");
    }
}
