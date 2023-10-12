using Phantasma.Infrastructure.Pay;
using Phantasma.Infrastructure.Pay.Enums;
using Phantasma.Infrastructure.Pay.Structs;

namespace Phantasma.Infrastructure.Tests.Pay.Structs;

public class CryptoCurrencyInfoTests
{
    [Fact]
    public void Constructor_SetsFieldsCorrectly()
    {
        // Arrange
        string symbol = "BTC";
        string name = "Bitcoin";
        int decimals = 8;
        string platform = "Bitcoin";
        CryptoCurrencyCaps caps = CryptoCurrencyCaps.Balance | CryptoCurrencyCaps.Transfer;

        // Act
        var info = new CryptoCurrencyInfo(symbol, name, decimals, platform, caps);

        // Assert
        Assert.Equal(symbol, info.Symbol);
        Assert.Equal(name, info.Name);
        Assert.Equal(decimals, info.Decimals);
        Assert.Equal(platform, info.Platform);
        Assert.Equal(caps, info.Caps);
    }
}
