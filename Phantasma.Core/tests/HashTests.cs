using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Core.Cryptography;
using Shouldly;

namespace Phantasma.Core.Tests;

[TestClass]
public class HashTests
{
    [TestMethod]
    public void null_hash_test()
    {
        var hash = Hash.Null;
        hash.ToByteArray().Length.ShouldBe(Hash.Length);
        hash.ToByteArray().ShouldBe(new byte[Hash.Length]);
    }
}
