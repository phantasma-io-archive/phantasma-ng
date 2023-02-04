using Xunit;
using Phantasma.Core.Cryptography.ECDsa;
using Phantasma.Core.Numerics;
using System;
using System.Text;
using Phantasma.Core.Cryptography;
using Phantasma.Node.Chains.Ethereum;
using Phantasma.Node.Chains.Neo2;

// Testing ECDsa signature
// Testing methods:
// bool Verify(byte[] message, IEnumerable<Address> addresses)
// ECDsaSignature Generate(IKeyPair keypair, byte[] message, ECDsaCurve curve, Func<byte[], byte[], byte[], byte[]> customSignFunction = null)
// ExtractPublicKeyFromAddress(Address address)

namespace Phantasma.Core.Tests.Cryptography.ECDsa;
public class CryptoECDsaSignatureTests
{
    [Fact]
    public void GenerateVerifyNeoTest()
    {
        var curve = ECDsaCurve.Secp256r1;

        var msg = "Hello Phantasma!";
        var msgBytes = Encoding.ASCII.GetBytes(msg);

        var msgIncorrect = "Hello Fhantasma!";
        var msgIncorrectBytes = Encoding.ASCII.GetBytes(msgIncorrect);

        var keys = NeoKeys.Generate();
        Assert.True(keys.PrivateKey.Length == PhantasmaKeys.PrivateKeyLength);

        var keysIncorrect = NeoKeys.Generate();
        Assert.True(keysIncorrect.PrivateKey.Length == PhantasmaKeys.PrivateKeyLength);

        var ecdsaSignature = ECDsaSignature.Generate(keys, msgBytes, curve);

        Console.WriteLine("ecdsaSignature.Bytes: " + Base16.Encode(ecdsaSignature.Bytes));

        var bytes = new byte[34];
        bytes[0] = (byte)AddressKind.User;
        Core.Utils.ByteArrayUtils.CopyBytes(keys.PublicKey, 0, bytes, 1, 33);
        var address = Address.FromBytes(bytes);

        var bytes2 = new byte[34];
        bytes[0] = (byte)AddressKind.User;
        Core.Utils.ByteArrayUtils.CopyBytes(keysIncorrect.PublicKey, 0, bytes2, 1, 33);
        var addressIncorrect = Address.FromBytes(bytes2);

        // Check correct message and address
        Assert.True(ecdsaSignature.Verify(msgBytes, new Address[] { address }));
        // Check incorrect message
        Assert.False(ecdsaSignature.Verify(msgIncorrectBytes, new Address[] { address }));
        // Check incorrect address
        Assert.False(ecdsaSignature.Verify(msgBytes, new Address[] { addressIncorrect }));
    }
    [Fact]
    public void GenerateVerifyEthTest()
    {
        var curve = ECDsaCurve.Secp256k1;

        var msg = "Hello Phantasma!";
        var msgBytes = Encoding.ASCII.GetBytes(msg);

        var msgIncorrect = "Hello Fhantasma!";
        var msgIncorrectBytes = Encoding.ASCII.GetBytes(msgIncorrect);

        var keys = EthereumKey.Generate();
        Assert.True(keys.PrivateKey.Length == PhantasmaKeys.PrivateKeyLength);

        var keysIncorrect = EthereumKey.Generate();
        Assert.True(keysIncorrect.PrivateKey.Length == PhantasmaKeys.PrivateKeyLength);

        var ecdsaSignature = ECDsaSignature.Generate(keys, msgBytes, curve);

        Console.WriteLine("ecdsaSignature.Bytes: " + Base16.Encode(ecdsaSignature.Bytes));

        var bytes = new byte[34];
        bytes[0] = (byte)AddressKind.User;
        Core.Utils.ByteArrayUtils.CopyBytes(keys.PublicKey, 0, bytes, 1, 33);
        var address = Address.FromBytes(bytes);

        var bytes2 = new byte[34];
        bytes[0] = (byte)AddressKind.User;
        Core.Utils.ByteArrayUtils.CopyBytes(keysIncorrect.PublicKey, 0, bytes2, 1, 33);
        var addressIncorrect = Address.FromBytes(bytes2);

        // Check correct message and address
        Assert.True(ecdsaSignature.Verify(msgBytes, new Address[] { address }));
        // Check incorrect message
        Assert.False(ecdsaSignature.Verify(msgIncorrectBytes, new Address[] { address }));
        // Check incorrect address
        Assert.False(ecdsaSignature.Verify(msgBytes, new Address[] { addressIncorrect }));
    }
    [Fact]
    public void VerifyNeoPredefinedTest()
    {
        var curve = ECDsaCurve.Secp256r1;

        var wif = "KwPpBSByydVKqStGHAnZzQofCqhDmD2bfRgc9BmZqM3ZmsdWJw4d";
        var wifIncorrect = "KzCR1GkfdVqnkXD6nvJnTVP8doXGYtBiSC5wCwf4o33QS2DwLpeJ";
        
        var signatureHex = "BE92E117E4C7829B90BF70764089B83ABBC41B3A017937E1F714FC88D475B47568D8961600964EEB6C6AEA5763A8416B946248C1A5A39D4DE29F0EE3B8A5D320";
        var signatureIncorrectHex = "BE92E117E4C7829B90BF70764089B83ABBC41B3A017937E1F714FC88D475B47568D8961600964EEB6C6AEA5763A8416B946248C1A5A39D4DE29F0EE3B8A5D321";

        var msg = "Hello Phantasma!";
        var msgBytes = Encoding.ASCII.GetBytes(msg);

        var msgIncorrect = "Hello Fhantasma!";
        var msgIncorrectBytes = Encoding.ASCII.GetBytes(msgIncorrect);

        var ecdsaSignature = new ECDsaSignature(Base16.Decode(signatureHex), curve);
        var ecdsaSignatureIncorrect = new ECDsaSignature(Base16.Decode(signatureIncorrectHex), curve);

        var keys = NeoKeys.FromWIF(wif);
        Assert.True(keys.PrivateKey.Length == PhantasmaKeys.PrivateKeyLength);

        var keysIncorrect = NeoKeys.FromWIF(wifIncorrect);
        Assert.True(keysIncorrect.PrivateKey.Length == PhantasmaKeys.PrivateKeyLength);

        var bytes = new byte[34];
        bytes[0] = (byte)AddressKind.User;
        Core.Utils.ByteArrayUtils.CopyBytes(keys.PublicKey, 0, bytes, 1, 33);
        var address = Address.FromBytes(bytes);

        var bytes2 = new byte[34];
        bytes[0] = (byte)AddressKind.User;
        Core.Utils.ByteArrayUtils.CopyBytes(keysIncorrect.PublicKey, 0, bytes2, 1, 33);
        var addressIncorrect = Address.FromBytes(bytes2);

        // Check correct message and address
        Assert.True(ecdsaSignature.Verify(msgBytes, new Address[] { address }));
        
        // Check incorrect message
        Assert.False(ecdsaSignature.Verify(msgIncorrectBytes, new Address[] { address }));

        // Check incorrect address
        Assert.False(ecdsaSignature.Verify(msgBytes, new Address[] { addressIncorrect }));

        // Check incorrect signature
        Assert.False(ecdsaSignatureIncorrect.Verify(msgBytes, new Address[] { address }));
    }
    [Fact]
    public void VerifyEthPredefinedTest()
    {
        var curve = ECDsaCurve.Secp256k1;

        var wif = "KwPpBSByydVKqStGHAnZzQofCqhDmD2bfRgc9BmZqM3ZmsdWJw4d";
        var wifIncorrect = "KzCR1GkfdVqnkXD6nvJnTVP8doXGYtBiSC5wCwf4o33QS2DwLpeJ";

        var signatureHex = "AD19BE0BD2EF66DA9D7EEC7E89A7CD6613D16F205BD4F807E6794C740BA278C5C52D02B38B1576417B7F9FCC21079E83D2ED429FA6C528097BFB0E48D19BAD9B";
        var signatureIncorrectHex = "AD19BE0BD2EF66DA9D7EEC7E89A7CD6613D16F205BD4F807E6794C740BA278C5C52D02B38B1576417B7F9FCC21079E83D2ED429FA6C528097BFB0E48D19BAD9C";

        var msg = "Hello Phantasma!";
        var msgBytes = Encoding.ASCII.GetBytes(msg);

        var msgIncorrect = "Hello Fhantasma!";
        var msgIncorrectBytes = Encoding.ASCII.GetBytes(msgIncorrect);

        var ecdsaSignature = new ECDsaSignature(Base16.Decode(signatureHex), curve);
        var ecdsaSignatureIncorrect = new ECDsaSignature(Base16.Decode(signatureIncorrectHex), curve);

        var keys = EthereumKey.FromWIF(wif);
        Assert.True(keys.PrivateKey.Length == PhantasmaKeys.PrivateKeyLength);

        var keysIncorrect = EthereumKey.FromWIF(wifIncorrect);
        Assert.True(keysIncorrect.PrivateKey.Length == PhantasmaKeys.PrivateKeyLength);

        var bytes = new byte[34];
        bytes[0] = (byte)AddressKind.User;
        Core.Utils.ByteArrayUtils.CopyBytes(keys.PublicKey, 0, bytes, 1, 33);
        var address = Address.FromBytes(bytes);

        var bytes2 = new byte[34];
        bytes[0] = (byte)AddressKind.User;
        Core.Utils.ByteArrayUtils.CopyBytes(keysIncorrect.PublicKey, 0, bytes2, 1, 33);
        var addressIncorrect = Address.FromBytes(bytes2);

        // Check correct message and address
        Assert.True(ecdsaSignature.Verify(msgBytes, new Address[] { address }));

        // Check incorrect message
        Assert.False(ecdsaSignature.Verify(msgIncorrectBytes, new Address[] { address }));

        // Check incorrect address
        Assert.False(ecdsaSignature.Verify(msgBytes, new Address[] { addressIncorrect }));

        // Check incorrect signature
        Assert.False(ecdsaSignatureIncorrect.Verify(msgBytes, new Address[] { address }));
    }

