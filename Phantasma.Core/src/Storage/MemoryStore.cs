using System;
using System.Collections.Generic;
using System.Linq;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Storage;

public class MemoryStore : IKeyValueStoreAdapter
{
    private Dictionary<byte[], byte[]> _entries = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

    public uint Count => (uint)_entries.Count;

    public MemoryStore()
    {
    }

    public void SetValue(byte[] key, byte[] value)
    {
        if (value == null || value.Length == 0)
        {
            Remove(key);
            return;
        }

        _entries[key] = value;
    }

    public byte[] GetValue(byte[] key)
    {
        if (ContainsKey(key))
        {
            return _entries[key];
        }

        return null;
    }

    public bool ContainsKey(byte[] key)
    {
        var result = _entries.ContainsKey(key);
        return result;
    }

    public void Remove(byte[] key)
    {
        _entries.Remove(key);
    }

    public void Visit(Action<byte[], byte[]> visitor, ulong searchCount, byte[] prefix)
    {
        ulong count = 0;
        foreach(var entry in _entries)
        {
            var entryPrefix = entry.Key.Take(prefix.Length);
            if (count <= searchCount && entryPrefix.SequenceEqual(prefix))
            {
                visitor(entry.Key, entry.Value);
                count++;
            }

            if (count > searchCount)
                break;
        }
    }
}
