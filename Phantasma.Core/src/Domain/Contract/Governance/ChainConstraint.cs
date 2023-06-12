using System.Numerics;

namespace Phantasma.Core.Domain.Contract.Governance;

public struct ChainConstraint
{
    public ConstraintKind Kind;
    public BigInteger Value;
    public string Tag;
}
