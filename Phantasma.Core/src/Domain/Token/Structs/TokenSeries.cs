using System.IO;
using System.Numerics;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Token.Enums;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Token.Structs;

public class TokenSeries : ITokenSeries
{
    public BigInteger MintCount { get; private set; }
    public BigInteger MaxSupply { get; private set; }
    public TokenSeriesMode Mode { get; private set; }
    public byte[] Script { get; private set; }
    public ContractInterface ABI { get; private set; }
    public byte[] ROM{ get; private set; }

    public TokenSeries(BigInteger mintCount, BigInteger maxSupply, TokenSeriesMode mode, byte[] script, ContractInterface ABI, byte[] ROM)
    {
        MintCount = mintCount;
        MaxSupply = maxSupply;
        Mode = mode;
        Script = script;
        this.ABI = ABI;
        SetROM(ROM);
    }

    public TokenSeries() // required for ISerializable
    {
        // do nothing
    }


    public void SetROM(byte[] ROM)
    {
        this.ROM = ROM != null ? ROM : new byte[0];
    }

    public BigInteger GenerateMintID()
    {
        this.MintCount = MintCount + 1;
        return MintCount;
    }

    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteBigInteger(MintCount);
        writer.WriteBigInteger(MaxSupply);
        writer.Write((byte)Mode);
        writer.WriteByteArray(Script);

        var bytes = ABI.ToByteArray();
        writer.WriteByteArray(bytes);

        writer.WriteByteArray(ROM);
    }

    public void UnserializeData(BinaryReader reader)
    {
        this.MintCount = reader.ReadBigInteger();
        this.MaxSupply = reader.ReadBigInteger();
        this.Mode = (TokenSeriesMode)reader.ReadByte();
        this.Script = reader.ReadByteArray();

        var bytes = reader.ReadByteArray();
        this.ABI = ContractInterface.FromBytes(bytes);

        this.ROM = reader.ReadByteArray();
    }
}
