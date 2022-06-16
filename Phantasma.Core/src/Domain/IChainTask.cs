using System.Numerics;
using Phantasma.Core;

namespace Phantasma.Core;

public interface IChainTask
{
    BigInteger ID { get; }
    bool State { get; }
    Address Owner { get; }
    string ContextName { get; }
    string Method { get; }
    uint Frequency { get; }
    uint Delay { get; }
    TaskFrequencyMode Mode { get; }
    BigInteger GasLimit { get; }
    BigInteger Height { get; }
    byte[] ToByteArray();
}
