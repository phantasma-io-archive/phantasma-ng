namespace Phantasma.Core.Domain.Oracle;

public struct PackedNFTData
{
    public readonly string Symbol;
    public readonly byte[] ROM;
    public readonly byte[] RAM;

    public PackedNFTData(string symbol, byte[] rom, byte[] ram)
    {
        Symbol = symbol;
        ROM = rom;
        RAM = ram;
    }
}
