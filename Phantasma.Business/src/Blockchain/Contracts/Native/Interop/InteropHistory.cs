using System.Numerics;
using Phantasma.Core.Cryptography;

namespace Phantasma.Business.Blockchain.Contracts.Native;

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
