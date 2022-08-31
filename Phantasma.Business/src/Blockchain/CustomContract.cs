using Phantasma.Core.Domain;
using Phantasma.Shared;

namespace Phantasma.Business.Blockchain
{
    public sealed class CustomContract : SmartContract, ICustomContract
    {
        private string _name;
        public override string Name => _name;

        public byte[] Script { get; private set; }

        public CustomContract(string name, byte[] script, ContractInterface abi) : base()
        {
            Throw.IfNull(script, nameof(script));
            this.Script = script;

            _name = name;

            this.ABI = abi;
        }
    }
}
