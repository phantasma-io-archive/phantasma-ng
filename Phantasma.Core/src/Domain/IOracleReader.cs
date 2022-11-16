using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Types;

namespace Phantasma.Core.Domain;

public interface IOracleReader
{
    BigInteger ProtocolVersion { get; }
    IEnumerable<OracleEntry> Entries { get; }
    string GetCurrentHeight(string platformName, string chainName);
    void SetCurrentHeight(string platformName, string chainName, string height);
    List<InteropBlock> ReadAllBlocks(string platformName, string chainName);
    T Read<T>(Timestamp time, string url) where T : class;
    InteropTransaction ReadTransaction(string platform, string chain, Hash hash);
    void Clear();
    void MergeTxData();
}

public static class OracleExtensions
{
    public static BigInteger ReadBigInteger(this IOracleReader reader, Timestamp time, string url)
    {
        var bytes = reader.Read<byte[]>(time, url);
        if (bytes == null || bytes.Length <= 0)
        {
            return -1;
        }

        var value = new BigInteger(bytes, true);

        return value;
    }

    public static BigInteger ReadPrice(this IOracleReader reader, Timestamp time, string symbol)
    {
        return ReadBigInteger(reader, time, "price://" + symbol);
    }
}
