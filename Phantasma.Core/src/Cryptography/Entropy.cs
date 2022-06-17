using Phantasma.Shared.Utils;
using System;

namespace Phantasma.Core
{
    public static class Entropy
    {
        private static System.Security.Cryptography.RandomNumberGenerator rnd = System.Security.Cryptography.RandomNumberGenerator.Create();

        public static byte[] GetRandomBytes(int targetLength)
        {
            var bytes = new byte[targetLength];
            lock (rnd)
            {
                rnd.GetBytes(bytes);
            }

            return bytes;
        }
    }
}
