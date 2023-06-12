using System.Numerics;
using Phantasma.Core.Cryptography;

namespace Phantasma.Core.Domain.Contract.Gas;

public struct GasLender
{
    public BigInteger balance;
    public Address paymentAddress;
}
