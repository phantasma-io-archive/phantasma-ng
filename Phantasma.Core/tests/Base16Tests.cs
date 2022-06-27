using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Phantasma.Core.Tests;

[TestClass]
public class Base16Tests
{
    [TestMethod]
    public void encode_decode_test_numbers()
    {
        const string testString = "0815";
        var byteArray = Encoding.ASCII.GetBytes(testString);
        var encodedString = byteArray.Encode();
        var decodedBytes = encodedString.Decode();
        decodedBytes.ShouldBe(byteArray);
        Utils.GetStringFromByteArray(decodedBytes).ShouldBe(testString);
    }

    [TestMethod]
    public void encode_decode_test_string()
    {
        const string testString = "Sepp";
        var byteArray = Encoding.ASCII.GetBytes(testString);
        var encodedString = byteArray.Encode();
        var decodedBytes = encodedString.Decode();
        decodedBytes.ShouldBe(byteArray);
        Utils.GetStringFromByteArray(decodedBytes).ShouldBe(testString);
    }

    /*[TestMethod]
    public void wrong_input_test()
    {
    }*/
}
