using System.Numerics;
using Phantasma.Core.Cryptography.Structs;

namespace Phantasma.Core.Domain.Contract.Interop.Structs;

public struct InteropHistory
{
    public Hash sourceHash;
    public string sourcePlatform;
    public string sourceChain;
    public Address sourceAddress;

    public Hash destHash;
    public string destPlatform;
    public string destChain;
    public Address destAddress;

    public string symbol;
    public BigInteger value;
}
