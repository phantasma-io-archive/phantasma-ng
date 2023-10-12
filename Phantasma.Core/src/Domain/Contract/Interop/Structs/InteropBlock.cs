using Phantasma.Core.Cryptography.Structs;

namespace Phantasma.Core.Domain.Contract.Interop.Structs;

public class InteropBlock
{
    public readonly string Platform;
    public readonly string Chain;
    public readonly Hash Hash;
    public readonly Hash[] Transactions;

    public InteropBlock()
    {

    }

    public InteropBlock(string platform, string chain, Hash hash, Hash[] transactions)
    {
        Platform = platform;
        Chain = chain;
        Hash = hash;
        Transactions = transactions;
    }
}
