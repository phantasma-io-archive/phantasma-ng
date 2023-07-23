using Phantasma.Core.Storage.Context.Interfaces;

namespace Phantasma.Core.Storage.Context.Structs
{
    public struct StorageValue : IStorageCollection
    {
        public StorageValue(byte[] baseKey, StorageContext context) : this()
        {
            BaseKey = baseKey;
            Context = context;
        }

        public byte[] BaseKey { get; }
        public StorageContext Context { get; }
    }
}
