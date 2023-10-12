using System.Numerics;
using Phantasma.Core.Domain.Contract.Governance.Enums;

namespace Phantasma.Core.Domain.Contract.Governance.Structs;

public struct ChainConstraint
{
    public ConstraintKind Kind;
    public BigInteger Value;
    public string Tag;
}
