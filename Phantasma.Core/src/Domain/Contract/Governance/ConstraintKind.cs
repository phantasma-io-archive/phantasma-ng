namespace Phantasma.Core.Domain.Contract.Governance;

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
