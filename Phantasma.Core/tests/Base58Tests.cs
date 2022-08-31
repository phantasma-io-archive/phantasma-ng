using System.IO;
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
        var encodedString = Numerics.Base58.Encode(byteArray);
        var decodedBytes = Numerics.Base58.Decode(encodedString);
        decodedBytes.ShouldBe(byteArray);
        GetStringFromByteArray(decodedBytes).ShouldBe(testString);
    }

    [TestMethod]
    public void encode_decode_test_string()
    {
        const string testString = "Sepp";
        var byteArray = Encoding.ASCII.GetBytes(testString);
        var encodedString = Numerics.Base58.Encode(byteArray);
        var decodedBytes = Numerics.Base58.Decode(encodedString);
        decodedBytes.ShouldBe(byteArray);
        GetStringFromByteArray(decodedBytes).ShouldBe(testString);
    }

    /*[TestMethod]
    public void wrong_input_test()
    {
    }*/

    private static string GetStringFromByteArray(byte[] array)
    {
        using var stream = new MemoryStream(array);
        using var streamReader = new StreamReader(stream);

        return streamReader.ReadToEnd();
    }
}
