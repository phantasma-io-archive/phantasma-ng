using Phantasma.Core.Cryptography;
using Shouldly;
using Xunit;

namespace Phantasma.Core.Tests;

public class HashTests
{
    [Fact]
    public void null_hash_test()
    {
        var hash = Hash.Null;
        hash.ToByteArray().Length.ShouldBe(Hash.Length);
        hash.ToByteArray().ShouldBe(new byte[Hash.Length]);
    }
}
