namespace Phantasma.Core.Storage.Context
{
    public interface IStorageCollection
    {
        byte[] BaseKey { get; }
        StorageContext Context { get; }
    }
}
