using System;
using System.IO;
using System.Text;
using Neo.Wallets;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Core.Numerics;
using Phantasma.Core.Utils;
using Phantasma.Infrastructure.Pay;
using Phantasma.Core.Cryptography;
using Phantasma.Node.Chains.Ethereum;

namespace Phantasma.Core.Tests.Domain;

using Xunit;

using Phantasma.Core.Domain;

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

        public LinkSimulator(Nexus Nexus, string name, MyAccount account)
        {
            this._nexus = Nexus;
            this._name = name;
            this._account = account;
        }

        public override string Nexus => _name;

        public override string Name => _nexus.Name;

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
            throw new NotImplementedException();
        }

        protected override void InvokeScript(string chain, byte[] script, int id, Action<byte[], string> callback)
        {
            throw new NotImplementedException();
        }

        protected void InvokeScript(string chain, byte[] script, int id, Action<string[], string> callback)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        protected override void WriteArchive(Hash hash, int blockIndex, byte[] data, Action<bool, string> callback)
        {
            throw new NotImplementedException();
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
}
