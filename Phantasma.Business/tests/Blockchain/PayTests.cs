using System;
using System.Linq;
using System.Numerics;
using Phantasma.Core.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Numerics;
using Phantasma.Infrastructure.Pay.Chains;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain;

[Collection(nameof(SystemTestCollectionDefinition))]
public class PayTests
{
    [Fact]
    public void TestEthereumWallet()
    {
        var keys = new PhantasmaKeys(Base16.Decode("a95bd75a7b3b1c0a2a14595e8065a95cb06417f6aaedcc3bc45fda52900ab9e8"));
        var wallet = new EthereumWallet(keys);
        var address = wallet.Address;
        Assert.True(address.Equals("0xe57a6c074d1db5ed7c98228df71ce5fa35b6bc72", StringComparison.OrdinalIgnoreCase));
    }

    /*
    [Fact]
    public void TestEOSWallet()
    {
        var wif = "5KA2AqEoo7jqepqeEqK2FjjjgG5nxQN6vfuiSZqgJM79ej6eo4Q";
        byte[] data = wif.Base58CheckDecode();

        byte[] privateKey = new byte[32];
        ByteArrayUtils.CopyBytes(data, 1, privateKey, 0, privateKey.Length);

        var keys = new PhantasmaKeys(privateKey);
        var wallet = new EOSWallet(keys);
        var address = wallet.Address;
        Assert.True(address.Equals("EOS8dBKtG9fbhC1wi1SscL32iFRsSi4PsZDT2EHJcYXwV5dAMiBcK", StringComparison.OrdinalIgnoreCase));
    }*/

    /*
    [Fact]
    public void TestBitcoinWallet()
    {
        var keys = new PhantasmaKeys(Base16.Decode("60cf347dbc59d31c1358c8e5cf5e45b822ab85b79cb32a9f3d98184779a9efc2"));
        var wallet = new BitcoinWallet(keys);
        var address = wallet.Address;
        Assert.True(address.Equals("17JsmEygbbEUEpvt4PFtYaTeSqfb9ki1F1", StringComparison.OrdinalIgnoreCase));
    }*/

    [Fact]
    public void TestEndian()
    {
        var n = new BigInteger(100000000);
        var bytes = n.ToByteArray();
        Assert.True(bytes.Length == 4);
        Assert.True(bytes[0] == 00);
        Assert.True(bytes[1] == 0xe1);
        Assert.True(bytes[2] == 0xf5);
        Assert.True(bytes[3] == 05);
    }

    [Fact]
    public void TestDecodeScriptHash()
    {
        var targetAddress = "2N8bXfrWTzqZoV89dosge2JxvE38VnHurqD";
        var temp = targetAddress.Base58CheckDecode().Skip(1).ToArray();

        byte OP_HASH160 = 0xa9;
        byte OP_EQUAL = 0x87;
        var outputKeyScript = ByteArrayUtils.ConcatBytes(new byte[] { OP_HASH160, 0x14 }, ByteArrayUtils.ConcatBytes(temp, new byte[] { OP_EQUAL }));
        var hex = Base16.Encode(outputKeyScript).ToLower();
    }

}

