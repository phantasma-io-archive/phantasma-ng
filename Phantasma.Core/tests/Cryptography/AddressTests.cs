using Phantasma.Core.Cryptography;
using Shouldly;
using Xunit;

namespace Phantasma.Core.Tests;

public class AddressTests
{
    [Fact]
    public void null_address_test()
    {
        var address = Address.Null;
        address.ToByteArray().Length.ShouldBe(Address.LengthInBytes);
        address.ToByteArray().ShouldBe(new byte[Address.LengthInBytes]);
    }
}
