using System;

namespace Phantasma.Core.Storage;

public interface IKeyValueStoreAdapter
{
    void SetValue(byte[] key, byte[] value);
    byte[] GetValue(byte[] key);
    bool ContainsKey(byte[] key);
    void Remove(byte[] key);
    uint Count { get; }
    void Visit(Action<byte[], byte[]> visitor, ulong searchCount, byte[] prefix);
}
