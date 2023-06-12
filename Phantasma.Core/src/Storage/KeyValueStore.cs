using System;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Serializer;

namespace Phantasma.Core.Storage
{
    public class KeyValueStore<K, V>
    {
        public readonly string Name;

        public readonly IKeyValueStoreAdapter Adapter;

        public uint Count => Adapter.Count;

        public KeyValueStore()
        {
        }

        public KeyValueStore(IKeyValueStoreAdapter adapter)
        {
            Adapter = adapter;
        }

        public V this[K key]
        {
            get { return Get(key); }
            set { Set(key, value); }
        }

        public void Set(K key, V value)
        {
            var keyBytes = Serialization.Serialize(key);
            var valBytes = Serialization.Serialize(value);
            Adapter.SetValue(keyBytes, valBytes);
        }

        public bool TryGet(K key, out V value)
        {
            var keyBytes = Serialization.Serialize(key);
            var bytes = Adapter.GetValue(keyBytes);
            if (bytes == null)
            {
                value = default(V);
               return false;
            }
            value = Serialization.Unserialize<V>(bytes);
            return true;
        }

        public V Get(K key)
        {
            var keyBytes = Serialization.Serialize(key);
            var bytes = Adapter.GetValue(keyBytes);
            if (bytes == null)
            {
                Throw.If(bytes == null, "item not found in keystore");

            }
            return Serialization.Unserialize<V>(bytes);
        }

        public bool ContainsKey(K key)
        {
            var keyBytes = Serialization.Serialize(key);
            return Adapter.ContainsKey(keyBytes);
        }

        public void Remove(K key)
        {
            var keyBytes = Serialization.Serialize(key);
            Adapter.Remove(keyBytes);
        }

        public void Visit(Action<K, V> visitor, ulong searchCount = 0, byte[] prefix = null)
        {
            Adapter.Visit((keyBytes, valBytes) =>
            {
                var key = Serialization.Unserialize<K>(keyBytes);
                var val = Serialization.Unserialize<V>(valBytes);
                visitor(key, val);
            }, searchCount, prefix);
        }
    }
}
