using Phantasma.Infrastructure.Pay;
using Phantasma.Infrastructure.Pay.Chains;

namespace Phantasma.Infrastructure.Tests.Pay;

public class WalletUtilsTests
{
    [Fact]
    public void GetPlatformByID_ReturnsPhantasmaPlatform_ForID0()
    {
        // Act
        var result = WalletUtils.GetPlatformByID(0);

        // Assert
        Assert.Equal(PhantasmaWallet.PhantasmaPlatform, result);
    }

    [Fact]
    public void GetPlatformByID_ReturnsNeoPlatform_ForID1()
    {
        // Act
        var result = WalletUtils.GetPlatformByID(1);

        // Assert
        Assert.Equal(NeoWallet.NeoPlatform, result);
    }

    [Fact]
    public void GetPlatformByID_ReturnsEthereumPlatform_ForID2()
    {
        // Act
        var result = WalletUtils.GetPlatformByID(2);

        // Assert
        Assert.Equal(EthereumWallet.EthereumPlatform, result);
    }

    [Fact]
    public void GetPlatformByID_ThrowsException_ForInvalidID()
    {
        // Act & Assert
        Assert.Throws<NotImplementedException>(() => WalletUtils.GetPlatformByID(3));
    }
}
