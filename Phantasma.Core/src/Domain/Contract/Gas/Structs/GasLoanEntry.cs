using System.Numerics;
using Phantasma.Core.Cryptography.Structs;

namespace Phantasma.Core.Domain.Contract.Gas.Structs;

public struct GasLoanEntry
{
    public Hash hash;
    public Address borrower;
    public Address lender;
    public BigInteger amount;
    public BigInteger interest;
}
