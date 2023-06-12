using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Validation;

namespace Phantasma.Business.Blockchain.VM;

public partial class RuntimeVM : GasMachine, IRuntime
{
    public void CreateOrganization(Address from, string ID, string name, byte[] script)
    {
        ExpectAddressSize(from, nameof(from));
        ExpectNameLength(ID, nameof(ID));
        ExpectNameLength(name, nameof(name));
        ExpectScriptLength(script, nameof(script));

        Expect(IsRootChain(), "must be root chain");

        Expect(IsWitness(from), "invalid witness");

        Expect(ValidationUtils.IsValidIdentifier(ID), "invalid organization name");

        Expect(!Nexus.OrganizationExists(RootStorage, ID), "organization already exists");

        Nexus.CreateOrganization(RootStorage, ID, name, script);

        var org = GetOrganization(ID) as Organization;
        org.InitCreator(from);

        if (Nexus.HasGenesis())
        {
            var fuelCost = GetGovernanceValue(DomainSettings.FuelPerOrganizationDeployTag);
            // governance value is in usd fiat, here convert from fiat to fuel amount
            fuelCost = this.GetTokenQuote(DomainSettings.FiatTokenSymbol, DomainSettings.FuelTokenSymbol, fuelCost);
            // burn the "cost" tokens
            BurnTokens(DomainSettings.FuelTokenSymbol, from, fuelCost);
        }


        this.Notify((EventKind)EventKind.OrganizationCreate, from, ID);
    }
    
    public string[] GetOrganizations()
    {
        return Nexus.GetOrganizations(RootStorage);
    }

    public bool OrganizationExists(string name)
    {
        ExpectNameLength(name, nameof(name));
        return Nexus.OrganizationExists(RootStorage, name);
    }

    public IOrganization GetOrganization(string name)
    {
        ExpectNameLength(name, nameof(name));
        return Nexus.GetOrganizationByName(RootStorage, name);
    }

    public bool AddMember(string organization, Address admin, Address target)
    {
        ExpectNameLength(organization, nameof(organization));
        ExpectAddressSize(admin, nameof(admin));
        ExpectAddressSize(target, nameof(target));

        var org = Nexus.GetOrganizationByName(RootStorage, organization);
        return org.AddMember(this, admin, target);
    }

    public bool RemoveMember(string organization, Address admin, Address target)
    {
        ExpectNameLength(organization, nameof(organization));
        ExpectAddressSize(admin, nameof(admin));
        ExpectAddressSize(target, nameof(target));

        var org = Nexus.GetOrganizationByName(RootStorage, organization);
        return org.RemoveMember(this, admin, target);
    }

    public void MigrateToken(Address from, Address to)
    {
        ExpectAddressSize(from, nameof(from));
        ExpectAddressSize(to, nameof(to));

        Nexus.MigrateTokenOwner(RootStorage, from, to);
    }

    public void MigrateMember(string organization, Address admin, Address source, Address destination)
    {
        ExpectNameLength(organization, nameof(organization));
        ExpectAddressSize(admin, nameof(admin));
        ExpectAddressSize(source, nameof(source));
        ExpectAddressSize(destination, nameof(destination));

        var org = Nexus.GetOrganizationByName(RootStorage, organization);
        org.MigrateMember(this, admin, source, destination);
    }
}
