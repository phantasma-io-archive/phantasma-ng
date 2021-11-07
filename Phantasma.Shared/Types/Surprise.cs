namespace Phantasma.Shared.Types
{
    public interface IPromise<T>
    {
        T Value { get; }
        Timestamp Timestamp { get; }
        bool Hidden { get; }
    }
}
