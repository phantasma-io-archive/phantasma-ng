using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Storage.Context;

namespace Phantasma.Core.Domain.Interfaces;

public interface INativeContract
{
    string Name { get; }
    NativeContractKind Kind { get; }
    ContractInterface ABI { get; }
    IRuntime Runtime { get; }
    Address Address { get; }
    void SetRuntime(IRuntime runtime);
    void LoadFromStorage(StorageContext storage);
    void SaveChangesToStorage();
    bool HasInternalMethod(string methodName);
    object CallInternalMethod(IRuntime runtime, string name, object[] args);
}
