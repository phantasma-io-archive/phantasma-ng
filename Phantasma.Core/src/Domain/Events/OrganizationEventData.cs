using Phantasma.Core.Cryptography;

namespace Phantasma.Core.Domain.Events;

public struct OrganizationEventData
{
    public readonly string Organization;
    public readonly Address MemberAddress;

    public OrganizationEventData(string organization, Address memberAddress)
    {
        this.Organization = organization;
        this.MemberAddress = memberAddress;
    }
}
