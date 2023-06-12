using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Enums;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Oracle.Enums;

namespace Phantasma.Business.Blockchain.VM;

public partial class RuntimeVM : GasMachine, IRuntime
{
    public bool FeedExists(string name)
    {
        ExpectNameLength(name, nameof(name));
        return Nexus.FeedExists(RootStorage, name);
    }

    public void CreateFeed(Address owner, string name, FeedMode mode)
    {
        ExpectAddressSize(owner, nameof(owner));
        ExpectNameLength(name, nameof(name));
        ExpectEnumIsDefined(mode, nameof(mode));

        Expect(IsRootChain(), "must be root chain");

        var pow = Transaction.Hash.GetDifficulty();
        Expect(pow >= (int)ProofOfWork.Minimal, "expected proof of work");

        Expect(!string.IsNullOrEmpty(name), "name required");

        Expect(owner.IsUser, "owner address must be user address");
        Expect(IsStakeMaster(owner), "needs to be master");
        Expect(IsWitness(owner), "invalid witness");

        Expect(Nexus.CreateFeed(RootStorage, owner, name, mode), "feed creation failed");

        DomainExtensions.Notify(this, EventKind.FeedCreate, owner, name);
    }

    public IFeed GetFeed(string name)
    {
        ExpectNameLength(name, nameof(name));
        return Nexus.GetFeedInfo(RootStorage, name);
    }

    public string[] GetFeeds()
    {
        return Nexus.GetFeeds(RootStorage);
    }
}
