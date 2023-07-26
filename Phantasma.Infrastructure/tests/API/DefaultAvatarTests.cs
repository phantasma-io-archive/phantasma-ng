using Phantasma.Infrastructure.API;

namespace Phantasma.Infrastructure.Tests.API;

public class DefaultAvatarTests
{
    [Fact]
    public void GetDefaultAvatar_ReturnsDefaultAvatar()
    {
        var defaultAvatar = DefaultAvatar.Data;

        Assert.NotNull(defaultAvatar);
    }
}
