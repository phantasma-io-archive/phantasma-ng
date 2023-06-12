using System.Collections.Generic;
using System.Linq;
using Phantasma.Core.Cryptography;

namespace Phantasma.Core.Domain.Contract.Interop;

public class InteropTransaction
{
    public readonly Hash Hash;        
    public readonly InteropTransfer[] Transfers;

    public InteropTransaction()
    {

    }

    public InteropTransaction(Hash hash, IEnumerable<InteropTransfer> transfers)
    {
        Hash = hash;
        this.Transfers = transfers.ToArray();
    }
}
