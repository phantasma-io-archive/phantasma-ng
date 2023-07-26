using Phantasma.Infrastructure.Pay;
using Phantasma.Infrastructure.Pay.Structs;

namespace Phantasma.Infrastructure.Tests.Pay.Structs;

public class WalletBalanceTests
{
    [Fact]
    public void Constructor_SetsFieldsCorrectly()
    {
        // Arrange
        string symbol = "BTC";
        decimal amount = 1.23m;
        string chain = "main";

        // Act
        var walletBalance = new WalletBalance(symbol, amount, chain);

        // Assert
        Assert.Equal(symbol, walletBalance.Symbol);
        Assert.Equal(amount, walletBalance.Amount);
        Assert.Equal(chain, walletBalance.Chain);
    }

    [Fact]
    public void Constructor_SetsDefaultChain_WhenChainNotSpecified()
    {
        // Arrange
        string symbol = "BTC";
        decimal amount = 1.23m;

        // Act
        var walletBalance = new WalletBalance(symbol, amount);

        // Assert
        Assert.Equal(symbol, walletBalance.Symbol);
        Assert.Equal(amount, walletBalance.Amount);
        Assert.Equal("main", walletBalance.Chain);
    }
}
