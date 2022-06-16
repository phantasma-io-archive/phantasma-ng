using System.Numerics;

namespace Phantasma.Core;

public interface ICustomContract
{
    string Name { get; }
    byte[] Script { get; }
    ContractInterface ABI { get; }
    BigInteger Order { get; } // TODO remove this?
    IRuntime Runtime { get; }
    Address Address { get; }
}
