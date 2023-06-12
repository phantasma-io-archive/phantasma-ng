using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Phantasma.Business.Blockchain.Archives;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Types;

namespace Phantasma.Business.Tests.Blockchain.Storage;

using Xunit;

public class ArchiveTest
{
    [Fact]
    public void TestArchiveShared()
    {
        var merkle = new MerkleTree(Encoding.UTF8.GetBytes("Hello world!"));
        var user = PhantasmaKeys.Generate();
        var user2 = PhantasmaKeys.Generate();
        var archiveSize = DomainSettings.ArchiveBlockSize * 8;
        var timeNow = Timestamp.Now;
        var missingBlock = new List<int>(){0, 1, 2, 3, 4, 5};
        var archiveEncryption = new SharedArchiveEncryption();
        var archive = new Archive(merkle, "ourArchiveEnc2", archiveSize, timeNow, archiveEncryption, missingBlock);

        archive.AddOwner(user2.Address);
        archive.AddOwner(user.Address);

        var archive2 = archive;

        var bytesArchive = archive.ToByteArray();
        var bytes = new byte[archiveSize];
        var stream = new MemoryStream(bytesArchive);
        var reader = new BinaryReader(stream);
        archive.UnserializeData(reader);

        var blockCount = (archive.Size / DomainSettings.ArchiveBlockSize);
        if (archive.Size % DomainSettings.ArchiveBlockSize != 0)
        {
            blockCount++;
        }
        
        Assert.Equal(archive, archive2);
        Assert.Equal(archive.Name, archive2.Name);
        Assert.Equal(archive.Size, archive2.Size);
        Assert.Equal(archive.Time, archive2.Time);
        Assert.Equal(archive.MissingBlockIndices, (IEnumerable<int>)missingBlock);
        Assert.Equal(archive.MerkleTree, archive.MerkleTree);
        Assert.True(archive.IsOwner(user.Address));
        Assert.NotEqual(0, archive.MissingBlockCount);
        Assert.Equal(archive.BlockCount, blockCount);
        Assert.True(archive.BlockHashes != null);
        
        archive2.AddMissingBlock(0);
        archive2.AddMissingBlock(1);
        
        Assert.Equal(4, archive2.MissingBlockCount);

        Assert.Equal(archive.Owners.Count(), 2);
        
        // TODO: allow the ability to encode with multiple keys
        /*
        var dataToEncrypt = Encoding.UTF8.GetBytes("Hello world!");
        var encryptedData = archive.Encryption.Encrypt(dataToEncrypt, user);
        var decryptedData = archive.Encryption.Decrypt(encryptedData, user);
        Assert.Equal(dataToEncrypt, decryptedData);*/ 
        

    }
}
