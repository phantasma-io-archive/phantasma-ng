using System.Collections.Generic;
using Phantasma.Core;

namespace Phantasma.Business
{
    public abstract class ExecutionContext : IExecutionContext
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

        public abstract ExecutionState Execute(IExecutionFrame frame, Stack<VMObject> stack);

        public override string ToString()
        {
            return Name;
        }
    }
}
