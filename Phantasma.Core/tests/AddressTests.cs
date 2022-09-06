using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Core.Cryptography;
using Shouldly;

namespace Phantasma.Core.Tests;

[TestClass]
public class AddressTests
{
    [TestMethod]
    public void null_address_test()
    {
        var address = Address.Null;
        address.ToByteArray().Length.ShouldBe(Address.LengthInBytes);
        address.ToByteArray().ShouldBe(new byte[Address.LengthInBytes]);
    }
}
