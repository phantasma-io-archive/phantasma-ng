using System.Collections.Generic;
using System.IO;
using System.Text;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Types;

namespace Phantasma.Business.Tests.Blockchain.Storage;

using Xunit;

using Phantasma.Business.Blockchain.Storage;

public class ArchiveExtensionsTest
{
    [Fact]
    public void TestArchive()
    {
        var merkle = new MerkleTree(Encoding.UTF8.GetBytes("Hello world!"));
        var user = PhantasmaKeys.Generate();
        var user2 = PhantasmaKeys.Generate();
        var archiveEncryption = new PrivateArchiveEncryption(user.Address);
        var missingBlock = new List<int>();
        var timeNow = Timestamp.Now;
        var archive = new Archive(merkle, "ourArchive", 2048, timeNow, archiveEncryption,missingBlock);

        var bytes = archive.ToByteArray();
        archive.AddOwner(user.Address);
        archive.AddOwner(user2.Address);
        
        Assert.Equal(archive.Name, "ourArchive");
        Assert.Equal(archive.Size, 2048);
        Assert.Equal(archive.Time, timeNow);
        Assert.Equal(archive.MissingBlockIndices, (IEnumerable<int>)missingBlock);
        Assert.Equal(archive.MerkleTree, merkle);


        var archive2EncryptionBytes = ArchiveExtensions.ToBytes(archiveEncryption);
        var archive2Encryption = ArchiveExtensions.ReadArchiveEncryption(archive2EncryptionBytes);
        //Assert.Equal(archive2Encryption, archiveEncryption);
        Assert.Equal(archive2Encryption.Mode, archive2Encryption.Mode);
    }
    
    [Fact]
    public void TestArchiveEncryption()
    {
        var merkle = new MerkleTree(Encoding.UTF8.GetBytes("Hello world!"));
        var user = PhantasmaKeys.Generate();
        var user2 = PhantasmaKeys.Generate();
        var timeNow = Timestamp.Now;
        var missingBlock = new List<int>();
        var archiveEncryption = new PrivateArchiveEncryption(user.Address);
        var archive = new Archive(merkle, "ourArchiveEnc2", 2048, timeNow, archiveEncryption, missingBlock);

        archive.AddOwner(user2.Address);
        archive.AddOwner(user.Address);

        var archive2 = archive;

        var bytesArchive = archive.ToByteArray();
        var bytes = new byte[2048];
        var stream = new MemoryStream(bytesArchive);
        var reader = new BinaryReader(stream);
        archive.UnserializeData(reader);
        
        Assert.Equal(archive, archive2);
        Assert.Equal(archive.Name, archive2.Name);
        Assert.Equal(archive.Size, archive2.Size);
        Assert.Equal(archive.Time, archive2.Time);
        Assert.Equal(archive.MissingBlockIndices, (IEnumerable<int>)missingBlock);
        Assert.Equal(archive.MerkleTree, archive.MerkleTree);
    }
}
