using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Phantasma.Business;
using Phantasma.Core;
//using Phantasma.Ethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexConvertors.Extensions;
using Org.BouncyCastle.Crypto.Digests;

namespace Phantasma.Spook.Chains
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

        public static string FindSymbolFromAsset(Business.Nexus nexus, string assetID)
        {
            if (assetID.StartsWith("0x"))
            {
                assetID = assetID.Substring(2);
            }

            var symbol = nexus.GetPlatformTokenByHash(Hash.FromUnpaddedHex(assetID), "ethereum", nexus.RootStorage);

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
