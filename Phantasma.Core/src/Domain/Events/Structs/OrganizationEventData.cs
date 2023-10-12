using Phantasma.Core.Cryptography.Structs;

namespace Phantasma.Core.Domain.Events.Structs;

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
