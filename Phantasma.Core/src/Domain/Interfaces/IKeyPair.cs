using System;
using Phantasma.Core.Cryptography;

namespace Phantasma.Core.Domain.Interfaces;

public interface IKeyPair
{
    byte[] PrivateKey { get; }
    byte[] PublicKey { get; }

    // byte[] customSignFunction(byte[] message, byte[] prikey, byte[] pubkey)
    // allows singning with custom crypto libs.
    Signature Sign(byte[] msg, Func<byte[], byte[], byte[], byte[]> customSignFunction = null);
}
