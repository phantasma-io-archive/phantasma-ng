using Xunit;
using System.Text;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.EdDSA;
using Phantasma.Core.Numerics;

// Testing methods:
// bool Verify(byte[] signature, byte[] message, byte[] publicKey)

namespace Phantasma.Core.Tests.Cryptography.EdDSA;

public class CryptoEd25519Tests
{
    [Fact]
    public void VerifyPredefinedTest()
    {
        var keyHex = "A1CF654B4FE1C83597D165C6C8B6089FFF6D53423E6FE93D41E687A2C12942F7";
        var keyIncorrectHex = "A1CF654B4FE1C83597D165C6C8B6189FFF6D53423E6FE93D41E687A2C12942F7";

        var signatureHex = "7E4054CE755B555F405A4CC911D5C88FBE77630C63FC43DA7C7D92235AA79887032AD4C42C16D380AB05BBD1AB6A60C6CF8FBC12758B2A1C96CC9734B1C35801";
        var signatureIncorrectHex = "7E4054CE755B555F405A4CC911D5C88FBE77630C63FC43DA7C7D92235AA79887032AD4C42C26D380AB05BBD1AB6A60C6CF8FBC12758B2A1C96CC9734B1C35801";

        var msg = "Hello Phantasma!";
        var msgBytes = Encoding.ASCII.GetBytes(msg);

        var msgIncorrect = "Hello Fhantasma!";
        var msgIncorrectBytes = Encoding.ASCII.GetBytes(msgIncorrect);

        var signatureBytes = Base16.Decode(signatureHex);
        var signatureIncorrectBytes = Base16.Decode(signatureIncorrectHex);

        var keys = new PhantasmaKeys(Base16.Decode(keyHex));
        Assert.True(keys.PrivateKey.Length == PhantasmaKeys.PrivateKeyLength);

        var keysIncorrect = new PhantasmaKeys(Base16.Decode(keyIncorrectHex));
        Assert.True(keysIncorrect.PrivateKey.Length == PhantasmaKeys.PrivateKeyLength);

        // Check correct signature, message and public key
        Assert.True(Ed25519.Verify(signatureBytes, msgBytes, keys.PublicKey));

        // Check incorrect signature
        Assert.False(Ed25519.Verify(signatureIncorrectBytes, msgBytes, keys.PublicKey));

        // Check incorrect message
        Assert.False(Ed25519.Verify(signatureBytes, msgIncorrectBytes, keys.PublicKey));

        // Check incorrect public key
        Assert.False(Ed25519.Verify(signatureBytes, msgBytes, keysIncorrect.PublicKey));

        // Check incorrect signature and message
        Assert.False(Ed25519.Verify(signatureIncorrectBytes, msgIncorrectBytes, keys.PublicKey));

        // Check incorrect signature and public key
        Assert.False(Ed25519.Verify(signatureIncorrectBytes, msgBytes, keysIncorrect.PublicKey));

        // Check incorrect message and public key
        Assert.False(Ed25519.Verify(signatureBytes, msgIncorrectBytes, keysIncorrect.PublicKey));

        // Check incorrect everything
        Assert.False(Ed25519.Verify(signatureIncorrectBytes, msgIncorrectBytes, keysIncorrect.PublicKey));
    }
}
