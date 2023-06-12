using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Contract;

namespace Phantasma.Core.Domain.Interfaces;

public interface ICustomContract
{
    string Name { get; }
    byte[] Script { get; }
    ContractInterface ABI { get; }
    IRuntime Runtime { get; }
    Address Address { get; }
}
