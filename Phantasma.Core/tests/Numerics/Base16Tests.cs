using System.IO;
using System.Text;
using Phantasma.Core.Numerics;
using Shouldly;
using Xunit;

namespace Phantasma.Core.Tests.Numerics;

public class Base16Tests
{
    [Fact]
    public void encode_decode_test_numbers()
    {
        const string testString = "0815";
        var byteArray = Encoding.ASCII.GetBytes(testString);
        var encodedString = byteArray.Encode();
        var decodedBytes = encodedString.Decode();
        decodedBytes.ShouldBe(byteArray);
        GetStringFromByteArray(decodedBytes).ShouldBe(testString);
    }

    [Fact]
    public void encode_decode_test_string()
    {
        const string testString = "Sepp";
        var byteArray = Encoding.ASCII.GetBytes(testString);
        var encodedString = byteArray.Encode();
        var decodedBytes = encodedString.Decode();
        decodedBytes.ShouldBe(byteArray);
        GetStringFromByteArray(decodedBytes).ShouldBe(testString);
    }

    /*[Fact]
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
