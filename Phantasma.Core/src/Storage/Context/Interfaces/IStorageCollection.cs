namespace Phantasma.Core.Storage.Context.Interfaces
{
    public interface IStorageCollection
    {
        byte[] BaseKey { get; }
        StorageContext Context { get; }
    }
}
