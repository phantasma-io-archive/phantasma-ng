using Phantasma.Core.Domain;

namespace Phantasma.Core.Storage.Context;

public class StorageCollection
{
    private readonly INexus _nexus;
    public StorageContext MainStorage { get; private set; }
    public StorageContext OrganizationStorage { get; private set; }
    public StorageContext TransactionsStorage { get; private set; }
    public StorageContext BlocksStorage { get; private set; }
    public StorageContext BlocksTransactionsStorage { get; private set; }
    public StorageContext ContractsStorage { get; private set; }
    public StorageContext TasksStorage { get; private set; }
    public StorageContext AddressStorage { get; private set; }
    public StorageContext AddressBalancesStorage { get; private set; }
    public StorageContext AddressBalancesNFTStorage { get; private set; }
    public StorageContext AddressTransactionsStorage { get; private set; }
    public StorageContext ArchiveStorage { get; private set; }
    public StorageContext PlatformsStorage { get; private set; }

    public StorageCollection(INexus Nexus, string name)
    {
        _nexus = Nexus;
        this.MainStorage = (StorageContext)new KeyStoreStorage(Nexus.GetChainStorage(name));
        this.TransactionsStorage = (StorageContext)new KeyStoreStorage(Nexus.GetChainStorage("txs"));
        this.OrganizationStorage = (StorageContext)new KeyStoreStorage(Nexus.GetChainStorage("organization"));
        this.BlocksStorage = (StorageContext)new KeyStoreStorage(Nexus.GetChainStorage("blocks"));
        this.BlocksTransactionsStorage = (StorageContext)new KeyStoreStorage(Nexus.GetChainStorage("blocks.txs"));
        this.ContractsStorage = (StorageContext)new KeyStoreStorage(Nexus.GetChainStorage("contracts"));
        this.TasksStorage = (StorageContext)new KeyStoreStorage(Nexus.GetChainStorage("tasks"));
        this.AddressStorage = (StorageContext)new KeyStoreStorage(Nexus.GetChainStorage("address"));
        this.AddressBalancesStorage = (StorageContext)new KeyStoreStorage(Nexus.GetChainStorage("address.balances"));
        this.AddressBalancesNFTStorage = (StorageContext)new KeyStoreStorage(Nexus.GetChainStorage("address.balances.nft"));
        this.AddressTransactionsStorage = (StorageContext)new KeyStoreStorage(Nexus.GetChainStorage("address.txs"));
        this.ArchiveStorage = (StorageContext)new KeyStoreStorage(Nexus.GetChainStorage("archive"));
        this.PlatformsStorage = (StorageContext)new KeyStoreStorage(Nexus.GetChainStorage("Platforms"));
    }
}
