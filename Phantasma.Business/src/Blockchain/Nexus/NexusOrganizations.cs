using System;
using System.Linq;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Core;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Validation;
using Phantasma.Core.Storage.Context;

namespace Phantasma.Business.Blockchain;

public partial class Nexus
{
    #region ORGANIZATIONS
    public void CreateOrganization(StorageContext storage, string ID, string name, byte[] script)
    {
        var organizationList = GetSystemList(OrganizationTag, storage);

        var organization = new Organization(ID, storage);
        organization.Init(name, script);

        // add to persistent list of tokens
        organizationList.Add(ID);

        var organizationMap = GetSystemMap(OrganizationTag, storage);
        organizationMap.Set<Address, string>(organization.Address, ID);
    }

    public bool OrganizationExists(StorageContext storage, string name) // name in this case is actually the id....
    {
        var orgs = GetOrganizations(storage);
        return orgs.Contains(name);
    }

    public IOrganization GetOrganizationByName(StorageContext storage, string name) // name in this case is actually the id....
    {
        if (OrganizationExists(storage, name))
        {
            var org = new Organization(name, storage);
            return org;
        }

        return null;
    }

    public IOrganization GetOrganizationByAddress(StorageContext storage, Address address)
    {
        var organizationMap = GetSystemMap(OrganizationTag, storage);
        if (organizationMap.ContainsKey<Address>(address))
        {
            var name = organizationMap.Get<Address, string>(address);
            return GetOrganizationByName(storage, name);
        }

        return null;
    }
    
    public string[] GetOrganizations(StorageContext storage)
    {
        var list = GetSystemList(OrganizationTag, storage);
        return list.All<string>();
    }
    #endregion
}
