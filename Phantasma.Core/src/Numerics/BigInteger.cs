using System;
using System.Linq;
using System.Numerics;

/*
 * Implementation of BigIntegerOld class, written for Phantasma project
 * Author: Simão Pavlovich and Bernardo Pinho
 */

namespace Phantasma.Core
{
    public static class BigIntegerExtensions
    {
        public static System.Numerics.BigInteger AsBigInteger(this byte[] source) { return (source == null || source.Length == 0) ? new System.Numerics.BigInteger(0) : new System.Numerics.BigInteger(source); }
        public static byte[] AsByteArray(this BigInteger source) { return source.ToByteArray(); }
    }
}
