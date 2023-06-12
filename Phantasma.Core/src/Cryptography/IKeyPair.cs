using System;

namespace Phantasma.Core.Cryptography;

public interface IKeyPair
{
    byte[] PrivateKey { get; }
    byte[] PublicKey { get; }

    // byte[] customSignFunction(byte[] message, byte[] prikey, byte[] pubkey)
    // allows singning with custom crypto libs.
    Signature Sign(byte[] msg, Func<byte[], byte[], byte[], byte[]> customSignFunction = null);
}
