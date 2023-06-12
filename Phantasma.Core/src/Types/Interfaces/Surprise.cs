using Phantasma.Core.Types.Structs;

namespace Phantasma.Core.Types.Interfaces
{
    public interface IPromise<T>
    {
        T Value { get; }
        Timestamp Timestamp { get; }
        bool Hidden { get; }
    }
}
