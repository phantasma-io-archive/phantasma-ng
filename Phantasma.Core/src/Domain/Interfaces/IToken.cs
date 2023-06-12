using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Token;
using Phantasma.Core.Domain.Token.Enums;

namespace Phantasma.Core.Domain.Interfaces
{
    public interface IToken
    {
        string Name { get; }
        string Symbol { get; }
        Address Owner { get; }
        TokenFlags Flags { get; }
        BigInteger MaxSupply { get;  }
        int Decimals { get; }
        byte[] Script { get; }
        ContractInterface ABI { get; }
    }
}
