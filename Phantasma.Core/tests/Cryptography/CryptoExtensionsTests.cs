using System.Linq;
using System.Text;

namespace Phantasma.Core.Tests.Cryptography;

using Xunit;

using Phantasma.Core.Cryptography;

public class CryptoExtensionsTests
{
    [Fact]
    public void TestAES()
    {
        var hash = CryptoExtensions.AESGenerateIV(4);
        Assert.Equal(4, hash.Length);
    }

    [Fact]
    public void TestAESGCM()
    {
        // create a test for methd AESGCMEncrypt
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10 };
        var key = new byte[] { 0x2B, 0x7E, 0x15, 0x16, 0x28, 0xAE, 0xD2, 0xA6, 0xAB, 0xF7, 0x15, 0x88, 0x09, 0xCF, 0x4F, 0x3C };
        
        var dataEncrypted = CryptoExtensions.AESGCMEncrypt(data, key);
        Assert.NotEqual(data, dataEncrypted);
        
        var dataDecrypted = CryptoExtensions.AESGCMDecrypt(dataEncrypted, key);
        Assert.Equal(data.Length, dataDecrypted.Length);
        Assert.Equal(data, dataDecrypted);
    }
    
    [Fact]
    public void TestAESGCMIV()
    {
        // create a test for methd AESGCMEncrypt
        var iv = CryptoExtensions.AESGenerateIV(16);
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10 };
        var key = new byte[] { 0x2B, 0x7E, 0x15, 0x16, 0x28, 0xAE, 0xD2, 0xA6, 0xAB, 0xF7, 0x15, 0x88, 0x09, 0xCF, 0x4F, 0x3C };
        
        var dataEncrypted = CryptoExtensions.AESGCMEncrypt(data, key, iv);
        Assert.NotEqual(data, dataEncrypted);
        
        var dataDecrypted = CryptoExtensions.AESGCMDecrypt(dataEncrypted, key, iv);
        Assert.Equal(data.Length, dataDecrypted.Length);
        Assert.Equal(data, dataDecrypted);
    }
    
    [Fact]
    public void TestBase58Check()
    {
        // create a based 58 string with some text that can be encoded
        var text = "Hello World";
        var bytes  = Encoding.UTF8.GetBytes(text);
        var base58 = CryptoExtensions.Base58CheckEncode(bytes);
        var dataEncoded = Numerics.Base58.Encode(bytes);
        Assert.NotEqual(dataEncoded, base58);
        
        // decode the base 58 string
        var dataDecoded = CryptoExtensions.Base58CheckDecode(base58);
        var dataDecoded2 = Numerics.Base58.Decode(dataEncoded);
        Assert.Equal(dataDecoded, dataDecoded2);
    }

    [Fact]
    public void TestRIPEMD160()
    {
        // create a test for methd RIPEMD160
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10 };
        var hash = CryptoExtensions.RIPEMD160(data);
        Assert.Equal(20, hash.Length);
    }
    
    [Fact]
    public void TestSha256()
    {
        // create a test for methd SHA256
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10 };
        var hash = CryptoExtensions.Sha256(data);
        Assert.Equal(32, hash.Length);
        
        // create a test for method SHA256 with offset and length
        var data2 = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20 };
        var hash2 = CryptoExtensions.Sha256(data2, 0, 16);
        Assert.Equal(32, hash2.Length);
        
        // create a test for method SHA256 with offset and length uint
        var data3 = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20 };
        var hash3 = CryptoExtensions.Sha256(data3, 0u, 16u);
        Assert.Equal(32, hash3.Length);
        
        // create a test for method SHA256 
        var data4 = data3.ToList();
        var hash4 = CryptoExtensions.Sha256(data4);
    }
    
}
