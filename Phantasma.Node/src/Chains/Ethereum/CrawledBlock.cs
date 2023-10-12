using System.Collections.Generic;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Contract.Interop.Structs;

namespace Phantasma.Node.Chains.Ethereum;

public class CrawledBlock
{
    public Hash Hash { get; }
    public Dictionary<string, Dictionary<string, List<InteropTransfer>>> Transfers { get; }

    public CrawledBlock(Hash hash, Dictionary<string, Dictionary<string, List<InteropTransfer>>> transfers)
    {
        Hash = hash;
        Transfers = transfers;
    }
}
