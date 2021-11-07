using Phantasma.Shared.Types;

namespace Phantasma.Core
{
    public interface ITransaction
    {
        byte[] Script { get; }

        string NexusName { get; }
        string ChainName { get; }

        Timestamp Expiration { get; }

        byte[] Payload { get; }

        Signature[] Signatures { get; }
        Hash Hash { get; }
    }
}
