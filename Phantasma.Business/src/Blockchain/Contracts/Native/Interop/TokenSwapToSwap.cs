using System.IO;
using Phantasma.Core.Domain;
using Phantasma.Core.Utils;

namespace Phantasma.Business.Blockchain.Contracts.Native;

public struct TokenSwapToSwap : ISerializable
{
    public string Platform;
    public string Symbol;

    public int Decimals;
    public string ExternalContractAddress;
    public Swapper[] Swappers;
        
    public TokenSwapToSwap(string platform, string symbol, int decimals , string externalContractAddress, Swapper[] swappers)
    {
        this.Platform = platform;
        this.Symbol = symbol;
        this.Decimals = decimals;
        this.ExternalContractAddress = externalContractAddress;
        this.Swappers = swappers;
    }
        
    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteVarString(Platform);
        writer.WriteVarString(Symbol);
        writer.WriteVarInt(Decimals);
        writer.WriteVarString(ExternalContractAddress);
        writer.WriteVarInt(Swappers.Length);
        foreach (var swapper in Swappers)
        {
            swapper.SerializeData(writer);
        }
    }

    public void UnserializeData(BinaryReader reader)
    {
        this.Platform = reader.ReadVarString();
        this.Symbol = reader.ReadVarString();
        this.Decimals = (int)reader.ReadVarInt();
        this.ExternalContractAddress = reader.ReadVarString();
        var swapperCount = (int)reader.ReadVarInt();
        this.Swappers = new Swapper[swapperCount];
        for (int i = 0; i < swapperCount; i++)
        {
            var temp = new Swapper();
            temp.UnserializeData(reader);
            this.Swappers[i] = temp;
        }
    }
}
