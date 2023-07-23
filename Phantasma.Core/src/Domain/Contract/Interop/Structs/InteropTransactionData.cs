using System.Collections.Generic;
using System.Numerics;
using Phantasma.Core.Cryptography.Structs;

namespace Phantasma.Core.Domain.Contract.Interop.Structs;

public class InteropTransactionData
{
    public Hash Hash;
    public string TransactionHash { get; set; }
    public string BlockHash { get; set; }
    public string ContractAddress { get; set; }
    public BigInteger Gas { get; set; }
    public BigInteger GasPrice { get; set; }
    public BigInteger Status { get; set; }
    public List<InteropTransfer> Transfers { get; set; }
    
    // SwapIn
    // ClaimTokens
    // SwapOut
}
