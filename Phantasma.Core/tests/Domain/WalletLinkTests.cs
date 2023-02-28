using System;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Unicode;
using System.Threading;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Numerics;
using Phantasma.Core.Utils;
using Phantasma.Infrastructure.Pay;
using Phantasma.Core.Cryptography;
using Phantasma.Node.Chains.Ethereum;

namespace Phantasma.Core.Tests.Domain;

using Xunit;

using Phantasma.Core.Domain;
using Phantasma.Business.Blockchain.Contracts.Native;

public class WalletLinkTests
{
    // Create a class based on WalletLink to test the protected methods
    public class LinkSimulator : WalletLink
    {
        [Flags]
        public enum PlatformKind
        {
            None = 0x0,
            Phantasma = 0x1,
            Neo = 0x2,
            Ethereum = 0x4,
            BSC = 0x8,
        }

        private Nexus _nexus;
        private string _name;
        private MyAccount _account;

        public LinkSimulator(Nexus Nexus, string name, MyAccount account) : base()
        {
            this._nexus = Nexus;
            this._name = name;
            this._account = account;
            
        }

        public override string Nexus => _nexus.Name;

        public override string Name => _name;

        protected override WalletStatus Status => WalletStatus.Ready;

        private PlatformKind RequestPlatform(string platform)
        {
            PlatformKind targetPlatform;

            if (!Enum.TryParse<PlatformKind>(platform, true, out targetPlatform))
            {
                return PlatformKind.None;
            }

            if (!_account.CurrentPlatform.HasFlag(targetPlatform))
            {
                return PlatformKind.None;
            }

            if (_account.CurrentPlatform != targetPlatform)
            {
                _account.CurrentPlatform = targetPlatform;
            }

            return targetPlatform;
        }

        protected override void Authorize(string dapp, string token, int version, Action<bool, string> callback)
        {            
            if (version > LinkProtocol)
            {
                callback(false, "unknown Phantasma Link version " + version);
                return;
            }
            
            if (_account.CurrentPlatform != PlatformKind.Phantasma)
                _account.CurrentPlatform = PlatformKind.Phantasma;
            
            var state = Status;
            if (state == null)
            {
                callback(false, "not logged in");
                return;
            }

            var result = true;
                
            //state.RegisterDappToken(dapp, token);
            callback(result, result ? null : "rejected");
        }

        protected override void GetAccount(string platform, Action<Account, string> callback)
        {
            callback(new Account(), null);
        }

        protected override void InvokeScript(string chain, byte[] script, int id, Action<byte[], string> callback)
        {
            if (id >= 2 && id <= 4)
            {
                var result = Encoding.UTF8.GetBytes("test");
                callback(result, null);
            }
            else
            {
                callback(null, "not implemented");
            }
        }

        protected override void SignData(string platform, SignatureKind kind, byte[] data, int id, Action<string, string, string> callback)
        {
            var targetPlatform = RequestPlatform(platform);
            if (targetPlatform == PlatformKind.None)
            {
                callback(null, null, "Unsupported platform: " + platform);
                return;
            }

            var state = Status;
            if (state != WalletStatus.Ready)
            {
                callback(null, null, "not logged in");
                return;
            }

            var account = _account;

            var description = System.Text.Encoding.UTF8.GetString(data);

            var randomValue = new Random().Next(0, int.MaxValue);
            var randomBytes = BitConverter.GetBytes(randomValue);

            var msg = ByteArrayUtils.ConcatBytes(randomBytes, data);
            Core.Cryptography.Signature signature;
            
            switch (kind)
            {
                case SignatureKind.Ed25519:
                    var phantasmaKeys = account.keys;
                    signature = phantasmaKeys.Sign(msg);
                    break;

                case SignatureKind.ECDSA:
                    var ethKeys = new EthereumKey(account.keys.PrivateKey);
                    signature = ethKeys.Sign(msg);
                    break;

                default:
                    callback(null, null, kind + " signatures unsupported");
                    return;
            }

            byte[] sigBytes = null;

            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.WriteSignature(signature);
                }

                sigBytes = stream.ToArray();
            }

