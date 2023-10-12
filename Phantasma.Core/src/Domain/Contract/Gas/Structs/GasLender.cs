using System.Numerics;
using Phantasma.Core.Cryptography.Structs;

namespace Phantasma.Core.Domain.Contract.Gas.Structs;

public struct GasLender
{
    public BigInteger balance;
    public Address paymentAddress;
}
