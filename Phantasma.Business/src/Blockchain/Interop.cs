using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;

namespace Phantasma.Business.Blockchain
{
    public static class InteropUtils
    {
        public static PhantasmaKeys GenerateInteropKeys(PhantasmaKeys genesisKeys, Hash genesisHash, string platformName)
        {
            var temp = $"{genesisKeys.ToWIF()}{genesisHash.ToString()}{platformName}";
            temp = temp.ToUpper();

            var privateKey = CryptoExtensions.Sha256(temp);
            var key = new PhantasmaKeys(privateKey);
            return key;
        }
    }
}
