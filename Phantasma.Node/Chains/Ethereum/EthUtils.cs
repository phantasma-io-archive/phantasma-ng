using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Org.BouncyCastle.Crypto.Digests;
using Phantasma.Business.Blockchain;
using Phantasma.Core.Cryptography;

//using Phantasma.Ethereum.Hex.HexConvertors.Extensions;

namespace Phantasma.Node.Chains.Ethereum
{
    public static class EthUtils
    {
        public static void RunSync(Func<Task> method)
        {
            var task = method();
            task.GetAwaiter().GetResult();
        }

        public static TResult RunSync<TResult>(Func<Task<TResult>> method)
        {
            var task = method();
            return task.GetAwaiter().GetResult();
        }

        public static string FindSymbolFromAsset(Nexus nexus, string assetID)
        {
            if (assetID.StartsWith("0x"))
            {
                assetID = assetID.Substring(2);
            }

            var symbol = nexus.GetPlatformTokenByHash(Hash.FromUnpaddedHex(assetID), "ethereum", nexus.RootChain.StorageCollection.PlatformsStorage);

            if (String.IsNullOrEmpty(symbol))
            {
                return null;
            }

            return symbol;
        }
    }

    public class Sha3Keccak
    {
        public static Sha3Keccak Current { get; } = new Sha3Keccak();

        public string CalculateHash(string value)
        {
            var input = Encoding.UTF8.GetBytes(value);
            var output = CalculateHash(input);
            return output.ToHex();
        }

        public string CalculateHashFromHex(params string[] hexValues)
        {
            var joinedHex = string.Join("", hexValues.Select(x => x.RemoveHexPrefix()).ToArray());
            return CalculateHash(joinedHex.HexToByteArray()).ToHex();
        }

        public byte[] CalculateHash(byte[] value)
        {
            var digest = new KeccakDigest(256);
            var output = new byte[digest.GetDigestSize()];
            digest.BlockUpdate(value, 0, value.Length);
            digest.DoFinal(output, 0);
            return output;
        }
    }
}
