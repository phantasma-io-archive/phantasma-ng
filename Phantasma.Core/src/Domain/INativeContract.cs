using System.Numerics;
using Phantasma.Core.Context;

namespace Phantasma.Core;

public interface INativeContract
{
    string Name { get; }
    NativeContractKind Kind { get; }
    ContractInterface ABI { get; }
    BigInteger Order { get; } // TODO remove this?
    IRuntime Runtime { get; }
    Address Address { get; }
    void SetRuntime(IRuntime runtime);
    void LoadFromStorage(StorageContext storage);
    void SaveChangesToStorage();
    bool HasInternalMethod(string methodName);
    object CallInternalMethod(IRuntime runtime, string name, object[] args);
}
