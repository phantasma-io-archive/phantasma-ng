using System.Numerics;
using Phantasma.Core.Cryptography.Structs;

namespace Phantasma.Core.Domain.Contract.Interop.Structs;

public struct InteropTransfer
{
    public readonly string sourceChain;
    public readonly Address sourceAddress;
    public readonly string destinationChain;
    public readonly Address destinationAddress;
    public readonly Address interopAddress;
    public readonly string Symbol;
    public BigInteger Value;
    public byte[] Data;

    public InteropTransfer(string sourceChain, Address sourceAddress, string destinationChain, Address destinationAddress, Address interopAddress, string symbol, BigInteger value, byte[] data = null)
    {
        this.sourceChain = sourceChain;
        this.sourceAddress = sourceAddress;
        this.destinationChain = destinationChain;
        this.destinationAddress = destinationAddress;
        this.interopAddress = interopAddress;
        Symbol = symbol;
        Value = value;
        Data = data != null ? data : new byte[0];
    }
}
