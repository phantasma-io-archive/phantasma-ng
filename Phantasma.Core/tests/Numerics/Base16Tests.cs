using System;
using System.IO;
using System.Text;
using Phantasma.Core.Numerics;
using Shouldly;
using Xunit;

namespace Phantasma.Core.Tests.Numerics;

[Collection("Numerics")]
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
    
    [Fact]
    public void encode_decode_test_string_with_special_chars()
    {
        const string testString = "Sepp@";
        var byteArray = Encoding.ASCII.GetBytes(testString);
        var encodedString = byteArray.Encode();
        var decodedBytes = encodedString.Decode();
        decodedBytes.ShouldBe(byteArray);
        GetStringFromByteArray(decodedBytes).ShouldBe(testString);
    }
    
    [Fact]
    public void encode_null_test()
    {
        byte[]? byteArray = null;
        var encodedString = byteArray.Encode();
        encodedString.ShouldBeEmpty();
    }
    
    [Fact]
    public void decode_null_test()
    {
        string? encodedString = null;
        var decodedBytes = encodedString.Decode();
        decodedBytes.ShouldBeEmpty();
    }
    
    [Fact]
    public void decode_start_0x(){
        const string testString = "0x0815";
        var decodedBytes = testString.Decode();
        decodedBytes.ShouldBe(new byte[]{8, 21});
    }
    
    [Fact]
    public void decode_b_less_than_0_or_a_less_than_0(){
        const string testString = "oxox";
        var decodedBytes = testString.Decode(false);
        decodedBytes.ShouldBeNull();
    }
    
    [Fact]
    public void decode_allow_expections(){
        const string testString = "xox";
        Should.Throw<Exception>(() => testString.Decode(true));
    }
    
    [Fact]
    public void decode_not_allow_expections(){
        const string testString = "teste";
        var decodedBytes = testString.Decode(false);
        decodedBytes.ShouldBe(null);
    }
    
    private static string GetStringFromByteArray(byte[] array)
    {
        using var stream = new MemoryStream(array);
        using var streamReader = new StreamReader(stream);

        return streamReader.ReadToEnd();
    }
}
