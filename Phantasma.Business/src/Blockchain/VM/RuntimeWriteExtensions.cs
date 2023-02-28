using System.Numerics;
using System.Text;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Business.VM;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Utils;

namespace Phantasma.Business.Blockchain.VM;

public static class RuntimeWriteExtensions
{
    /// <summary>
    /// Get the used Key quota for a given address
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    private static byte[] GetUsedQuotaKey(Address target)
    {
        return SmartContract.GetKeyForField(NativeContractKind.Storage, "_dataQuotas", true); ;
    }

    /// <summary>
    /// Validate the key
    /// </summary>
    /// <param name="Runtime"></param>
    /// <param name="key"></param>
    private static void ValidateKey(IRuntime Runtime, byte[] key)
    {
        Runtime.Expect(key.Length > 0 && key.Length <= StorageContract.MaxKeySize, "invalid key");
        var firstChar = (char)key[0];
        Runtime.Expect(firstChar != '.', "permission denied"); // NOTE link correct PEPE here
    }

    /// <summary>
    /// Set the used quota for a given address
    /// </summary>
    /// <param name="Runtime"></param>
    /// <param name="target"></param>
    /// <param name="usedQuota"></param>
    private static void SetDataQuotas(this IRuntime Runtime, Address target, BigInteger usedQuota)
    {
        //Runtime.Expect(Runtime.CurrentContext.Name != VirtualMachine.EntryContextName, "invalid context");
        Runtime.Expect(usedQuota >= 0, "invalid used quota");
        var storage = new StorageMap(GetUsedQuotaKey(target), Runtime.Storage);
        storage.Set(target, usedQuota);
    }

    /// <summary>
    /// Write data to the storage of a given address
    /// </summary>
    /// <param name="Runtime"></param>
    /// <param name="target"></param>
    /// <param name="Key"></param>
    /// <param name="value"></param>
    internal static void WriteData(this IRuntime Runtime, Address target, byte[] Key, byte[] value)
    {
        ValidateKey(Runtime, Key);

        var usedQuota = Runtime.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.GetUsedDataQuota), target).AsNumber();

        BigInteger deleteSize = 0;
        if (Runtime.Storage.Has(Key))
        {
            var oldData = Runtime.Storage.Get(Key);
            deleteSize = oldData.Length;
        }

        var writeSize = value.Length;
        if (writeSize > deleteSize)
        {
            var diff = writeSize - deleteSize;
            var availableSize = Runtime.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.GetAvailableSpace), target).AsNumber();
            Runtime.Expect(availableSize >= diff, $"not enough storage space available: requires " + diff + ", only have: " + availableSize);
        }

        Runtime.Storage.Put(Key, value);

        usedQuota -= deleteSize;
        usedQuota += writeSize;

        if (usedQuota <= 0)
        {
            usedQuota = writeSize; // fix for data written in previous protocol
        }

        Runtime.SetDataQuotas(target, usedQuota);

        var temp = Runtime.Storage.Get(Key);
        Runtime.Expect(temp.Length == value.Length, "storage write corruption");
    }

    /// <summary>
    /// Delete data from the storage of a given address
    /// </summary>
    /// <param name="Runtime"></param>
    /// <param name="target"></param>
    /// <param name="key"></param>
    internal static void DeleteData(this IRuntime Runtime, Address target, byte[] key)
    {
        ValidateKey(Runtime, key);

        Runtime.Expect(Runtime.Storage.Has(key), "key does not exist");

        var value = Runtime.Storage.Get(key);
        var deleteSize = value.Length;

        Runtime.Storage.Delete(key);

        var usedQuota = Runtime.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.GetUsedDataQuota), target).AsNumber();
        usedQuota -= deleteSize;

        if (usedQuota < 0)
        {
            usedQuota = 0;
        }

        Runtime.SetDataQuotas(target, usedQuota);
    }
}
