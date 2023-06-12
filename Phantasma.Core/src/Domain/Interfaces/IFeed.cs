
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain.Oracle;

namespace Phantasma.Core.Domain.Interfaces
{
    public interface IFeed
    {
        public string Name { get;  }
        public Address Address { get;  }
        public FeedMode Mode { get;  }
    }
}
