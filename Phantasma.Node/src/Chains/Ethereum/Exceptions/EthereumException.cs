using System;

namespace Phantasma.Node.Chains.Ethereum;

public class EthereumException : Exception
{
    public EthereumException(string msg) : base(msg)
    {

    }

    public EthereumException(string msg, Exception cause) : base(msg, cause)
    {

    }
}
