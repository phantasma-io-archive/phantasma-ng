namespace Phantasma.Core.Domain.Contract.Validator;

public enum ValidatorType
{
    Invalid,
    Proposed,
    Primary,
    Secondary, // aka StandBy
}
