using System.Numerics;

namespace Phantasma.Core
{
    public static class BigIntegerExtensions
    {
        public static System.Numerics.BigInteger AsBigInteger(this byte[] source) { return (source == null ||
                source.Length == 0) ? new System.Numerics.BigInteger(0) : new System.Numerics.BigInteger(source); }
        public static byte[] AsByteArray(this BigInteger source) { return source.ToByteArray(); }
    }
}
