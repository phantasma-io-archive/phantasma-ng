using Phantasma.Core.Domain.Contract;

namespace Phantasma.Core.Domain.Interfaces
{
    public interface IContract
    {
        public string Name { get; }
        public ContractInterface ABI { get; }
    }
}
