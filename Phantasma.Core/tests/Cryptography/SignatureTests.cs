using System;
using System.IO;
using System.Text;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.TransactionData;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;
using Tendermint.Version;
using Xunit;

namespace Phantasma.Core.Tests.Cryptography;

public class SignatureTests
{
    // TODO
    
    [Fact]
    public void TestSignature()
    {
        var nexusName = "testnet";
        var chainName = "main";
        var script = new byte[] { 0x01, 0x02, 0x03 };
        var expiration = new Timestamp(1234567890);
        var payload = "payload";
        var keys = PhantasmaKeys.FromWIF("L5UEVHBjujaR1721aZM5Zm5ayjDyamMZS9W35RE9Y9giRkdf3dVx");
        
        Transaction tx = Transaction.Null;
        tx = new Transaction(nexusName, chainName, script, expiration, payload);
        
        tx.Sign(keys);
        var bytes = new byte[]
        {
            7, 116, 101, 115, 116, 110, 101, 116,   4, 109,  97, 105,
            110,   3,   1,   2,   3, 210,   2, 150,  73,   7, 112,  97,
            121, 108, 111,  97, 100,   1,   1,  64,  76,   3,  56,  89,
            162,  10,  79, 194, 228, 105, 179, 116,  31, 176,  90, 206,
            223, 236,  36, 191, 233,  46,   7,  99,  54, 128,  72, 134,
            101, 215, 159, 145, 103, 115, 255,  64, 208, 232,  28,  68,
            104, 225, 193,  72, 126, 110,  30, 110, 239, 218,  92,  93,
            124,  83, 193,  92,  79, 179,  73, 194,  52, 154,  24,   2
        };

        var txFromTS = Transaction.Unserialize(bytes);
        
        Assert.Equal(tx.NexusName, txFromTS.NexusName);
        Assert.Equal(tx.ChainName, txFromTS.ChainName);
        Assert.Equal(tx.Script, txFromTS.Script);
        Assert.Equal(tx.Expiration, txFromTS.Expiration);
        Assert.Equal(tx.Payload, txFromTS.Payload);
        Assert.Equal(tx.Signatures.Length, txFromTS.Signatures.Length);
        Assert.Equal(tx.Signatures[0].Kind, txFromTS.Signatures[0].Kind);
        Assert.Equal(tx.Signatures[0].ToByteArray(), txFromTS.Signatures[0].ToByteArray());
        Assert.Equal(tx.Hash, txFromTS.Hash);
    }

    [Fact]
    public void TestMyTest()
    {
        var encoded =
            "07746573746e6574046d61696efd1c03304430303033303335303334303330333030304430303033303231303237303330303044303030323233323230303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303033303030443030303432463530333234423339374136443739343634343445343734453336364533363638343836393534353534313741333636413731364533323339373333353437333135333537344336393538373734333536353136333730343836333531363230333030304430303034303834313643364336463737343736313733303330303044303030343033363736313733324430303031324530313044303030333031303130333030304430303034303933323331333333393332333333313333333030333030304430303035303435353146443136333033303030443030303530343746434443463633303330303044303030373034303130303030303030333030304430303034304137363631364336393634363137343646373237333033303030443030303431363645363537383735373332453730373236463734364636333646364332453736363537323733363936463645303330303044303030343246353033323442333937413644373934363434344534373445333636453336363834383639353435353431374133363641373136453332333937333335343733313533353734433639353837373433353635313633373034383633353136323033303030443030303430383439364536393734353036463643364330333030304430303034303936333646364537333635364537333735373332443030303132453031304430303034324635303332344233393741364437393436343434453437344533363645333636383438363935343535343137413336364137313645333233393733333534373331353335373443363935383737343335363531363337303438363335313632303330303044303030343038353337303635364536343437363137333033303030443030303430333637363137333244303030313245303130427fcdcf63123633364636453733363536453733373537330101406b4ccd63c838cc1a4ccba10579d4e8768058c23edb77c560597dc0105755879d9eac3b2be5ba67d5dcadd2e132114dc96e42ccc0f8c7114ec0a15d3249a6a702";
        var decode = Base16.Decode(encoded);
        var tx = Transaction.Unserialize(decode);
        var payload = Encoding.UTF8.GetBytes("636F6E73656E737573");
        var addr = Address.FromText("S3dGUjVwYa31AxdthdpsuyBKgX1N65FnoQhUkSgYbUEdRp4");
        
        var script = ScriptUtils.BeginScript()
            .AllowGas(addr, Address.Null, 10000, 210000)
            //.CallContract(NativeContractKind.Consensus, nameof(ConsensusContract.InitPoll), )
            .SpendGas(addr)
            .EndScript();
        
        Assert.Equal("testnet", tx.NexusName);
        Assert.Equal("main", tx.ChainName);
        Assert.NotEqual(new byte[0], tx.Script);
        Assert.Equal(payload, tx.Payload);
        Assert.Equal(1, tx.Signatures.Length);
        Assert.Equal(SignatureKind.Ed25519, tx.Signatures[0].Kind);
        Assert.NotEqual(new byte[0], tx.Signatures[0].ToByteArray());
        
    }
}
