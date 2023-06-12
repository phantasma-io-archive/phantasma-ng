using Phantasma.Core.Cryptography;
using Phantasma.Core.Types;

namespace Phantasma.Core.Domain.Contract.Validator;

public struct ValidatorEntry
{
    public Address address;
    public Timestamp election;
    public ValidatorType type;
}
