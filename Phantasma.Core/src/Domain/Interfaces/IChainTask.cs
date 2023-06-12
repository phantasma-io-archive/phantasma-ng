using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Tasks;
using Phantasma.Core.Domain.Tasks.Enum;

namespace Phantasma.Core.Domain.Interfaces;

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
