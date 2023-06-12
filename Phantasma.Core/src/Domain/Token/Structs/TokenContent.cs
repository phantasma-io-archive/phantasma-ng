using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Exceptions;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Token.Enums;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types.Structs;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Token.Structs;

public struct TokenContent : ISerializable
{
    public BigInteger SeriesID { get; private set; }
    public BigInteger MintID { get; private set; }
    public string CurrentChain { get; private set; }
    public Address Creator { get; private set; }
    public Address CurrentOwner { get; private set; }
    public byte[] ROM { get; private set; }
    public byte[] RAM { get; private set; }
    public Timestamp Timestamp { get; private set; }
    public TokenInfusion[] Infusion { get; private set; }

    public BigInteger TokenID { get; private set; }

    // sizes in bytes
    // TODO find optimal values for this
    public static readonly int MaxROMSize = 1024;
    public static readonly int MaxRAMSize = 1024;

    public TokenContent(BigInteger seriesID, BigInteger mintID, string currentChain, Address creator, Address currentOwner,
        byte[] ROM, byte[] RAM, Timestamp timestamp, IEnumerable<TokenInfusion> infusion, TokenSeriesMode mode) : this()
    {
        this.SeriesID = seriesID;
        this.MintID = mintID;
        this.Creator = creator;
        CurrentChain = currentChain;
        CurrentOwner = currentOwner;
        this.ROM = ROM;
        this.RAM = RAM;
        this.Timestamp = timestamp;
        this.Infusion = infusion != null ? infusion.ToArray(): new TokenInfusion[0];

        UpdateTokenID(mode);
    }

    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteBigInteger(SeriesID);
        writer.WriteBigInteger(MintID);
        writer.WriteAddress(Creator);
        writer.WriteVarString(CurrentChain);
        writer.WriteAddress(CurrentOwner);
        writer.WriteByteArray(ROM);
        writer.WriteByteArray(RAM);
        writer.Write(Timestamp.Value);
        writer.WriteVarInt(Infusion.Length);
        foreach (var entry in Infusion)
        {
            writer.WriteVarString(entry.Symbol);
            writer.WriteBigInteger(entry.Value);
        }
    }

    public void UnserializeData(BinaryReader reader)
    {
        SeriesID = reader.ReadBigInteger();
        MintID = reader.ReadBigInteger();

        Creator = reader.ReadAddress();

        CurrentChain = reader.ReadVarString();
        CurrentOwner = reader.ReadAddress();

        ROM = reader.ReadByteArray();
        ROM = ROM ?? new byte[0];

        RAM = reader.ReadByteArray();
        RAM = RAM ?? new byte[0];

        Timestamp = new Timestamp(reader.ReadUInt32());

        var infusionCount = (int)reader.ReadVarInt();
        this.Infusion = new TokenInfusion[infusionCount];
        for (int i = 0; i < infusionCount; i++)
        {
            var symbol = reader.ReadVarString();
            var value = reader.ReadBigInteger();
            Infusion[i] = new TokenInfusion(symbol, value);
        }
    }

    public void UpdateTokenID(TokenSeriesMode mode)
    {
        byte[] bytes;

        switch (mode)
        {
            case TokenSeriesMode.Unique: bytes = ROM; break;
            case TokenSeriesMode.Duplicated: bytes = ROM.Concat(SeriesID.ToUnsignedByteArray())
                .Concat(MintID.ToUnsignedByteArray()).ToArray(); break;
            default:
                throw new ChainException($"Generation of tokenID for Series with {mode} is not implemented");
        }

        // TODO check this
        this.TokenID = Hash.FromBytes(bytes);
    }

    public byte[] ToByteArray()
    {
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream))
            {
                SerializeData(writer);
            }

            return stream.ToArray();
        }
    }

    public void ReplaceROM(byte[] newROM)
    {
        this.ROM = newROM;
    }
}
