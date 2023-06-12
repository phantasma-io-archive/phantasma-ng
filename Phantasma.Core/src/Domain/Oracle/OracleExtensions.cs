using System.Numerics;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Types;

namespace Phantasma.Core.Domain.Oracle;

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
