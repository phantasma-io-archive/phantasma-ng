
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Oracle;
using Phantasma.Core.Domain.Oracle.Enums;

namespace Phantasma.Core.Domain.Interfaces
{
    public interface IFeed
    {
        public string Name { get;  }
        public Address Address { get;  }
        public FeedMode Mode { get;  }
    }
}
