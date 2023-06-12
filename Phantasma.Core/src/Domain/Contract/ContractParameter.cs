using Phantasma.Core.Domain.VM;

namespace Phantasma.Core.Domain.Contract;

public struct ContractParameter
{
    public readonly string name;
    public readonly VMType type;

    public ContractParameter(string name, VMType vmtype)
    {
        this.name = name;
        this.type = vmtype;
    }
}
