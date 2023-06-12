using System;
using System.Text;
using Phantasma.Business.Blockchain.Archives;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Enums;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Exceptions;

namespace Phantasma.Business.Tests.Blockchain.Storage;

using Xunit;

public class SharedArchiveEncryptionTests
{
    [Fact]
    public void TestSharedArchive()
    {
        var user = PhantasmaKeys.Generate();
        var archive = new SharedArchiveEncryption();
        Assert.Equal(archive.Source, Address.Null);
        Assert.Equal(archive.Destination, Address.Null);
        Assert.Equal(archive.Mode, ArchiveEncryptionMode.Shared);

        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        Assert.Throws<ChainException>(() =>
        {
            var encrypted = archive.Encrypt(data, user);
            var decrypted = archive.Decrypt(encrypted, user);
            Assert.Equal(data, decrypted);
        });
        
        Assert.Throws<ChainException>(() =>
        {
            var decrypted = archive.Decrypt(data, user);
            Assert.Equal(data, decrypted);
        });
        
    }
    
    [Fact]
    public void TestSharedArchiveName()
    {
        var user = PhantasmaKeys.Generate();
        var archive = new SharedArchiveEncryption();
        Assert.Equal(archive.Source, Address.Null);
        Assert.Equal(archive.Destination, Address.Null);
        Assert.Equal(archive.Mode, ArchiveEncryptionMode.Shared);

        var data = "MyNameIs";
        Assert.Throws<NotImplementedException>(() =>
        {
            var encrypted = archive.EncryptName(data, user);
            var decrypted = archive.DecryptName(encrypted, user);
            Assert.Equal(data, decrypted);
        });

        Assert.Throws<NotImplementedException>(() =>
        {
            var decrypted = archive.DecryptName(data, user);
            Assert.NotNull(decrypted);
        });

    }
}