    [Fact]
    public void Test_ECDsaSignature_Verify()
    {
        var curve = (ECDsaCurve)10;

        var wif = "KwPpBSByydVKqStGHAnZzQofCqhDmD2bfRgc9BmZqM3ZmsdWJw4d";
        
        var signatureHex = "AD19BE0BD2EF66DA9D7EEC7E89A7CD6613D16F205BD4F807E6794C740BA278C5C52D02B38B1576417B7F9FCC21079E83D2ED429FA6C528097BFB0E48D19BAD9B";

        var msg = "Hello Phantasma!";
        var msgBytes = Encoding.ASCII.GetBytes(msg);

        var ecdsaSignature = new ECDsaSignature(Base16.Decode(signatureHex), curve);

        var keys = EthereumKey.FromWIF(wif);
        Assert.True(keys.PrivateKey.Length == PhantasmaKeys.PrivateKeyLength);

        var bytes = new byte[34];
        bytes[0] = (byte)AddressKind.User;
        Core.Utils.ByteArrayUtils.CopyBytes(keys.PublicKey, 0, bytes, 1, 33);
        var address = Address.FromBytes(bytes);

        // Check correct message and address
        Assert.Throws<Exception>(() => ecdsaSignature.Verify(msgBytes, new Address[] { address }));
        
    }
}
