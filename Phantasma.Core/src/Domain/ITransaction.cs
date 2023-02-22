using System;
using System.Collections.Generic;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Types;

namespace Phantasma.Core.Domain
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
        
        bool HasSignatures { get; }

        byte[] ToByteArray(bool withSignature);

        void Sign(IKeyPair keypair, Func<byte[], byte[], byte[], byte[]> customSignFunction = null);
        void AddSignature(Signature signature);

        Signature GetTransactionSignature(IKeyPair keypair,
            Func<byte[], byte[], byte[], byte[]> customSignFunction = null);
        
        
        bool IsSignedBy(Address address);
        bool IsSignedBy(IEnumerable<Address> addresses);

        void Mine(ProofOfWork targetDifficulty);
        void Mine(int targetDifficulty);
        
        //ITransaction Unserialize(byte[] bytes);
    }
}
