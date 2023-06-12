using Phantasma.Core.Storage.Context;

namespace Phantasma.Core.Domain.Contract.Privacy;

internal struct PrivacyQueue
{
    public uint ID;
    public int size;
    public StorageList addresses; //<Address>
    public StorageList signatures; //<RingSignature>
}
