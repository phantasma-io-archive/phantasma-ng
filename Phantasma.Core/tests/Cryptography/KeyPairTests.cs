using System;
using System.Security.Cryptography;
using System.Text;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.EdDSA;
using Xunit;

namespace Phantasma.Core.Tests.Cryptography;

public class KeyPairTests
{
    // Address = P2K9zmyFDNGN6n6hHiTUAz6jqn29s5G1SWLiXwCVQcpHcQb
    // Valid wif = KxMn2TgXukYaNXx7tEdjh7qB2YaMgeuKy47j4rvKigHhBuZWeP3r
    // Test that PhantasmaKeys.Generate generates a new key pair correctly
    [Fact]
    public void Generate_GeneratesNewKeyPair()
    {
        var keyPair = PhantasmaKeys.Generate();
        
        // Assert that the private key is not null
        Assert.NotNull(keyPair.PrivateKey);
        // Assert that the private key has the correct length
        Assert.Equal(PhantasmaKeys.PrivateKeyLength, keyPair.PrivateKey.Length);
        // Assert that the public key is not null
        Assert.NotNull(keyPair.PublicKey);
        // Assert that the public key has the correct length
        Assert.Equal(Ed25519.PrivateKeySeedSizeInBytes, keyPair.PublicKey.Length);
        // Assert that the address is not null
        Assert.NotNull(keyPair.Address);
    }

    // Test that PhantasmaKeys.FromWIF correctly generates a key pair from a WIF
    [Fact]
    public void FromWIF_GeneratesKeyPair()
    {
        // Use a known WIF
        const string wif = "KxMn2TgXukYaNXx7tEdjh7qB2YaMgeuKy47j4rvKigHhBuZWeP3r";
        var keyPair = PhantasmaKeys.FromWIF(wif);

        // Assert that the private key is not null
        Assert.NotNull(keyPair.PrivateKey);
        // Assert that the private key has the correct length
        Assert.Equal(PhantasmaKeys.PrivateKeyLength, keyPair.PrivateKey.Length);
        // Assert that the public key is not null
        Assert.NotNull(keyPair.PublicKey);
        // Assert that the public key has the correct length
        Assert.Equal(Ed25519.PublicKeySizeInBytes, keyPair.PublicKey.Length);
        // Assert that the address is not null
        Assert.NotNull(keyPair.Address);
        // Assert that the address is the correct address for the WIF
        Assert.Equal("P2K9zmyFDNGN6n6hHiTUAz6jqn29s5G1SWLiXwCVQcpHcQb", keyPair.Address.Text);
    }
    
    // Test that PhantasmaKeys.ToString correctly generates a WIF from a key pair
    [Fact]
    public void ToString_GeneratesWIF()
    {
        // Use a known private key
        var wif = "KxMn2TgXukYaNXx7tEdjh7qB2YaMgeuKy47j4rvKigHhBuZWeP3r";
        var keyPair = PhantasmaKeys.FromWIF(wif);

        // Assert that the WIF is not null
        Assert.NotNull(keyPair.ToString());
        // Assert that the WIF is the correct WIF for the private key
        Assert.Equal("P2K9zmyFDNGN6n6hHiTUAz6jqn29s5G1SWLiXwCVQcpHcQb", keyPair.ToString());
    }
    
    // Test that new PhantasmaKeys with a valid private key are valid
    [Fact]
    public void IsValid_ValidPrivateKey_ReturnsTrue()
    {
        // Use a known private key
        // Create a new instance of the RNGCryptoServiceProvider class
        RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider();

        // Create a new 32-byte array
        byte[] privateKey = new byte[32];

        // Fill the array with cryptographically secure random bytes
        rngCsp.GetBytes(privateKey);
        var keyPair = new PhantasmaKeys(privateKey);

        // Assert that the private key is not null
        Assert.NotNull(keyPair.PrivateKey);
        // Assert that the private key has the correct length
        Assert.Equal(PhantasmaKeys.PrivateKeyLength, keyPair.PrivateKey.Length);
        // Assert that the public key is not null
        Assert.NotNull(keyPair.PublicKey);
        // Assert that the public key has the correct length
        Assert.Equal(Ed25519.PrivateKeySeedSizeInBytes, keyPair.PublicKey.Length);
        // Assert that the address is not null
        Assert.NotNull(keyPair.Address);
    }
    
    // Test that new PhantasmaKeys with a null private key throws an exception
    [Fact]
    public void NewKeyPair_NullPrivateKey_ThrowsException()
    {
        // Assert that the constructor throws an exception
        Assert.Throws<NullReferenceException>(() => new PhantasmaKeys(null));

        var bytes = new byte[] { 01, 02, 50, 10 };
        Assert.Throws<Exception>(() => new PhantasmaKeys(bytes));
    }
    
    // Test that PhantasmaKeys.Sign correctly signs a message
    [Fact]
    public void Sign_SignsMessage()
    {
        // Use a known private key
        var wif = "KxMn2TgXukYaNXx7tEdjh7qB2YaMgeuKy47j4rvKigHhBuZWeP3r";
        var keyPair = PhantasmaKeys.FromWIF(wif);

        // Use a known message
        var message = "Hello World";

        // Sign the message
        var signature = keyPair.Sign(Encoding.UTF8.GetBytes(message));

        // Assert that the signature is not null
        Assert.NotNull(signature);
        // Assert that the signature has the correct length
        Assert.Equal(Ed25519.SignatureSizeInBytes+1, signature.ToByteArray().Length);
    }

}
