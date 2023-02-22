using Phantasma.Core.Domain;

namespace Phantasma.Core.Storage.Context;

public class StorageCollectionChangeSet
{
    private readonly INexus _nexus;
    public StorageChangeSetContext MainStorage { get; private set; }
    public StorageChangeSetContext OrganizationStorage { get; private set; }
    public StorageChangeSetContext TransactionsStorage { get; private set; }
    public StorageChangeSetContext BlocksStorage { get; private set; }
    public StorageChangeSetContext BlocksTransactionsStorage { get; private set; }
    public StorageChangeSetContext ContractsStorage { get; private set; }
    public StorageChangeSetContext TasksStorage { get; private set; }
    public StorageChangeSetContext AddressStorage { get; private set; }
    public StorageChangeSetContext AddressBalancesStorage { get; private set; }
    public StorageChangeSetContext AddressBalancesNFTStorage { get; private set; }
    public StorageChangeSetContext AddressTransactionsStorage { get; private set; }
    public StorageChangeSetContext ArchiveStorage { get; private set; }
    public StorageChangeSetContext PlatformsStorage { get; private set; }

    public StorageCollectionChangeSet( StorageCollection collection)
    {
        this.MainStorage = new StorageChangeSetContext(collection.MainStorage);
        this.TransactionsStorage = new StorageChangeSetContext(collection.TransactionsStorage);
        this.OrganizationStorage = new StorageChangeSetContext(collection.OrganizationStorage);
        this.BlocksStorage = new StorageChangeSetContext(collection.BlocksStorage);
        this.BlocksTransactionsStorage = new StorageChangeSetContext(collection.BlocksTransactionsStorage);
        this.ContractsStorage =new StorageChangeSetContext(collection.ContractsStorage);
        this.TasksStorage = new StorageChangeSetContext(collection.TasksStorage);
        this.AddressStorage = new StorageChangeSetContext(collection.AddressStorage);
        this.AddressBalancesStorage = new StorageChangeSetContext(collection.AddressBalancesStorage);
        this.AddressBalancesNFTStorage = new StorageChangeSetContext(collection.AddressBalancesNFTStorage);
        this.AddressTransactionsStorage = new StorageChangeSetContext(collection.AddressTransactionsStorage);
        this.ArchiveStorage = new StorageChangeSetContext(collection.ArchiveStorage);
        this.PlatformsStorage = new StorageChangeSetContext(collection.PlatformsStorage);
    }

    public void Execute()
    {
        this.MainStorage.Execute();
        this.TransactionsStorage.Execute();
        this.OrganizationStorage.Execute();
        this.BlocksStorage.Execute();
        this.BlocksTransactionsStorage.Execute();
        this.ContractsStorage.Execute();
        this.TasksStorage.Execute();
        this.AddressStorage.Execute();
        this.AddressBalancesStorage.Execute();
        this.AddressBalancesNFTStorage.Execute();
        this.AddressTransactionsStorage.Execute();
        this.ArchiveStorage.Execute();
        this.PlatformsStorage.Execute();
    }

    public void Clone()
    {
    }
}
