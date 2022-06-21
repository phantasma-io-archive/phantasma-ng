using System.Collections.Generic;
using System.Threading.Tasks;

namespace Phantasma.Core
{
    public abstract class ExecutionContext
    {
        public abstract string Name { get; }

        private Address _address;
        public Address Address {
            get
            {
                if (_address.IsNull)
                {
                    _address = Address.FromHash(Name);
                }

                return _address;
            }
        }

        public abstract Task<ExecutionState> Execute(ExecutionFrame frame, Stack<VMObject> stack);

        public override string ToString()
        {
            return Name;
        }
    }
}
