using System;
using System.Threading.Tasks;
using Phantasma.Business.Blockchain;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;

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

            var symbol = nexus.GetPlatformTokenByHash(Hash.FromUnpaddedHex(assetID), "ethereum", nexus.RootStorage);

            if (String.IsNullOrEmpty(symbol))
            {
                return null;
            }

            return symbol;
        }

        public static string FindSymbolFromHash(Nexus nexus, string contractAddress)
        {
            var hash = Hash.FromUnpaddedHex(contractAddress);
            var symbol = nexus.GetPlatformTokenByHashInterop(hash, "ethereum", nexus.RootStorage);

            if (String.IsNullOrEmpty(symbol))
            {
                return null;
            }

            return symbol;
        }
    }
}
