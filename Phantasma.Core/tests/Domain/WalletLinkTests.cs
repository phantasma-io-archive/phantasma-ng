using System;
using Phantasma.Core.Cryptography;

namespace Phantasma.Core.Tests.Domain;

using Xunit;

using Phantasma.Core.Domain;

public class WalletLinkTests
{
    // Create a class based on WalletLink to test the protected methods
    public class WalletLinkTestClass : WalletLink
    {
        public WalletLinkTestClass(string name, string address, string publicKey, string privateKey, string chainName, string chainAddress, string chainSymbol, string chainDecimals) : base()
        {
        }

        public string GetAddressFromPublicKey(string publicKey)
        {
            return Address.FromText(publicKey).Text;
        }

        public string GetAddressFromPrivateKey(string privateKey)
        {
            return Address.FromWIF(privateKey).Text;
        }

        protected override WalletStatus Status { get; }
        public override string Nexus { get; }
        public override string Name { get; }
        
        protected override void Authorize(string dapp, string token, int version, Action<bool, string> callback)
        {
            throw new NotImplementedException();
        }

        protected override void GetAccount(string platform, Action<Account, string> callback)
        {
            throw new NotImplementedException();
        }

        protected override void InvokeScript(string chain, byte[] script, int id, Action<byte[], string> callback)
        {
            throw new NotImplementedException();
        }

        protected override void SignData(string platform, SignatureKind kind, byte[] data, int id, Action<string, string, string> callback)
        {
            throw new NotImplementedException();
        }

        protected override void SignTransaction(string platform, SignatureKind kind, string chain, byte[] script, byte[] payload, int id,
            Action<Hash, string> callback)
        {
            throw new NotImplementedException();
        }

        protected override void WriteArchive(Hash hash, int blockIndex, byte[] data, Action<bool, string> callback)
        {
            throw new NotImplementedException();
        }
    }
    
    [Fact]
    public void TestWalletLink()
    {
        
    }
}
