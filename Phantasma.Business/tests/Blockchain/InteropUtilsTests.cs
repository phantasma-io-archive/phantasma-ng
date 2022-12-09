using Phantasma.Core.Cryptography;

namespace Phantasma.Business.Tests.Blockchain;

using Xunit;

using Phantasma.Business.Blockchain;

public class InteropUtilsTests
{
    [Fact]
    public void TestInteropUtils()
    {
        var keys = PhantasmaKeys.Generate();
        var hash = Hash.FromString("000000120012000123");
        var platform = "test";
        var result = InteropUtils.GenerateInteropKeys(keys, hash, platform);
        Assert.True(result.GetType() == typeof(PhantasmaKeys));
        Assert.NotNull(result);
        Assert.True(Address.IsValidAddress(result.Address.Text));
    }
}
