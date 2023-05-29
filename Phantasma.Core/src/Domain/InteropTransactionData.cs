using System.Numerics;
using Phantasma.Core.Cryptography;

namespace Phantasma.Core.Domain;

public class InteropTransactionData
{
    public Hash Hash;
    public string TransactionHash { get; set; }
    public string BlockHash { get; set; }
    public string ContractAddress { get; set; }
    public BigInteger Status { get; set; }
    
    
    
    public readonly string sourceChain;
    public readonly Address sourceAddress;
    public readonly string destinationChain;
    public readonly Address destinationAddress;
    public readonly Address interopAddress;
    public readonly string Symbol;
    public BigInteger Value;
    public byte[] Data;
}
