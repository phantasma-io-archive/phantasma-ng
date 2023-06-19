using System;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Enums;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Domain.Interfaces;

namespace Phantasma.Business.Blockchain.VM;

public partial class RuntimeVM : GasMachine, IRuntime
{
    public bool ChainExists(string name)
    {
        ExpectNameLength(name, nameof(name));
        return Nexus.ChainExists(RootStorage, name);
    }

    public int GetIndexOfChain(string name)
    {
        ExpectNameLength(name, nameof(name));
        return Nexus.GetIndexOfChain(name);
    }

    public IChain GetChainParent(string name)
    {
        ExpectNameLength(name, nameof(name));
        var parentName = Nexus.GetParentChainByName(name);
        return GetChainByName(parentName);
    }


    public void CreateChain(Address creator, string organization, string name, string parentChain)
    {
        ExpectAddressSize(creator, nameof(creator));
        ExpectNameLength(organization, nameof(organization));
        ExpectNameLength(name, nameof(name));
        ExpectNameLength(parentChain, nameof(parentChain));

        Expect(IsRootChain(), "must be root chain");

        var pow = Transaction.Hash.GetDifficulty();
        Expect(pow >= (int)ProofOfWork.Minimal, "expected proof of work");

        Expect(!string.IsNullOrEmpty(name), "name required");
        Expect(!string.IsNullOrEmpty(parentChain), "parent chain required");

        Expect(OrganizationExists(organization), "invalid organization");
        var org = GetOrganization(organization);
        Expect(org.IsMember(creator), "creator does not belong to organization");

        Expect(creator.IsUser, "owner address must be user address");
        Expect(IsStakeMaster(creator), "needs to be master");
        Expect(IsWitness(creator), "invalid witness");

        name = name.ToLowerInvariant();

        Expect(!name.Equals(parentChain, StringComparison.OrdinalIgnoreCase), "same name as parent");

        Nexus.CreateChain(RootStorage, organization, name, parentChain);
        this.Notify(EventKind.ChainCreate, creator, name);
    }

    public bool IsAddressOfParentChain(Address address)
    {
        if (IsRootChain())
        {
            return false;
        }

        ExpectAddressSize(address, nameof(address));
        var parentName = Nexus.GetParentChainByName(Chain.Name);
        var target = Nexus.GetChainByAddress(address);
        return target.Name == parentName;
    }

    public bool IsAddressOfChildChain(Address address)
    {
        ExpectAddressSize(address, nameof(address));
        var parentName = Nexus.GetParentChainByAddress(address);
        return Chain.Name == parentName;
    }

    public bool IsNameOfParentChain(string name)
    {
        if (IsRootChain())
        {
            return false;
        }

        ExpectNameLength(name, nameof(name));
        var parentName = Nexus.GetParentChainByName(Chain.Name);
        return name == parentName;
    }

    public bool IsNameOfChildChain(string name)
    {
        ExpectNameLength(name, nameof(name));
        var parentName = Nexus.GetParentChainByName(name);
        return Chain.Name == parentName;
    }

    public IChain GetChainByAddress(Address address)
    {
        ExpectAddressSize(address, nameof(address));
        return Nexus.GetChainByAddress(address);
    }

    public IChain GetChainByName(string name)
    {
        ExpectNameLength(name, nameof(name));
        return Nexus.GetChainByName(name);
    }

    public string[] GetChains()
    {
        return Nexus.GetChains(RootStorage);
    }

    public IChain GetRootChain()
    {
        return GetChainByName(DomainSettings.RootChainName);
    }

    public bool IsRootChain()
    {
        var rootChain = GetRootChain();
        return Chain.Address == rootChain.Address;
    }
}
