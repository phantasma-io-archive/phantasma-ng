using System.IO;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Contract.Exchange;

public struct PoolRoute : ISerializable
{
    public string EntrySymbol;
    public string EndSymbol;
    public PoolRouteItem[] Route;
        
    public PoolRoute()
    {
        this.EntrySymbol = "";
        this.EndSymbol = "";
        this.Route = new PoolRouteItem[0];
    }
        
    public PoolRoute(string entrySymbol, string endSymbol, PoolRouteItem[] route)
    {
        this.EntrySymbol = entrySymbol;
        this.EndSymbol = endSymbol;
        this.Route = route;
    }
        
    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteVarString(EntrySymbol);
        writer.WriteVarString(EndSymbol);
        writer.WriteVarInt(Route.Length);
        foreach (var item in Route)
        {
            item.SerializeData(writer);
        }
    }

    public void UnserializeData(BinaryReader reader)
    {
        this.EntrySymbol = reader.ReadVarString();
        this.EndSymbol = reader.ReadVarString();
        var length = (int)reader.ReadVarInt();
        Route = new PoolRouteItem[length];
        for (int i = 0; i < length; i++)
        {
            Route[i].UnserializeData(reader);
        }
    }
}
