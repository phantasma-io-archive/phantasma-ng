using Phantasma.Core.Storage.Context.Structs;

namespace Phantasma.Core.Domain.Contract.Privacy.Structs;

internal struct PrivacyQueue
{
    public uint ID;
    public int size;
    public StorageList addresses; //<Address>
    public StorageList signatures; //<RingSignature>
}
