using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Core.Numerics;
using Shouldly;

namespace Phantasma.Core.Tests;

[TestClass]
public class BigIntegerTests
{
    [TestMethod]
    public void byte_array_to_big_integer()
    {
        var byteArray = new byte[] {42};
        var bigInteger = byteArray.AsBigInteger();
        bigInteger.ShouldBe(42);
    }

    [TestMethod]
    public void big_integer_to_byte_array()
    {
        BigInteger bigInteger = 42;
        var byteArray = bigInteger.AsByteArray();
        byteArray.ShouldBe(new byte[] {42});
    }
}
