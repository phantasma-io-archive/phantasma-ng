namespace Phantasma.Core.Domain.Contract.Governance.Enums;

public enum ConstraintKind
{
    MaxValue,
    MinValue,
    GreatThanOther,
    LessThanOther,
    MustIncrease,
    MustDecrease,
    Deviation,
}
