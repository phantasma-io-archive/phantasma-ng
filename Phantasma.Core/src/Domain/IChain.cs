using System.Numerics;
using Phantasma.Shared.Types;

namespace Phantasma.Core
{
    public interface IChain
    {
        string Name { get; }
        Address Address { get; }
        BigInteger Height { get; }
    }
}
