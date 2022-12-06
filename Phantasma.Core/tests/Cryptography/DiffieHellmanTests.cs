using System.Linq;
using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Phantasma.Core.Cryptography.ECDsa;
using Phantasma.Core.Numerics;

namespace Phantasma.Core.Tests.Cryptography;

using Xunit;
using Phantasma.Core.Cryptography;

public class DiffieHellmanTests
{
    // Write tests based on DiffieHellman class
    [Fact]
    public void TestDiffieHellman()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("Hello World");
        var bytesKeys = new byte[32];
        var publicKey = new byte[32];
        PhantasmaKeys keys = new PhantasmaKeys(bytesKeys);
        PhantasmaKeys remoteKeys = new PhantasmaKeys(publicKey);
        var dfEnconded = DiffieHellman.Encrypt(bytes, keys.PrivateKey);
        //var secret = DiffieHellman.GetSharedSecret(keys.PrivateKey, publicKey);
        var dfDecoded = DiffieHellman.Decrypt(dfEnconded, keys.PrivateKey);
        var dfDecodedString = Encoding.UTF8.GetString(dfDecoded).Replace("\0", string.Empty);

        // Tests
        Assert.Equal(System.Text.Encoding.UTF8.GetString(bytes), dfDecodedString);
        Assert.NotEqual(bytes.Length, dfDecoded.Length);
        Assert.Equal(32, keys.PrivateKey.Length);
    }
    
    // Write tests based on DiffieHellman class
    [Fact]
    public void TestDiffieHellman2()
    {
        /*var bytes = System.Text.Encoding.UTF8.GetBytes("Hello World");
        var bytesKeys = new byte[32];
        var bytesKeys2 = new byte[32];
        var publicKey = new byte[32];
        
        PhantasmaKeys remoteKeys = new PhantasmaKeys(publicKey);

        PhantasmaKeys keys1 = new PhantasmaKeys(bytesKeys);
        PhantasmaKeys keys2 = new PhantasmaKeys(bytesKeys2);
        
        var dfEnconded = DiffieHellman.Encrypt(bytes, keys1.PrivateKey);
        var dfDecoded = DiffieHellman.Decrypt(dfEnconded, keys1.PrivateKey);
        var dfDecodedString = Encoding.UTF8.GetString(dfDecoded).Replace("\0", string.Empty);
        
        // Tests
        Assert.Equal(System.Text.Encoding.UTF8.GetString(bytes), dfDecodedString);
        Assert.NotEqual(bytes.Length, dfDecoded.Length);
        Assert.Equal(32, keys1.PrivateKey.Length);
        Assert.Equal(32, keys2.PrivateKey.Length);
        
        
        
        var shared = DiffieHellman.GetSharedSecret(remoteKeys.PrivateKey, privateKeyParameters.PublicKeyParamSet.GetEncoded());
  //      var shared2 = DiffieHellman.GetSharedSecret(keys2.PrivateKey, keys1.PublicKey);
    //    Assert.Equal(shared, shared2);*/

        //var secret = DiffieHellman.GetSharedSecret(keys.PrivateKey, publicKey);

    }
}
