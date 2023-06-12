using System.Numerics;
using Phantasma.Core.Cryptography;

namespace Phantasma.Core.Domain.Contract.Gas;

public struct GasLoanEntry
{
    public Hash hash;
    public Address borrower;
    public Address lender;
    public BigInteger amount;
    public BigInteger interest;
}
