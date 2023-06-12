using System.Text;
using Phantasma.Core.Storage.Context.Interfaces;

namespace Phantasma.Core.Storage.Context.Structs
{
    public struct StorageMap : IStorageCollection
    {
        public StorageMap(string baseKey, StorageContext context) : this(Encoding.UTF8.GetBytes(baseKey), context)
        {
        }

        public StorageMap(byte[] baseKey, StorageContext context) : this()
        {
            BaseKey = baseKey;
            Context = context;
        }

        public byte[] BaseKey { get; }
        public StorageContext Context { get; }
    }
}
