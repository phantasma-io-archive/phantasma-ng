using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Contract.Validator.Enums;
using Phantasma.Core.Types.Structs;

namespace Phantasma.Core.Domain.Contract.Validator.Structs;

public struct ValidatorEntry
{
    public Address address;
    public Timestamp election;
    public ValidatorType type;
}
