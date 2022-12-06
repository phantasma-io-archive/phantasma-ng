using Xunit;
using System;
using System.Text;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.EdDSA;
using Phantasma.Core.Numerics;

// Testing main Phantasma signature
// Testing methods:
// Ed25519Signature Generate(IKeyPair keypair, byte[] message)
// bool Verify(byte[] message, IEnumerable<Address> addresses)

namespace Phantasma.Core.Tests.Cryptography.EdDSA;

public class CryptoEd25519SignatureTests
{
    [Fact]
    public void GenerateVerifyTest()
    {
        var msg = "Hello Phantasma!";
        var msgBytes = Encoding.ASCII.GetBytes(msg);

        var msgIncorrect = "Hello Fhantasma!";
        var msgIncorrectBytes = Encoding.ASCII.GetBytes(msgIncorrect);

        var keys = PhantasmaKeys.Generate();
        Assert.True(keys.PrivateKey.Length == PhantasmaKeys.PrivateKeyLength);

        var ed25519Signature = Ed25519Signature.Generate(keys, msgBytes);

        Console.WriteLine("Private key: " + Base16.Encode(keys.PrivateKey));
        Console.WriteLine("ed25519Signature.Bytes: " + Base16.Encode(ed25519Signature.Bytes));

        // Check correct message and address
        Assert.True(ed25519Signature.Verify(msgBytes, new Address[] { keys.Address }));
        // Check incorrect message
        Assert.False(ed25519Signature.Verify(msgIncorrectBytes, new Address[] { keys.Address }));
        // Check incorrect address
        Assert.False(ed25519Signature.Verify(msgBytes, new Address[] { PhantasmaKeys.Generate().Address }));
    }
    
    [Fact]
    public void VerifyPredefinedTest()
    {
        var keyHex = "45D6EAB1315E9F80E21BAC270216A55C08D7664A10974445F2AD8B0750920DB4";
        var keyIncorrectHex = "45D6EAB1315E9F80E21BAC270216A55C08D7664A10974445F2AD8B0750920DB5";
        
        var signatureHex = "8F6BFD2BD009F7FA859B35C762A76C3FBD5DC18ACA66456B4C6AAEA985E90809A5297C3B5E01FEA4EB8B431A5204A90B92542C86283672C0EBB96D1EE60D490A";
        var signatureIncorrectHex = "8F6BFD2BD009F7FA859B35C762A76C3FBD5DC18ACA66456B4C6AAEA985E90809A5297C3B5E01FEA4EB8B431A5204A90B92542C86283672C0EBB96D1EE60D490B";

        var msg = "Hello Phantasma!";
        var msgBytes = Encoding.ASCII.GetBytes(msg);

        var msgIncorrect = "Hello Fhantasma!";
        var msgIncorrectBytes = Encoding.ASCII.GetBytes(msgIncorrect);

        var ed25519Signature = new Ed25519Signature(Base16.Decode(signatureHex));
        var ed25519SignatureIncorrect = new Ed25519Signature(Base16.Decode(signatureIncorrectHex));

        var keys = new PhantasmaKeys(Base16.Decode(keyHex));
        Assert.True(keys.PrivateKey.Length == PhantasmaKeys.PrivateKeyLength);

        var keysIncorrect = new PhantasmaKeys(Base16.Decode(keyIncorrectHex));
        Assert.True(keysIncorrect.PrivateKey.Length == PhantasmaKeys.PrivateKeyLength);

        // Check correct message and address
        Assert.True(ed25519Signature.Verify(msgBytes, new Address[] { keys.Address }));
        
        // Check incorrect message
        Assert.False(ed25519Signature.Verify(msgIncorrectBytes, new Address[] { keys.Address }));

        // Check incorrect address
        Assert.False(ed25519Signature.Verify(msgBytes, new Address[] { keysIncorrect.Address }));

        // Check incorrect signature
        Assert.False(ed25519SignatureIncorrect.Verify(msgBytes, new Address[] { keys.Address }));
    }
}