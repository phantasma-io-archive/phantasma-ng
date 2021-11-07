using Phantasma.Core.Context;

namespace Phantasma.Core
{
    public interface IOracleObserver
    {
        void Update(INexus nexus, StorageContext storage);
    }
}

