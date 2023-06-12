namespace Phantasma.Core.Storage.Context
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
