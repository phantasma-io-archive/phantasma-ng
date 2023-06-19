using Xunit;
using System;
using System.Text;
using Phantasma.Core.Cryptography.ECDsa;
using Phantasma.Core.Cryptography.ECDsa.Enums;
using Phantasma.Core.Numerics;
using Phantasma.Node.Chains.Neo2;

// Testing covers following methods:
// NeoKeys(byte[] privateKey)
// NeoKeys FromWIF(string wif)
// string GetWIF() - covered inside NeoKeys() call
// string ToString()
// Signature Sign(byte[] msg, Func<byte[], byte[], byte[], byte[]> customSignFunction = null)

namespace Phantasma.Core.Tests.Neo.Core;

public class PhantasmaNeoCoreTests
{
    [Fact]
    public void NeoKeysFromPkTest()
    {
        var keyHex = "A1CF654B4FE1C83597D165C6C8B6089FFF6D53423E6FE93D41E687A2C12942F7";

        // Data to verify:
        var publicKeyHex = "036509879495EFAAF9A05A1B6ED82708E4B673BFDF128E4BC7A02CA64B1D75AB55";
        var uncompressedPublicKeyHex = "6509879495EFAAF9A05A1B6ED82708E4B673BFDF128E4BC7A02CA64B1D75AB550FC7EADEE6D1EE79327478B2B79D4F6B46D3A92000CA8C5FEBB4D506D5BF1E19";
        var address = "AP6ZkjweW4NGskMca2KH2cchNJbFWW2vZe";
        var wif = "L2eFNhpqajpXqcgTGAo98NT37oNjvedFYxiKS4Rh8vKGxrBsFseL";

        var key = new NeoKeys(Base16.Decode(keyHex));

        Console.WriteLine("key.PrivateKey: " + Base16.Encode(key.PrivateKey));
        Assert.True(keyHex == Base16.Encode(key.PrivateKey));

        Console.WriteLine("key.PublicKey: " + Base16.Encode(key.PublicKey));
        Assert.True(publicKeyHex == Base16.Encode(key.PublicKey));

        Console.WriteLine("key.UncompressedPublicKey: " + Base16.Encode(key.UncompressedPublicKey));
        Assert.True(uncompressedPublicKeyHex == Base16.Encode(key.UncompressedPublicKey));

        Console.WriteLine("key.Address: " + key.Address);
        Assert.True(address == key.Address);

        Console.WriteLine("key.ToString(): " + key.ToString());
        Assert.True(address == key.ToString());

        Console.WriteLine("key.WIF: " + key.WIF);
        Assert.True(wif == key.WIF);
    }
    
    [Fact]
    public void NeoKeysFromWifTest()
    {
        var wif = "L2eFNhpqajpXqcgTGAo98NT37oNjvedFYxiKS4Rh8vKGxrBsFseL";

        // Data to verify:
        var keyHex = "A1CF654B4FE1C83597D165C6C8B6089FFF6D53423E6FE93D41E687A2C12942F7";
        var publicKeyHex = "036509879495EFAAF9A05A1B6ED82708E4B673BFDF128E4BC7A02CA64B1D75AB55";
        var uncompressedPublicKeyHex = "6509879495EFAAF9A05A1B6ED82708E4B673BFDF128E4BC7A02CA64B1D75AB550FC7EADEE6D1EE79327478B2B79D4F6B46D3A92000CA8C5FEBB4D506D5BF1E19";
        var address = "AP6ZkjweW4NGskMca2KH2cchNJbFWW2vZe";

        var key = NeoKeys.FromWIF(wif);

        Console.WriteLine("key.PrivateKey: " + Base16.Encode(key.PrivateKey));
        Assert.True(keyHex == Base16.Encode(key.PrivateKey));

        Console.WriteLine("key.PublicKey: " + Base16.Encode(key.PublicKey));
        Assert.True(publicKeyHex == Base16.Encode(key.PublicKey));

        Console.WriteLine("key.UncompressedPublicKey: " + Base16.Encode(key.UncompressedPublicKey));
        Assert.True(uncompressedPublicKeyHex == Base16.Encode(key.UncompressedPublicKey));

        Console.WriteLine("key.Address: " + key.Address);
        Assert.True(address == key.Address);

        Console.WriteLine("key.ToString(): " + key.ToString());
        Assert.True(address == key.ToString());

        Console.WriteLine("key.WIF: " + key.WIF);
        Assert.True(wif == key.WIF);
    }
    
    [Fact]
    public void SignTest()
    {
        var msg = "Hello Phantasma!";
        var msgBytes = Encoding.ASCII.GetBytes(msg);

        var msgIncorrect = "Hello Fhantasma!";
        var msgIncorrectBytes = Encoding.ASCII.GetBytes(msgIncorrect);

        var keyHex = "A1CF654B4FE1C83597D165C6C8B6089FFF6D53423E6FE93D41E687A2C12942F7";
        var keyIncorrectHex = "A1CF654B4FE1C83597D165C6C8B6099FFF6D53423E6FE93D41E687A2C12942F7";

        var key = new NeoKeys(Base16.Decode(keyHex));
        var keyIncorrect = new NeoKeys(Base16.Decode(keyIncorrectHex));

        var signature = key.Sign(msgBytes);
        var ecdsaSignature = (ECDsaSignature)signature;

        // Checking correct message and correct key
        Assert.True(ECDsa.Verify(msgBytes, ecdsaSignature.Bytes, key.PublicKey, ECDsaCurve.Secp256r1));

        // Checking incorrect message and correct key
        Assert.False(ECDsa.Verify(msgIncorrectBytes, ecdsaSignature.Bytes, key.PublicKey, ECDsaCurve.Secp256r1));

        // Checking correct message and incorrect key
        Assert.False(ECDsa.Verify(msgBytes, ecdsaSignature.Bytes, keyIncorrect.PublicKey, ECDsaCurve.Secp256r1));

        // Checking incorrect message and incorrect key
        Assert.False(ECDsa.Verify(msgIncorrectBytes, ecdsaSignature.Bytes, keyIncorrect.PublicKey, ECDsaCurve.Secp256r1));
    }
}

