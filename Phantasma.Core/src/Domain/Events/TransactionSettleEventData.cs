using Phantasma.Core.Cryptography;

namespace Phantasma.Core.Domain.Events;

public struct TransactionSettleEventData
{
    public readonly Hash Hash;
    public readonly string Platform;
    public readonly string Chain;

    public TransactionSettleEventData(Hash hash, string platform, string chain)
    {
        Hash = hash;
        Platform = platform;
        Chain = chain;
    }
}
