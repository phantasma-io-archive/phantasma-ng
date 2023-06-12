using System.Collections.Generic;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain.Contract.Interop;

namespace Phantasma.Core.Domain.Interfaces
{
    public interface ITokenSwapper
    {
        Hash SettleSwap(string sourcePlatform, string destPlatform, Hash sourceHash);
        IEnumerable<ChainSwap> GetPendingSwaps(Address address);

        bool SupportsSwap(string sourcePlatform, string destPlatform);
    }
}
