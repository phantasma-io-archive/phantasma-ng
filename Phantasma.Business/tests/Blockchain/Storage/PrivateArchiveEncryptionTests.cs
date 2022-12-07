using System;
using System.IO;
using System.Text;
using Neo.Wallets;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;

namespace Phantasma.Business.Tests.Blockchain.Storage;

using Phantasma.Business.Blockchain.Storage;

using Xunit;

public class PrivateArchiveEncryptionTests
{
    [Fact]
    public void TestEncryption()
    {
        var keys = PhantasmaKeys.Generate();

        var archive = new PrivateArchiveEncryption(keys.Address);
        Assert.Equal(archive.Address, keys.Address);
        Assert.Equal(archive.Mode, ArchiveEncryptionMode.Private);

        var data = new byte[32];

        var encrypted = archive.Encrypt(data, keys);

        var decrypted = archive.Decrypt(encrypted, keys);

        Assert.Equal(data, decrypted);
    }
    
    [Fact]
    public void TestEncryptionName()
    {
        var keys = PhantasmaKeys.Generate();

        var archive = new PrivateArchiveEncryption(keys.Address);

        var data = "name";

        var encrypted = archive.EncryptName(data, keys);

        var decrypted = archive.DecryptName(encrypted, keys);

        Assert.Equal(data, decrypted);
        
        Assert.Equal(keys.Address, archive.Address);
    }
    
    [Fact]
    public void TestEncryptionError()
    {
        var keys = PhantasmaKeys.Generate();

        var archive = new PrivateArchiveEncryption();

        var data = Encoding.UTF8.GetBytes("content");

        Assert.Throws<ChainException>(() =>
        {
            var encrypted = archive.Encrypt(data, keys);
            var decrypted = archive.Decrypt(encrypted, keys);
            Assert.Equal(data, decrypted);
        });
    }
    
    [Fact]
    public void TestEncryptionNameError()
    {
        var keys = PhantasmaKeys.Generate();

        var archive = new PrivateArchiveEncryption();

        var data = "name";

        Assert.Throws<ChainException>(() =>
        {
            var encrypted = archive.EncryptName(data, keys);
            var decrypted = archive.DecryptName(encrypted, keys);
            Assert.Equal(data, decrypted);
        });
    }
    
    [Fact]
    public void TestEncryptionSerialization()
    {
        var keys = PhantasmaKeys.Generate();

        var archive = new PrivateArchiveEncryption(keys.Address);
        

        var data = "name";

        var encrypted = archive.EncryptName(data, keys);

        var bytes = new byte[256];
        var stream = new MemoryStream(bytes);
        var writer = new BinaryWriter(stream);
        archive.SerializeData(writer);
        
        var stream2 = new MemoryStream(bytes);
        var reader = new BinaryReader(stream2);
        archive.UnserializeData(reader);
        
        Assert.Equal(archive.Address, keys.Address);

        var decrypted = archive.DecryptName(encrypted, keys);
        Assert.Equal(data, decrypted);
    }
}
