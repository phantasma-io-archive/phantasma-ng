
namespace Phantasma.Core
{
    public enum FeedMode
    {
        First,
        Last,
        Max,
        Min,
        Average
    }

    public interface IFeed
    {
        public string Name { get;  }
        public Address Address { get;  }
        public FeedMode Mode { get;  }
    }
}
