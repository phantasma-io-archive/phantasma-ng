using System.Numerics;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Token;

namespace Phantasma.Core.Domain.Interfaces;

public interface ITokenSeries: ISerializable
{
    BigInteger MintCount { get; }
    BigInteger MaxSupply { get; }
    TokenSeriesMode Mode { get; }
    byte[] Script { get;  }
    ContractInterface ABI { get; }
    byte[] ROM { get; }

    BigInteger GenerateMintID();
    void SetROM(byte[] ROM);
}
