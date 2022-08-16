using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Phantasma.Core.Tests;

[TestClass]
public class Base58
{
    [TestMethod]
    public void encode_decode_test_numbers()
    {
        const string testString = "0815";
        var byteArray = Encoding.ASCII.GetBytes(testString);
        var encodedString = Core.Base58.Encode(byteArray);
        var decodedBytes = Core.Base58.Decode(encodedString);
        decodedBytes.ShouldBe(byteArray);
        Utils.GetStringFromByteArray(decodedBytes).ShouldBe(testString);
    }

    [TestMethod]
    public void encode_decode_test_string()
    {
        const string testString = "Sepp";
        var byteArray = Encoding.ASCII.GetBytes(testString);
        var encodedString = Core.Base58.Encode(byteArray);
        var decodedBytes = Core.Base58.Decode(encodedString);
        decodedBytes.ShouldBe(byteArray);
        Utils.GetStringFromByteArray(decodedBytes).ShouldBe(testString);
    }

    /*[TestMethod]
    public void wrong_input_test()
    {
    }*/
}
