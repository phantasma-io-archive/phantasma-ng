using System.Text;

namespace Phantasma.Core.Storage.Context
{
    public struct StorageList : IStorageCollection
    {
        public StorageList(string baseKey, StorageContext context) : this(Encoding.UTF8.GetBytes(baseKey), context)
        {
        }

        public StorageList(byte[] baseKey, StorageContext context) : this()
        {
            BaseKey = baseKey;
            Context = context;
        }

        public byte[] BaseKey { get; }
        public StorageContext Context { get; }
    }
}
