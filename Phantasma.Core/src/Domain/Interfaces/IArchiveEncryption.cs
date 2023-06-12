using Phantasma.Core.Cryptography;

namespace Phantasma.Core.Domain.Interfaces;

public interface IArchiveEncryption : ISerializable
{
    ArchiveEncryptionMode Mode { get; }

    string EncryptName(string name, PhantasmaKeys keys);
    string DecryptName(string name, PhantasmaKeys keys);
    byte[] Encrypt(byte[] chunk, PhantasmaKeys keys);
    byte[] Decrypt(byte[] chunk, PhantasmaKeys keys);
}
