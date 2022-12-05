using Xunit;
using Phantasma.Node.Chains.Neo2;

// Testing methods:
// bool IsValidAddress(this string address)

namespace Phantasma.Core.Tests.Neo.Utils;

public class PhantasmaNeoUtilsTests
{
    [Fact]
    public void IsValidAddressTest()
    {
        // Checking valid address
        Assert.True("AP6ZkjweW4NGskMca2KH2cchNJbFWW2vZe".IsValidAddress());

        // Checking invalid address
        Assert.False("AP6ZkjweW4NGskMca2KH2cchNJbFWW2vZee".IsValidAddress());

        // Checking invalid address
        Assert.False("AP6ZkjweW4NGskMca2KH2cchNJbFWW2vZ".IsValidAddress());

        // Checking invalid address
        Assert.False("AP6ZkjweW4NGskMca2KH2cchNJbFWW2vZE".IsValidAddress());

        // Checking invalid address
        Assert.False("AP6ZkjweW4NGskMca2KH2cchNJbFWW2lOI".IsValidAddress());
    }
}
