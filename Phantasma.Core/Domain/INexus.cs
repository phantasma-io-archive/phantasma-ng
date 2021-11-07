using Phantasma.Core.Context;

namespace Phantasma.Core
{
    public interface INexus
    {
        void Attach(IOracleObserver observer);

        void Detach(IOracleObserver observer);

        void Notify(StorageContext storage);
    }
}

