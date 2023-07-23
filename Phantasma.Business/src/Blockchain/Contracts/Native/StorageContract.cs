using System;
using System.Numerics;
using Phantasma.Business.Blockchain.Archives;
using Phantasma.Business.VM;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Storage.Context.Structs;

namespace Phantasma.Business.Blockchain.Contracts.Native
{
    public sealed class StorageContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Storage;

        public const string KilobytesPerStakeTag = "storage.stake.kb";
        public const string FreeStoragePerContractTag = "storage.contract.kb";

        public static readonly BigInteger KilobytesPerStakeDefault = 40;
        public static readonly BigInteger FreeStoragePerContractDefault = 1024;
        
        public const int DefaultForeignSpacedPercent = 20;

        public const int MaxKeySize = 256;

#pragma warning disable 0649
        internal StorageMap _storageMap; //<Address, Collection<StorageEntry>>
        internal StorageMap _permissionMap; //<Address, Collection<StorageEntry>>
        internal StorageMap _dataQuotas; //<Address, BigInteger>
#pragma warning restore 0649

        public StorageContract() : base()
        {
        }

        /// <summary>
        /// Returns the size of storage for a stake amount
        /// </summary>
        /// <param name="stakeAmount"></param>
        /// <returns></returns>
        public BigInteger CalculateStorageSizeForStake(BigInteger stakeAmount)
        {
            var kilobytesPerStake = (int)Runtime.GetGovernanceValue(KilobytesPerStakeTag);
            var totalSize = stakeAmount * kilobytesPerStake * 1024;
            totalSize /= UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);

