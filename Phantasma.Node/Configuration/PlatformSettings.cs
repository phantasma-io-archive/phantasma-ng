using System.Numerics;
using Phantasma.Infrastructure.API;

namespace Phantasma.Node;

public class PlatformSettings
{
    public SwapPlatformChain Chain;
    public BigInteger InteropHeight;
    public string[] RpcNodes;
    public string[] Tokens;
    public string Fuel;
}
