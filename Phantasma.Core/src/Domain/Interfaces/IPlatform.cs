using Phantasma.Core.Domain.Platform;
using Phantasma.Core.Domain.Platform.Structs;

namespace Phantasma.Core.Domain.Interfaces
{
    public interface IPlatform
    {
        string Name { get; }
        string Symbol { get; } // for fuel
        PlatformSwapAddress[] InteropAddresses { get; }
    }
}
