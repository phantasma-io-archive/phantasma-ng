using Phantasma.Core.Storage.Context;

namespace Phantasma.Core.Domain
{
    public interface IOracleObserver
    {
        void Update(INexus nexus, StorageContext storage);
    }
}

