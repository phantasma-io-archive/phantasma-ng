using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Phantasma.Node.Chains.Ethereum;

[Function("swapIn", "bool")]
public class SwapInFunction : FunctionMessage
{
    [Parameter("address", "source", 1)]
    public string Source { get; set; }

    [Parameter("address", "target", 2)]
    public string Target { get; set; }

    [Parameter("uint256", "amount", 3)]
    public BigInteger Amount { get; set; }
}
