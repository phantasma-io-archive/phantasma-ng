using System;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Serializer;

namespace Phantasma.Core.Storage.Context;

public static class ValueUtils
{
    public static void Set<T>(this StorageValue value, T element)
    {
        byte[] bytes;
        if (typeof(IStorageCollection).IsAssignableFrom(typeof(T)))
        {
            var collection = (IStorageCollection)element;
            //bytes = MergeKey(map.BaseKey, key);
            bytes = collection.BaseKey;
        }
        else
        {
            bytes = Serialization.Serialize(element);
        }

        value.Context.Put(value.BaseKey, bytes);
    }

    public static T Get<T>(this StorageValue value)
    {
        var bytes = value.Context.Get(value.BaseKey);

        if (typeof(IStorageCollection).IsAssignableFrom(typeof(T)))
        {
            var args = new object[] { bytes, value.Context };
            var obj = (T)Activator.CreateInstance(typeof(T), args);
            return obj;
        }
        else
        {
            return Serialization.Unserialize<T>(bytes);
        }
    }

    public static bool HasValue(this StorageValue value)
    {
        return value.Context.Has(value.BaseKey);
    }

    public static void Clear(this StorageValue value)
    {
        value.Context.Delete(value.BaseKey);
    }
}