            return totalSize;
        }

        /// <summary>
        /// Method used to create a file in the storage.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="fileName"></param>
        /// <param name="fileSize"></param>
        /// <param name="contentMerkle"></param>
        /// <param name="encryptionContent"></param>
        public void CreateFile(Address target, string fileName, BigInteger fileSize, byte[] contentMerkle, byte[] encryptionContent)
        {
            Runtime.Expect(!Nexus.IsDangerousAddress(target), "this address can't be used as source");

            Runtime.Expect(Runtime.IsWitness(target), "invalid witness");
            Runtime.Expect(target.IsUser, "destination address must be user address");
            Runtime.Expect(fileSize >= DomainSettings.ArchiveMinSize, "file too small");
            Runtime.Expect(fileSize <= DomainSettings.ArchiveMaxSize, "file too big");

            var merkleTree = MerkleTree.FromBytes(contentMerkle);
            var archive = Runtime.GetArchive(merkleTree.Root);

            if (archive != null && archive.IsOwner(target))
            {
                return;
            }

            if (archive == null)
            {
                var encryption = ArchiveExtensions.ReadArchiveEncryption(encryptionContent);
                archive = Runtime.CreateArchive(merkleTree, target, fileName, fileSize, Runtime.Time, encryption);
            }

            AddFile(target, target, archive);
        }

        /// <summary>
        /// Checks if a file exists in the storage.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="hash"></param>
        /// <returns></returns>
        public bool HasFile(Address target, Hash hash)
        {
            var archive = Runtime.GetArchive(hash);
            return archive.IsOwner(target);
        }

        /// <summary>
        /// Adds a file to user storage. (Shared Storage)
        /// </summary>
        /// <param name="from"></param>
        /// <param name="target"></param>
        /// <param name="archiveHash"></param>
        public void AddFile(Address from, Address target, Hash archiveHash)
        {
            var archive = Runtime.GetArchive(archiveHash);
            AddFile(from, target, archive);
        }

        /// <summary>
        /// Adds a file to user storage. (Shared Storage)
        /// </summary>
        /// <param name="from"></param>
        /// <param name="target"></param>
        /// <param name="archive"></param>
        private void AddFile(Address from, Address target, IArchive archive)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(HasPermission(from, target), $"permissions missing for {from} to add file to {target}");

            Runtime.Expect(target.IsUser, "destination address must be user address");

            Runtime.Expect(archive != null, "archive does not exist");
            
            BigInteger requiredSize = archive.Size;

            var targetUsedSize = GetUsedSpace(target);
            var targetStakedAmount = Runtime.GetStake(target);
            var targetAvailableSize = CalculateStorageSizeForStake(targetStakedAmount);
            targetAvailableSize -= targetUsedSize;

            Runtime.Expect(targetAvailableSize >= requiredSize, "target account does not have available space");

            if (!archive.IsOwner(target))
            {
                Runtime.AddOwnerToArchive(archive.Hash, target);
            }

            var list = _storageMap.Get<Address, StorageList>(target);
            list.Add(archive.Hash);
        }

        /// <summary>
        /// Deletes a file from the storage.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="targetHash"></param>
        public void DeleteFile(Address from, Hash targetHash)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            var list = _storageMap.Get<Address, StorageList>(from);

            int targetIndex = -1;
            var count = list.Count();
            for (int i = 0; i < count; i++)
            {
                var entry = list.Get<Hash>(i);
                if (entry == targetHash)
                {
                    targetIndex = i;
                    break;
                }
            }

            Runtime.Expect(targetIndex >= 0, "archive not found");

            Runtime.Expect(Runtime.RemoveOwnerFromArchive(targetHash, from), "owner removal failed");
            list.RemoveAt(targetIndex);
        }
        
        /// <summary>
        ///  Checks if external address has permission to add files to target address
        /// </summary>
        /// <param name="external"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool HasPermission(Address external, Address target)
        {
            if (external == target)
            {
                return true;
            }

            var permissions = _permissionMap.Get<Address, StorageList>(target);
            return permissions.Contains(external);
        }

        /// <summary>
        /// Adds a permission to an external address to add files to target address
        /// </summary>
        /// <param name="from"></param>
        /// <param name="externalAddr"></param>
        public void AddPermission(Address from, Address externalAddr)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(from != externalAddr, "target must be different");

            var permissions = _permissionMap.Get<Address, StorageList>(from);
            Runtime.Expect(!permissions.Contains(externalAddr), $"permission already exists");

            permissions.Add(externalAddr);

            Runtime.Notify(EventKind.AddressLink, from, externalAddr);
        }

        /// <summary>
        /// Deletes a permission to an external address to add files to target address.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="externalAddr"></param>
        public void DeletePermission(Address from, Address externalAddr)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(from != externalAddr, "target must be different");

            var permissions = _permissionMap.Get<Address, StorageList>(from);
            Runtime.Expect(permissions.Contains(externalAddr), $"permission does not exist");

            permissions.Remove(externalAddr);

            Runtime.Notify(EventKind.AddressUnlink, from, externalAddr);
        }

        /// <summary>
        /// Migrate permissions from one address to another.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="oldAddr"></param>
        /// <param name="newAddr"></param>
        public void MigratePermission(Address target, Address oldAddr, Address newAddr)
        {
            Runtime.Expect(Runtime.IsWitness(oldAddr), "invalid witness");

            var permissions = _permissionMap.Get<Address, StorageList>(target);

            if (target != oldAddr)
            {
                Runtime.Expect(HasPermission(oldAddr, target), $"not permissions from {oldAddr} for target {target}");
                permissions.Remove(oldAddr);
                Runtime.Notify(EventKind.AddressUnlink, target, oldAddr);
            }

            if (newAddr != target)
            {
                Runtime.Expect(!HasPermission(newAddr, target), $"{newAddr} already has permissions for target {target}");
                permissions.Add(newAddr);
                Runtime.Notify(EventKind.AddressLink, target, newAddr);
            }
        }

        /// <summary>
        /// Migrate storage from one address to another.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="target"></param>
        public void Migrate(Address from, Address target)
        {
            Runtime.Expect(Runtime.PreviousContext.Name == "account", "invalid context");

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(!_dataQuotas.ContainsKey(target), "target address already in use");
            _dataQuotas.Migrate<Address, BigInteger>(from, target);

            _permissionMap.Migrate<Address, StorageList>(from, target);
            _storageMap.Migrate<Address, StorageList>(from, target);
        }

        /// <summary>
        /// Returns the used space for an address.
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        public BigInteger GetUsedSpace(Address from)
        {
            //if (!_storageMap.ContainsKey<Address>(from))
            //{
            //    return 0;
            //}

            var hashes = GetFiles(from);
            BigInteger usedSize = 0;
            var count = hashes.Length;
            for (int i = 0; i < count; i++)
            {
                var hash = hashes[i];
                var archive = Runtime.GetArchive(hash);

                // NOTE not throwing here allows accounts with this issue to still function
                //Runtime.Expect(archive != null, "missing archive");

                if (archive != null)
                {
                    usedSize += archive.Size;
                }
            }

            var usedQuota = GetUsedDataQuota(from);
            usedSize += usedQuota;

            return usedSize;
        }

        /// <summary>
        /// Returns the available space for an address.
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        public BigInteger GetAvailableSpace(Address from)
        {
            var stakedAmount = Runtime.GetStake(from);
            var totalSize = CalculateStorageSizeForStake(stakedAmount);

            if (from.IsSystem)
            {
                totalSize += Runtime.GetGovernanceValue(FreeStoragePerContractTag);
            }

            var usedSize = GetUsedSpace(from);
            Runtime.Expect(usedSize <= totalSize, "error in storage size calculation");
            return totalSize - usedSize;
        }

        /// <summary>
        /// Gets the list of files for an address.
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        public Hash[] GetFiles(Address from)
        {
            //Runtime.Expect(_storageMap.ContainsKey<Address>(from), "no files available for address");
            var list = _storageMap.Get<Address, StorageList>(from);
            return list.All<Hash>();
        }
        
        /// <summary>
        /// Validate's a key
        /// </summary>
        /// <param name="key"></param>
        private void ValidateKey(byte[] key)
        {
            Runtime.Expect(key.Length > 0 && key.Length <= MaxKeySize, "invalid key");

            var firstChar = (char)key[0];
            Runtime.Expect(firstChar != '.', "permission denied"); // NOTE link correct PEPE here
        }

        /// <summary>
        /// Returns the data quota for an address.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public BigInteger GetUsedDataQuota(Address address)
        {
            var result = _dataQuotas.Get<Address, BigInteger>(address);
            return result;
        }

        [Obsolete("Method is obsolete.", false)]
        public void WriteData(Address target, byte[] key, byte[] value)
        {
            Runtime.Expect(Runtime.ProtocolVersion <= 12, "Method was deprecated in protocol version 13");
            ValidateKey(key);

            var usedQuota = _dataQuotas.Get<Address, BigInteger>(target);

            BigInteger deleteSize = 0;
            if (Runtime.Storage.Has(key))
            {
                var oldData = Runtime.Storage.Get(key);
                deleteSize = oldData.Length;
            }

            var writeSize = value.Length;
            if (writeSize > deleteSize)
            {
                var diff = writeSize - deleteSize;
                var availableSize = GetAvailableSpace(target);
                Runtime.Expect(availableSize >= diff, $"not enough storage space available: requires " + diff + ", only have: " + availableSize);
            }

            Runtime.Storage.Put(key, value);

            usedQuota -= deleteSize;
            usedQuota += writeSize;

            if (usedQuota <= 0)
            {
                usedQuota = writeSize; // fix for data written in previous protocol
            }

            _dataQuotas.Set(target, usedQuota);

            _dataQuotas.Set(target, usedQuota);

            var temp = Runtime.Storage.Get(key);
            Runtime.Expect(temp.Length == value.Length, "storage write corruption");
        }

        [Obsolete("Method is obsolete.", false)]
        public void DeleteData(Address target, byte[] key)
        {
            Runtime.Expect(Runtime.ProtocolVersion <= 12, "Method was deprecated in protocol version 13");

            ValidateKey(key);

            Runtime.Expect(Runtime.Storage.Has(key), "key does not exist");

            var value = Runtime.Storage.Get(key);
            var deleteSize = value.Length;

            Runtime.Storage.Delete(key);

            var usedQuota = _dataQuotas.Get<Address, BigInteger>(target);
            usedQuota -= deleteSize;

            if (usedQuota < 0)
            {
                usedQuota = 0;
            }

            _dataQuotas.Set(target, usedQuota);
        }

        /// <summary>
        /// Calculate the required storage size for a given content size.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="contentSize"></param>
        /// <returns></returns>
        public static BigInteger CalculateRequiredSize(string fileName, BigInteger contentSize) => contentSize + Hash.Length + fileName.Length;
    }
}
