using System.Linq;
using System.Numerics;
using Phantasma.Business.Blockchain.Archives;
using Phantasma.Core;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Exceptions;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Storage.Context.Structs;
using Phantasma.Core.Storage.Interfaces;
using Phantasma.Core.Types.Structs;

namespace Phantasma.Business.Blockchain;

public partial class Nexus
{
    #region STORAGE

    private StorageMap GetArchiveMap(StorageContext storage)
    {
        var map = new StorageMap(ChainArchivesKey, storage);
        return map;
    }

    public IArchive GetArchive(StorageContext storage, Hash hash)
    {
        var map = GetArchiveMap(storage);

        if (map.ContainsKey(hash))
        {
            var bytes = map.Get<Hash, byte[]>(hash);
            var archive = Archive.Unserialize(bytes);
            return archive;
        }

        return null;
    }

    public bool ArchiveExists(StorageContext storage, Hash hash)
    {
        var map = GetArchiveMap(storage);
        return map.ContainsKey(hash);
    }

    public bool IsArchiveComplete(IArchive archive)
    {
        for (int i = 0; i < archive.BlockCount; i++)
        {
            if (!HasArchiveBlock(archive, i))
            {
                return false;
            }
        }

        return true;
    }

    public IArchive CreateArchive(StorageContext storage, MerkleTree merkleTree, Address owner, string name, BigInteger size, Timestamp time, IArchiveEncryption encryption)
    {
        var archive = GetArchive(storage, merkleTree.Root);
        Throw.If(archive != null, "archive already exists");

        archive = new Archive(merkleTree, name, size, time, encryption,
            Enumerable.Range(0, (int)MerkleTree.GetChunkCountForSize(size)).ToList());
        var archiveHash = merkleTree.Root;

        AddOwnerToArchive(storage, archive, owner);

        // ModifyArchive(storage, archive); => not necessary, addOwner already calls this

        return archive;
    }

    private void ModifyArchive(StorageContext storage, IArchive archive)
    {
        var map = GetArchiveMap(storage);
        var bytes = archive.ToByteArray();
        map.Set<Hash, byte[]>(archive.Hash, bytes);
    }

    public bool DeleteArchive(StorageContext storage, IArchive archive)
    {
        Throw.IfNull(archive, nameof(archive));

        Throw.If(archive.OwnerCount > 0, "can't delete archive, still has owners");

        for (int i = 0; i < archive.BlockCount; i++)
        {
            var blockHash = archive.MerkleTree.GetHash(i);
            if (_archiveContents.ContainsKey(blockHash))
            {
                _archiveContents.Remove(blockHash);
            }
        }

        var map = GetArchiveMap(storage);
        map.Remove(archive.Hash);

        return true;
    }

    public bool HasArchiveBlock(IArchive archive, int blockIndex)
    {
        Throw.IfNull(archive, nameof(archive));
        Throw.If(blockIndex < 0 || blockIndex >= archive.BlockCount, "invalid block index");

        var hash = archive.MerkleTree.GetHash(blockIndex);
        return _archiveContents.ContainsKey(hash);
    }

    public void WriteArchiveBlock(IArchive archive, int blockIndex, byte[] content)
    {
        Throw.IfNull(archive, nameof(archive));
        Throw.IfNull(content, nameof(content));
        Throw.If(blockIndex < 0 || blockIndex >= archive.BlockCount, "invalid block index");

        var hash = MerkleTree.CalculateBlockHash(content);

        if (_archiveContents.ContainsKey(hash))
        {
            return;
        }

        if (!archive.MerkleTree.VerifyContent(hash, blockIndex))
        {
            throw new ArchiveException("Block content mismatch");
        }

        _archiveContents.Set(hash, content);

        archive.AddMissingBlock(blockIndex);
        ModifyArchive(RootStorage, archive);
    }

    public byte[] ReadArchiveBlock(IArchive archive, int blockIndex)
    {
        Throw.IfNull(archive, nameof(archive));
        Throw.If(blockIndex < 0 || blockIndex >= archive.BlockCount, "invalid block index");

        var hash = archive.MerkleTree.GetHash(blockIndex);

        if (_archiveContents.ContainsKey(hash))
        {
            return _archiveContents.Get(hash);
        }

        return null;
    }

    public void AddOwnerToArchive(StorageContext storage, IArchive archive, Address owner)
    {
        archive.AddOwner(owner);
        ModifyArchive(storage, archive);
    }

    public void RemoveOwnerFromArchive(StorageContext storage, IArchive archive, Address owner)
    {
        archive.RemoveOwner(owner);

        if (archive.OwnerCount <= 0)
        {
            DeleteArchive(storage, archive);
        }
        else
        {
            ModifyArchive(storage, archive);
        }
    }
    
    public IKeyValueStoreAdapter GetChainStorage(string name)
    {
        return this.CreateKeyStoreAdapter($"chain.{name}");
    }

    #endregion
}
