using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Serializer;
using Phantasma.Core.Storage.Context.Structs;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Storage.Context;

public class StorageChangeSetContext : StorageContext, ISerializable
{
    public StorageContext baseContext { get; private set; }

    private readonly Dictionary<StorageKey, StorageChangeSetEntry> _entries =
        new Dictionary<StorageKey, StorageChangeSetEntry>(new StorageKeyComparer());

    public StorageChangeSetContext(StorageContext baseContext)
    {
        Throw.IfNull(baseContext, "base context");
        this.baseContext = baseContext;
    }

    public StorageChangeSetContext Clone()
    {
        return new StorageChangeSetContext(this);
    }

    public override void Clear()
    {
        _entries.Clear();
    }

    public override bool Has(StorageKey key)
    {
        if (_entries.ContainsKey(key))
        {
            var entry = _entries[key];
            return entry.newValue != null;
        }

        return baseContext.Has(key);
    }

    public override byte[] Get(StorageKey key)
    {
        if (_entries.ContainsKey(key))
        {
            return _entries[key].newValue;
        }

        return baseContext.Get(key);
    }

    public override void Put(StorageKey key, byte[] newValue)
    {
        StorageChangeSetEntry change;

        if (_entries.ContainsKey(key))
        {
            change = _entries[key];
            change.newValue = newValue;
        }
        else
        {
            byte[] oldValue;

            if (baseContext.Has(key))
            {
                oldValue = baseContext.Get(key);
            }
            else
            {
                oldValue = null;
            }

            change = new StorageChangeSetEntry()
            {
                oldValue = oldValue,
                newValue = newValue,
            };
        }

        _entries[key] = change;
    }

    public override void Delete(StorageKey key)
    {
        Put(key, null);
    }

    public void Execute()
    {
        foreach (var entry in _entries)
        {
            if (entry.Value.newValue == null)
            {
                baseContext.Delete(entry.Key);
            }
            else
            {
                baseContext.Put(entry.Key, entry.Value.newValue);
            }
        }
    }

    public void Undo()
    {
        foreach (var entry in _entries)
        {
            if (entry.Value.oldValue == null)
            {
                baseContext.Delete(entry.Key);
            }
            else
            {
                baseContext.Put(entry.Key, entry.Value.oldValue);
            }
        }
    }

    public bool Any()
    {
        return _entries.Count > 0;
    }

    public override uint Count()
    {
        return (uint)_entries.Count;
    }

    public override void Visit(Action<byte[], byte[]> visitor, ulong searchCount = 0, byte[] prefix = null)
    {
        ulong count = 0;
        // only used to track findings, to not overwrite them
        var found = new Dictionary<byte[], byte[]>();

        if (searchCount == 0)
        {
            return;
        }

        foreach (var entry in _entries)
        {
            var entryPrefix = entry.Key.keyData.Take(prefix.Length);
            if (count <= searchCount && entryPrefix.SequenceEqual(prefix))
            {
                found.Add(entry.Key.keyData, null);
                visitor(entry.Key.keyData, entry.Value.newValue);
                count++;
            }

            if (count > searchCount)
            {
                return;
            }
        }

        baseContext.Visit((key, value) =>
        {
            var entryPrefix = key.Take(prefix.Length);
            if (count <= searchCount && entryPrefix.SequenceEqual(prefix))
            {
                if (!found.ContainsKey(key))
                {
                    visitor(key, value);
                    count++;
                }
            }

            if (count > searchCount)
            {
                return;
            }
        }, searchCount, prefix);
    }

    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteVarInt(_entries.Count);
        foreach (KeyValuePair<StorageKey, StorageChangeSetEntry> valuePair in _entries)
        {
            var keySerialized = valuePair.Key.Serialize();
            var valueSerialized = valuePair.Value.Serialize();
            writer.WriteVarInt(keySerialized.Length);
            writer.Write(keySerialized);
            writer.WriteVarInt(valueSerialized.Length);
            writer.Write(valueSerialized);
        }
    }

    public void UnserializeData(BinaryReader reader)
    {
        _entries.Clear();
        var count = (int)reader.ReadVarInt();
        for (int i = 0; i < count; i++)
        {
            var lengthKey = reader.ReadVarInt();
            StorageKey key = Serialization.Unserialize<StorageKey>(reader.ReadBytes((int)lengthKey));
            var lengthValue = reader.ReadVarInt();
            StorageChangeSetEntry entry =
                Serialization.Unserialize<StorageChangeSetEntry>(reader.ReadBytes((int)lengthValue));
            _entries.Add(key, entry);
        }
    }
}