            var hexSig = Base16.Encode(sigBytes);
            var hexRand = Base16.Encode(randomBytes);

            callback(hexSig, hexRand, null);
        }
        
        public void forceSignData(string platform, SignatureKind kind, byte[] data, int id, Action<string, string, string> callback)
        {
            SignData(platform, kind, data, id, callback);
        }

        protected override void SignTransaction(string platform, SignatureKind kind, string chain, byte[] script, byte[] payload, int id, Action<Hash, string> callback)
        {
            var hash = new Hash(CryptoExtensions.Sha256("test"));
            if (id >= 5)
            {
                callback(Hash.Null, "not logged in");
            }else if ( platform == "phantasma")
                callback(hash, null);
            else
                callback(Hash.Null, "not logged in");
        }

        protected override void WriteArchive(Hash hash, int blockIndex, byte[] data, Action<bool, string> callback)
        {
            if ( hash == Hash.Null)
                callback(false, "not logged in");
            else
                callback(true, null);
        }
        
        public class MyAccount
        {
            public MyAccount Instance { get; private set; }
            public PhantasmaKeys keys;
            public PlatformKind CurrentPlatform;
            public string token;

            public MyAccount(PhantasmaKeys keys, PlatformKind platform)
            {
                Instance = this;
                this.keys = keys;
                CurrentPlatform = platform;
            }
        }
    }

    [Fact]
    public void TestWalletLinkConstructor()
    {
        var owner = PhantasmaKeys.Generate();
        
        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;
        
        var testUser1 = PhantasmaKeys.Generate();
        var account1 = new LinkSimulator.MyAccount(testUser1, LinkSimulator.PlatformKind.Phantasma);
        LinkSimulator link1 = new LinkSimulator(nexus, "Ac1", account1);
        
        Assert.Equal("simnet", link1.Nexus);
        Assert.Equal("Ac1", link1.Name);
    }

    [Fact]
    public void TestWalletLinkExecute_Authorize()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var testUser1 = PhantasmaKeys.Generate();
        var account1 = new LinkSimulator.MyAccount(testUser1, LinkSimulator.PlatformKind.Phantasma);
        LinkSimulator link1 = new LinkSimulator(nexus, "Ac1", account1);

        link1.Execute("authorize", ((id, node, sucess) =>
        {
            Assert.Equal(0, id);
            Assert.Equal("{\"message\":\"Invalid request id\"}", node.ToJsonString());
            Assert.False(sucess);
        }));
        
        link1.Execute("1,authorize,2", ((id, node, sucess) =>
        {
            Assert.Equal(1, id);
            Assert.Equal("{\"message\":\"Malformed request\"}", node.ToJsonString());
            Assert.False(sucess);
        }));
        
        link1.Execute("1,authorize", ((id, node, sucess) =>
        {
            Assert.Equal(1, id);
            Assert.Equal("{\"message\":\"authorize: Invalid amount of arguments: 0\"}", node.ToJsonString());
            Assert.False(sucess);
        }));
        
        link1.Execute("1,authorize/testDapp/x", ((id, node, sucess) =>
        {
            Assert.Equal(1, id);
            Assert.Equal("{\"message\":\"authorize: Invalid version: x\"}", node.ToJsonString());
            Assert.False(sucess);
        }));
        
        link1.Execute("1,authorize/testDapp/1", ((id, node, sucess) =>
        {
            // Get Token from node
            var token = node["token"].AsValue();
            var expexted = APIUtils.FromAPIResult(new WalletLink.Authorization() { wallet = "Ac1", nexus = nexus.Name, dapp = "testDapp", token = token.ToString(), version = 0 });
            Assert.Equal(1, id);
            Assert.Equal(expexted.ToJsonString(), node.ToJsonString());
            Assert.True(sucess);
        }));

        link1.Execute("2,authorize/testDapp/2", ((id, node, sucess) =>
        {
            Assert.Equal(2, id);
            var token = node["token"].AsValue();
            var expexted = APIUtils.FromAPIResult(new WalletLink.Authorization() { wallet = "Ac1", nexus = nexus.Name, dapp = "testDapp", token = token.ToString(), version = 1 });
            Assert.Equal(2, id);
            Assert.Equal(expexted.ToJsonString(), node.ToJsonString());
            Assert.True(sucess);
        }));
        
        // Fail
        link1.Execute("3,authorize/testDapp/4", ((id, node, sucess) =>
        {
            Assert.Equal(3, id);
            var token = node["token"].AsValue();
            var expexted = APIUtils.FromAPIResult(new WalletLink.Authorization() { wallet = "Ac1", nexus = nexus.Name, dapp = "testDapp", token = token.ToString(), version = 1 });
            Assert.Equal(3, id);
            Assert.Equal(expexted.ToJsonString(), node.ToJsonString());
            Assert.True(sucess);
        }));
    }

    [Fact]
    public void TestAuthorizeFail()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var testUser1 = PhantasmaKeys.Generate();
        var account1 = new LinkSimulator.MyAccount(testUser1, LinkSimulator.PlatformKind.Phantasma);
        LinkSimulator link1 = new LinkSimulator(nexus, "Ac1", account1);
        
        
        link1.Execute("1,authorize/testDapp/4", ((id, node, sucess) =>
        {
            Assert.Equal(1, id);
            Assert.Equal("{\"message\":\"unknown Phantasma Link version 4\"}", node.ToJsonString());
            Assert.False(sucess);
        }));
    }

    [Fact]
    public void TestGetAccount()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var testUser1 = PhantasmaKeys.Generate();
        var account1 = new LinkSimulator.MyAccount(testUser1, LinkSimulator.PlatformKind.Phantasma);
        LinkSimulator link1 = new LinkSimulator(nexus, "Ac1", account1);
        string token = "";
        link1.Execute("1,authorize/testDapp/1", ((id, node, sucess) =>
        {
            // Get Token from node
            token = node["token"].AsValue().ToString();
            var expexted = APIUtils.FromAPIResult(new WalletLink.Authorization() { wallet = "Ac1", nexus = nexus.Name, dapp = "testDapp", token = token.ToString(), version = 0 });
            Assert.Equal(1, id);
            Assert.Equal(expexted.ToJsonString(), node.ToJsonString());
            Assert.True(sucess);
            link1.Execute("2,getAccount", ((id, node, sucess) =>
            {
                Assert.Equal(2, id);
                Assert.Equal("{\"message\":\"A previous request is still pending\"}", node.ToJsonString());
                Assert.False(sucess);
            }));
        }));

        link1.Execute($"2,getAccount", ((id, node, sucess) =>
        {
            Assert.Equal(2, id);
            Assert.Equal("{\"message\":\"Invalid or missing API token\"}", node.ToJsonString());
            Assert.False(sucess);
        }));
        
        link1.Execute($"2,testDapp,{token},getAccount", ((id, node, sucess) =>
        {
            Assert.Equal(2, id);
            Assert.Equal("{\"message\":\"Malformed request\"}", node.ToJsonString());
            Assert.False(sucess);
        }));

        
        Thread.Sleep(2000);
        link1.Execute($"2,getAccount/testDapp/{token}", ((id, node, sucess) =>
        {
            Assert.Equal(2, id);
            var expexted = APIUtils.FromAPIResult(new WalletLink.Account());
            Assert.Equal(expexted.ToJsonString(), node.ToJsonString());
            Assert.True(sucess);
        }));
    }

    [Fact]
    public void TestSignData()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var testUser1 = PhantasmaKeys.Generate();
        var account1 = new LinkSimulator.MyAccount(testUser1, LinkSimulator.PlatformKind.Phantasma);
        LinkSimulator link1 = new LinkSimulator(nexus, "Ac1", account1);
        string token = "";
        var dataEncoded = Encoding.UTF8.GetBytes("test");
        var base16 = Base16.Encode(dataEncoded);
        
        link1.Execute("1,authorize/testDapp/1", ((id, node, sucess) =>
        {
            // Get Token from node
            token = node["token"].AsValue().ToString();
            
            var expexted = APIUtils.FromAPIResult(new WalletLink.Authorization() { wallet = "Ac1", nexus = nexus.Name, dapp = "testDapp", token = token.ToString(), version = 0 });
            Assert.Equal(1, id);
            Assert.Equal(expexted.ToJsonString(), node.ToJsonString());
            Assert.True(sucess);
        }));
        
        Thread.Sleep(2000);
        link1.Execute($"2,signData/{base16}/{SignatureKind.Ed25519}/testDapp/{token}", ((id, node, sucess) =>
        {
            Assert.Equal(2, id);
            var expexted = APIUtils.FromAPIResult(new WalletLink.Signature());
            Assert.True(node.ToJsonString().Contains("signature"));
            Assert.True(sucess);
        }));
        
        link1.Execute($"3,signData/oasdjaosd/{SignatureKind.Ed25519}/testDapp/{token}", ((id, node, sucess) =>
        {
            Assert.Equal(3, id);
            var expexted = APIUtils.FromAPIResult(new WalletLink.Error() { message = $"signTx: Invalid input received" });
            Assert.Equal(expexted.ToJsonString(), node.ToJsonString());
            Assert.False(sucess);
        }));
        
        link1.Execute($"4,signData/oasdjaosd/{SignatureKind.Ed25519}/asdojsaodjs/testDapp/{token}", ((id, node, sucess) =>
        {
            Assert.Equal(4, id);
            var expexted = APIUtils.FromAPIResult(new WalletLink.Error() { message = $"signTx: Invalid amount of arguments: 3" });
            Assert.Equal(expexted.ToJsonString(), node.ToJsonString());
            Assert.False(sucess);
        }));
    }

    [Fact]
    public void TestSignTx()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var testUser1 = PhantasmaKeys.Generate();
        var account1 = new LinkSimulator.MyAccount(testUser1, LinkSimulator.PlatformKind.Phantasma);
        LinkSimulator link1 = new LinkSimulator(nexus, "Ac1", account1);
        string token = "";
        var dataEncoded = Encoding.UTF8.GetBytes("test");
        var base16 = Base16.Encode(dataEncoded);
        
        link1.Execute("1,authorize/testDapp/1", ((id, node, sucess) =>
        {
            // Get Token from node
            token = node["token"].AsValue().ToString();
            
            var expexted = APIUtils.FromAPIResult(new WalletLink.Authorization() { wallet = "Ac1", nexus = nexus.Name, dapp = "testDapp", token = token.ToString(), version = 0 });
            Assert.Equal(1, id);
            Assert.Equal(expexted.ToJsonString(), node.ToJsonString());
            Assert.True(sucess);
        }));
        
        // Prepare a transaction
        var script = new ScriptBuilder().
            AllowGas(testUser1.Address, Address.Null, 1, 9999).
            CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser1.Address, UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals)).
            SpendGas(testUser1.Address).
            EndScript();

        var myScript = Base16.Encode(script);
        var payload = Base16.Encode(Encoding.UTF8.GetBytes("TestPayload"));
        
        link1.Execute($"2,signTx/simnet/main/{myScript}/{payload}/testDapp/{token}", ((id, node, sucess) =>
        {
            Assert.Equal(2, id);
            var expexted = APIUtils.FromAPIResult(new WalletLink.Signature());
            Assert.True(node.ToJsonString().Contains("hash"));
            Assert.True(sucess);
        }));
        
        link1.Execute($"3,signTx/test/main/{myScript}/{payload}/testDapp/{token}", ((id, node, sucess) =>
        {
            Assert.Equal(3, id);
            var expexted = APIUtils.FromAPIResult(new WalletLink.Error() { message = $"signTx: Expected nexus simnet, instead got test" });
            Assert.Equal(expexted.ToJsonString(), node.ToJsonString());
            Assert.False(sucess);
        }));
        
        link1.Execute($"4,signTx/simnet/main/null/{payload}/testDapp/{token}", ((id, node, sucess) =>
        {
            Assert.Equal(4, id);
            var expexted = APIUtils.FromAPIResult(new WalletLink.Error() { message = $"signTx: Invalid script data" });
            Assert.Equal(expexted.ToJsonString(), node.ToJsonString());
            Assert.False(sucess);
        }));
        
        link1.Execute($"5,signTx/simnet/main/{myScript}/{payload}/testDapp/{token}", ((id, node, sucess) =>
        {
            Assert.Equal(5, id);
            var expexted = APIUtils.FromAPIResult(new WalletLink.Error() { message = $"not logged in" });
            Assert.Equal(expexted.ToJsonString(), node.ToJsonString());
            Assert.False(sucess);
        }));
        
        link1.Execute($"6,signTx/simnet/main/{myScript}/{payload}/ajksdnjka/testDapp/{token}", ((id, node, sucess) =>
        {
            Assert.Equal(6, id);
            var expexted = APIUtils.FromAPIResult(new WalletLink.Error() { message = $"signTx: Invalid amount of arguments: 5" });
            Assert.Equal(expexted.ToJsonString(), node.ToJsonString());
            Assert.False(sucess);
        }));
    }

    [Fact]
    public void TestInvokeScript()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var testUser1 = PhantasmaKeys.Generate();
        var account1 = new LinkSimulator.MyAccount(testUser1, LinkSimulator.PlatformKind.Phantasma);
        LinkSimulator link1 = new LinkSimulator(nexus, "Ac1", account1);
        string token = "";
        var dataEncoded = Encoding.UTF8.GetBytes("test");
        var base16 = Base16.Encode(dataEncoded);
        
        link1.Execute("1,authorize/testDapp/1", ((id, node, sucess) =>
        {
            // Get Token from node
            token = node["token"].AsValue().ToString();
            
            var expexted = APIUtils.FromAPIResult(new WalletLink.Authorization() { wallet = "Ac1", nexus = nexus.Name, dapp = "testDapp", token = token.ToString(), version = 0 });
            Assert.Equal(1, id);
            Assert.Equal(expexted.ToJsonString(), node.ToJsonString());
            Assert.True(sucess);
        }));
        
        // Prepare a transaction
        var script = new ScriptBuilder().
            CallContract(NativeContractKind.Stake, nameof(StakeContract.GetRate)).
            EndScript();

        var myScript = Base16.Encode(script);
        var payload = Base16.Encode(Encoding.UTF8.GetBytes("TestPayload"));
        
        link1.Execute($"2,invokeScript/main/{myScript}/testDapp/{token}", ((id, node, sucess) =>
        {
            Assert.Equal(2, id);
            var expexted = APIUtils.FromAPIResult(new WalletLink.Invocation());
            Assert.True(node.ToJsonString().Contains("result"));
            Assert.True(sucess);
        }));
        
        link1.Execute($"3,invokeScript/main/null/testDapp/{token}", ((id, node, sucess) =>
        {
            Assert.Equal(3, id);
            var expexted = APIUtils.FromAPIResult(new WalletLink.Error() { message = $"signTx: Invalid script data" });
            Assert.Equal(expexted.ToJsonString(), node.ToJsonString());
            Assert.False(sucess);
        }));
        
        link1.Execute($"4,invokeScript/main/null/test/testDapp/{token}", ((id, node, sucess) =>
        {
            Assert.Equal(4, id);
            var expexted = APIUtils.FromAPIResult(new WalletLink.Error() { message = $"invokeScript: Invalid amount of arguments: 3" });
            Assert.Equal(expexted.ToJsonString(), node.ToJsonString());
            Assert.False(sucess);
        }));
        
        link1.Execute($"5,invokeScript/main/{myScript}/testDapp/{token}", ((id, node, sucess) =>
        {
            Assert.Equal(5, id);
            var expexted = APIUtils.FromAPIResult(new WalletLink.Error() { message = $"not implemented" });
            Assert.Equal(expexted.ToJsonString(), node.ToJsonString());
            Assert.False(sucess);
        }));
    }

    [Fact]
    public void TestWriteArchive()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var testUser1 = PhantasmaKeys.Generate();
        var account1 = new LinkSimulator.MyAccount(testUser1, LinkSimulator.PlatformKind.Phantasma);
        LinkSimulator link1 = new LinkSimulator(nexus, "Ac1", account1);
        string token = "";
        var dataEncoded = Encoding.UTF8.GetBytes("test");
        var base16 = Base16.Encode(dataEncoded);
        var hash = new Hash(CryptoExtensions.Sha256("test"));
        
        link1.Execute("1,authorize/testDapp/1", ((id, node, sucess) =>
        {
            // Get Token from node
            token = node["token"].AsValue().ToString();
            
            var expexted = APIUtils.FromAPIResult(new WalletLink.Authorization() { wallet = "Ac1", nexus = nexus.Name, dapp = "testDapp", token = token.ToString(), version = 0 });
            Assert.Equal(1, id);
            Assert.Equal(expexted.ToJsonString(), node.ToJsonString());
            Assert.True(sucess);
        }));
        
        // Prepare a transaction
        var script = new ScriptBuilder().
            CallContract(NativeContractKind.Stake, nameof(StakeContract.GetRate)).
            EndScript();

        var myScript = Base16.Encode(script);
        var payload = Base16.Encode(Encoding.UTF8.GetBytes("TestPayload"));
        
        link1.Execute($"2,writeArchive/{hash}/1/{base16}/testDapp/{token}", ((id, node, sucess) =>
        {
            Assert.Equal(2, id);
            var expexted = APIUtils.FromAPIResult(new WalletLink.Signature());
            Assert.True(node.ToJsonString().Contains("hash"));
            Assert.True(sucess);
        }));
        
        link1.Execute($"3,writeArchive/{Hash.Null}/1/{base16}/testDapp/{token}", ((id, node, sucess) =>
        {
            Assert.Equal(3, id);
            var expexted = APIUtils.FromAPIResult(new WalletLink.Error() { message = $"not logged in" });
            Assert.Equal(expexted.ToJsonString(), node.ToJsonString());
            Assert.False(sucess);
        }));
        
        link1.Execute($"4,writeArchive/{Hash.Null}/1/{base16}/asd/testDapp/{token}", ((id, node, sucess) =>
        {
            Assert.Equal(4, id);
            var expexted = APIUtils.FromAPIResult(new WalletLink.Error() { message = $"writeArchive: Invalid amount of arguments: 4" });
            Assert.Equal(expexted.ToJsonString(), node.ToJsonString());
            Assert.False(sucess);
        }));
        
        link1.Execute($"5,writeArchive/{Hash.Null}/1/null/testDapp/{token}", ((id, node, sucess) =>
        {
            Assert.Equal(5, id);
            var expexted = APIUtils.FromAPIResult(new WalletLink.Error() { message = $"invokeScript: Invalid archive data" });
            Assert.Equal(expexted.ToJsonString(), node.ToJsonString());
            Assert.False(sucess);
        }));
    }

    [Fact (Skip = "Until sorted out")]
    public void SignWithPhantasma()
    {
        // setup Nexus
        var owner = PhantasmaKeys.Generate();
        
        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        // Setup Users
        var testUser1 = PhantasmaKeys.Generate();
        var account1 = new LinkSimulator.MyAccount(testUser1, LinkSimulator.PlatformKind.Phantasma);
        LinkSimulator link1 = new LinkSimulator(nexus, "Ac1", account1);

        var testUser2 = PhantasmaKeys.Generate();
        var account2 = new LinkSimulator.MyAccount(testUser2, LinkSimulator.PlatformKind.Ethereum);
        LinkSimulator link2 = new LinkSimulator(nexus, "Ac2", account1);

        var platform = "phantasma";

        // Encode Data
        var rawData = Encoding.ASCII.GetBytes("SignWithPhantasma");

        // Make a sign Data Call
        var encryptionScheme = SignatureKind.Ed25519;
        link1.forceSignData(platform, encryptionScheme, rawData, 0, (signed, random, error) =>
        {
            Assert.True(random != null);
            Assert.True(signed != null);
            Console.WriteLine($"Error:{error}");
            var result = testUser1.Address.ValidateSignedData(signed, random, Base16.Encode(rawData));
            Assert.True(result, "Not Valid");
        });

        // Make a sign Data Call
        link2.forceSignData(platform, encryptionScheme, rawData, 1, (signed, random, error) =>
        {
            Assert.True(random != null);
            Assert.True(signed != null);
            Console.WriteLine($"Error:{error}");
            var result = testUser2.Address.ValidateSignedData(signed, random, Base16.Encode(rawData));
            Assert.True(result, "Valid, but shouldn't be.");
        });
    }

    [Fact (Skip = "Until sorted out")]
    public void SignWithEthereum()
    {
        // setup Nexus
        var owner = PhantasmaKeys.Generate();
        
        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        // Setup Users
        var testUser1 = PhantasmaKeys.Generate();
        var account1 = new LinkSimulator.MyAccount(testUser1, LinkSimulator.PlatformKind.Ethereum);
        LinkSimulator link1 = new LinkSimulator(nexus, "Ac1", account1);

        var testUser2 = PhantasmaKeys.Generate();
        var account2 = new LinkSimulator.MyAccount(testUser2, LinkSimulator.PlatformKind.Phantasma);
        LinkSimulator link2 = new LinkSimulator(nexus, "Ac2", account1);

        var platform = "ethereum";

        // Encode Data
        var rawData = Encoding.ASCII.GetBytes("SignWithEthereum");

        var ethKeys1 = new EthereumKey(testUser1.PrivateKey);
        // NOTE this is not the same as an "transcoded address", instead it is a Phantasma address made from the public key of the eth addres
        // A transcoded address would be created from the public ethereum address instead....
        var ethPhaAddress1 = Address.FromKey(ethKeys1); 

        // Make a sign Data Call
        var encryptionScheme = SignatureKind.ECDSA;
        link1.forceSignData(platform, encryptionScheme, rawData, 0, (signed, random, error) =>
        {
            Assert.True(random != null);
            Assert.True(signed != null);
            Console.WriteLine($"Error:{error}");
            var result = ethPhaAddress1.ValidateSignedData( signed, random, Base16.Encode(rawData));
            Assert.True(result, "Not Valid");
        });

        var ethKeys2 = new EthereumKey(testUser2.PrivateKey);
        var ethPhaAddress2 = Address.FromKey(ethKeys2);

        // Make a sign Data Call
        link2.forceSignData(platform, encryptionScheme, rawData, 1, (signed, random, error) =>
        {
            Assert.True(random != null);
            Assert.True(signed != null);
            Console.WriteLine($"Error:{error}");
            var result = ethPhaAddress2.ValidateSignedData( signed, random, Base16.Encode(rawData));
            Assert.True(result, "Valid, but shouldn't be.");
        });
    }
    
    // q: How can i create a test for AuthorizePlatform, that is not just a copy of the code?
    [Fact (Skip = "Until sorted out")]
    public void AuthorizePlatform()
    {
        var owner = PhantasmaKeys.Generate();
        
        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var account1 = new LinkSimulator.MyAccount(owner, LinkSimulator.PlatformKind.Phantasma);
        LinkSimulator link1 = new LinkSimulator(nexus, "Ac1", account1);

        var testUser2 = PhantasmaKeys.Generate();
        var account2 = new LinkSimulator.MyAccount(testUser2, LinkSimulator.PlatformKind.Phantasma);
        LinkSimulator link2 = new LinkSimulator(nexus, "Ac2", account2);

        var platform = "phantasma";

        var encryptionScheme = SignatureKind.Ed25519;
        
        link1.forceSignData(platform, encryptionScheme, Encoding.ASCII.GetBytes("AuthorizePlatform"), 0, (signed, random, error) =>
        {
            Assert.True(random != null);
            Assert.True(signed != null);
            Console.WriteLine($"Error:{error}");
            var result = testUser2.Address.ValidateSignedData(signed, random, Base16.Encode(Encoding.ASCII.GetBytes("AuthorizePlatform")));
            Assert.True(result, "Not Valid");
        });
    }
    
    [Fact]
    public void TestErrorMessageProperty()
    {
        var error = new WalletLink.Error();
        error.message = "This is an error message";
        Assert.Equal("This is an error message", error.message);
    }
    
    [Fact]
    public void TestAuthorizationProperties()
    {
        var authorization = new WalletLink.Authorization
        {
            wallet = "MyWallet",
            nexus = "MyNexus",
            dapp = "MyDapp",
            token = "MyToken",
            version = 1
        };

        Assert.Equal("MyWallet", authorization.wallet);
        Assert.Equal("MyNexus", authorization.nexus);
        Assert.Equal("MyDapp", authorization.dapp);
        Assert.Equal("MyToken", authorization.token);
        Assert.Equal(1, authorization.version);
    }
    
    [Fact]
    public void TestBalanceProperties()
    {
        var balance = new WalletLink.Balance
        {
            symbol = "USD",
            value = "100.00",
            decimals = 2
        };

        Assert.Equal("USD", balance.symbol);
        Assert.Equal("100.00", balance.value);
        Assert.Equal(2, balance.decimals);
    }
    
    [Fact]
    public void TestFileProperties()
    {
        var file = new WalletLink.File
        {
            name = "myfile.txt",
            size = 1024,
            date = 1623432423,
            hash = "abc123"
        };

        Assert.Equal("myfile.txt", file.name);
        Assert.Equal(1024, file.size);
        Assert.Equal((uint)1623432423, file.date);
        Assert.Equal("abc123", file.hash);
    }

    [Fact]
    public void TestAccountProperties()
    {
        var account = new WalletLink.Account
        {
            alias = "myalias",
            address = "myaddress",
            name = "MyName",
            avatar = "myavatar.png",
            platform = "MyPlatform",
            external = "myexternal",
            balances = new[] { new WalletLink.Balance { symbol = "USD", value = "100.00", decimals = 2 } },
            files = new[]
                { new WalletLink.File { name = "myfile.txt", size = 1024, date = 1623432423, hash = "abc123" } }
        };

        Assert.Equal("myalias", account.alias);
        Assert.Equal("myaddress", account.address);
        Assert.Equal("MyName", account.name);
        Assert.Equal("myavatar.png", account.avatar);
        Assert.Equal("MyPlatform", account.platform);
        Assert.Equal("myexternal", account.external);
        Assert.Equal("USD", account.balances[0].symbol);
        Assert.Equal("100.00", account.balances[0].value);
        Assert.Equal(2, account.balances[0].decimals);
        Assert.Equal("myfile.txt", account.files[0].name);
        Assert.Equal(1024, account.files[0].size);
        Assert.Equal((uint)1623432423, account.files[0].date);
        Assert.Equal("abc123", account.files[0].hash);
    }
    
    [Fact]
    public void TestConnection()
    {
        var token = "saiduhasdasd";
        var version = 10;
        var connection = new WalletLink.Connection(token, version);
        
        Assert.Equal(token, connection.Token);
        Assert.Equal(version, connection.Version);
    }
    
    [Fact]
    public void ResultProperty_IsCorrectlySet_WhenConstructed()
    {
        // Arrange
        var expectedResult = "test result";

        // Act
        var invocation = new WalletLink.Invocation() { result = expectedResult };

        // Assert
        Assert.Equal(expectedResult, invocation.result);
    }
    
    [Fact]
    public void HashProperty_IsCorrectlySet_WhenConstructed()
    {
        // Arrange
        var expectedHash = "test hash";

        // Act
        var transaction = new WalletLink.Transaction() { hash = expectedHash };

        // Assert
        Assert.Equal(expectedHash, transaction.hash);
    }
    
    [Fact]
    public void SignatureProperty_IsCorrectlySet_WhenConstructed()
    {
        // Arrange
        var expectedSignature = "test signature";

        // Act
        var signature = new WalletLink.Signature() { signature = expectedSignature };

        // Assert
        Assert.Equal(expectedSignature, signature.signature);
    }

    [Fact]
    public void RandomProperty_IsCorrectlySet_WhenConstructed()
    {
        // Arrange
        var expectedRandom = "test random value";

        // Act
        var signature = new WalletLink.Signature() { random = expectedRandom };

        // Assert
        Assert.Equal(expectedRandom, signature.random);
    }


}
