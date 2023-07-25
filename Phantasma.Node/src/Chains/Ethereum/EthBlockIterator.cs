using System.Numerics;

namespace Phantasma.Node.Chains.Ethereum;

public class EthBlockIterator
{
    public BigInteger currentBlock;
    public uint currentTransaction;

    public EthBlockIterator(EthAPI api)
    {
        this.currentBlock = api.GetBlockHeight();
        this.currentTransaction = 0;
    }

    public override string ToString()
    {
        return $"{currentBlock}/{currentTransaction}";
    }
}
