using System.Numerics;

namespace Phantasma.Core
{
    public interface IOrganization
    {
        string ID { get; }
        string Name { get; }
        Address Address { get; }
        byte[] Script { get; }
        BigInteger Size { get; } // number of members

        bool IsMember(Address address);
        bool IsWitness(Transaction transaction);
        bool MigrateMember(IRuntime Runtime, Address admin, Address from, Address to);
        bool AddMember(IRuntime Runtime, Address from, Address target);
        bool RemoveMember(IRuntime Runtime, Address from, Address target);
        Address[] GetMembers();
    }
}
