using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Phantasma.Core.Storage.Interfaces;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Storage;

public class BasicDiskStore : IKeyValueStoreAdapter
{
    private Dictionary<byte[], byte[]> _cache = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

    public uint Count => (uint)_cache.Count;

    private string fileName;

    public bool AutoFlush = true;

    public BasicDiskStore(string fileName)
    {
        this.fileName = fileName.Replace("\\", "/");

        var path = Path.GetDirectoryName(fileName);
        if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        if (File.Exists(fileName))
        {
            var lines = File.ReadAllLines(fileName);
            lock (_cache)
            {
                foreach (var line in lines)
                {
                    var temp = line.Split(',');
                    var key = Convert.FromBase64String(temp[0]);
                    var val = Convert.FromBase64String(temp[1]);
                    _cache[key] = val;
                }
            }
        }
    }

    public void Visit(Action<byte[], byte[]> visitor, ulong searchCount, byte[] prefix)
    {
        lock (_cache)
        {
            //TODO use prefix
            foreach (var entry in _cache)
            {
                visitor(entry.Key, entry.Value);
            }
        }
    }

    private void UpdateToDisk()
    {
        File.WriteAllLines(fileName,
            _cache.Select(x => Convert.ToBase64String((byte[])x.Key) + "," + Convert.ToBase64String((byte[])x.Value)));
    }

    public void SetValue(byte[] key, byte[] value)
    {
        Throw.IfNull(key, nameof(key));

        if (value == null || value.Length == 0)
        {
            Remove(key);
        }
        else
        {
            lock (_cache)
            {
                _cache[key] = value;
                if (AutoFlush)
                {
                    UpdateToDisk();
                }
            }
        }
    }

    public byte[] GetValue(byte[] key)
    {
        if (ContainsKey(key))
        {
            lock (_cache)
            {
                return _cache[key];
            }
        }

        return null;
    }

    public bool ContainsKey(byte[] key)
    {
        lock (_cache)
        {
            var result = _cache.ContainsKey(key);
            return result;
        }
    }

    public void Remove(byte[] key)
    {
        lock (_cache)
        {
            _cache.Remove(key);
            if (AutoFlush)
            {
                UpdateToDisk();
            }
        }
    }

    public void Flush()
    {
        if (!AutoFlush)
        {
            UpdateToDisk();
        }
    }
}
