using System.Numerics;
using Phantasma.Business.Blockchain.Archives;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Types.Structs;

namespace Phantasma.Business.Blockchain.VM;

public partial class RuntimeVM : GasMachine, IRuntime
{
    /// <summary>
    /// Create a new archive
    /// </summary>
    /// <param name="merkleTree"></param>
    /// <param name="owner"></param>
    /// <param name="name"></param>
    /// <param name="size"></param>
    /// <param name="time"></param>
    /// <param name="encryption"></param>
    /// <returns></returns>
    public IArchive CreateArchive(MerkleTree merkleTree, Address owner, string name, BigInteger size,
        Timestamp time, IArchiveEncryption encryption)
    {
        //TODO check valid values of merkleTree, encryption
        ExpectAddressSize(owner, nameof(owner));
        ExpectNameLength(name, nameof(name));

        // TODO validation
        var archive = Nexus.CreateArchive(RootStorage, merkleTree, owner, name, size, time, encryption);

        this.Notify(EventKind.FileCreate, owner, archive.Hash);

        return archive;
    }

    /// <summary>
    /// Write data to an archive
    /// </summary>
    /// <param name="archive"></param>
    /// <param name="blockIndex"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    public bool WriteArchive(IArchive archive, int blockIndex, byte[] data)
    {
        //TODO: archive validation
        ExpectArchiveLength(data, nameof(data));

        if (archive == null)
        {
            return false;
        }

        var blockCount = (int)archive.GetBlockCount();
        if (blockIndex < 0 || blockIndex >= blockCount)
        {
            return false;
        }

        Nexus.WriteArchiveBlock((Archive)archive, blockIndex, data);
        return true;
    }
    
    /// <summary>
    /// Checks if an archive exists
    /// </summary>
    /// <param name="hash"></param>
    /// <returns></returns>
    public bool ArchiveExists(Hash hash)
    {
        ExpectHashSize(hash, nameof(hash));
        return Nexus.ArchiveExists(RootStorage, hash);
    }

    /// <summary>
    /// Gets an archive
    /// </summary>
    /// <param name="hash"></param>
    /// <returns></returns>
    public IArchive GetArchive(Hash hash)
    {
        ExpectHashSize(hash, nameof(hash));
        return Nexus.GetArchive(RootStorage, hash);
    }

    /// <summary>
    /// Delete an archive
    /// </summary>
    /// <param name="hash"></param>
    /// <returns></returns>
    public bool DeleteArchive(Hash hash)
    {
        ExpectHashSize(hash, nameof(hash));

        var archive = Nexus.GetArchive(RootStorage, hash);
        if (archive == null)
        {
            return false;
        }

        return Nexus.DeleteArchive(RootStorage, archive);
    }

    /// <summary>
    /// Add an owner to an archive
    /// </summary>
    /// <param name="hash"></param>
    /// <param name="address"></param>
    /// <returns></returns>
    public bool AddOwnerToArchive(Hash hash, Address address)
    {
        ExpectHashSize(hash, nameof(hash));
        ExpectAddressSize(address, nameof(address));

        var archive = Nexus.GetArchive(RootStorage, hash);
        if (archive == null)
        {
            return false;
        }

        Nexus.AddOwnerToArchive(RootStorage, archive, address);

        this.Notify(EventKind.OwnerAdded, address, hash.ToByteArray());

        return true;
    }

    /// <summary>
    /// Remove an owner from an archive
    /// </summary>
    /// <param name="hash"></param>
    /// <param name="address"></param>
    /// <returns></returns>
    public bool RemoveOwnerFromArchive(Hash hash, Address address)
    {
        ExpectHashSize(hash, nameof(hash));
        ExpectAddressSize(address, nameof(address));

        var archive = Nexus.GetArchive(RootStorage, hash);
        if (archive == null)
        {
            return false;
        }

        Nexus.RemoveOwnerFromArchive(RootStorage, archive, address);

        if (archive.OwnerCount == 0)
        {
            this.Notify(EventKind.FileDelete, address, hash.ToByteArray());
        }
        else
        {
            this.Notify(EventKind.OwnerRemoved, address, hash.ToByteArray());
        }

        return true;
    }
}
