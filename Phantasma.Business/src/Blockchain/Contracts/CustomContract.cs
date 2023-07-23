using Phantasma.Core;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Interfaces;

namespace Phantasma.Business.Blockchain.Contracts
{
    public sealed class CustomContract : SmartContract, ICustomContract
    {
        private string _name;
        public override string Name => _name;

        public byte[] Script { get; private set; }

        public CustomContract(string name, byte[] script, ContractInterface abi) : base()
        {
            Throw.IfNull(script, nameof(script));
            Script = script;

            _name = name;

            ABI = abi;
        }
    }
}
