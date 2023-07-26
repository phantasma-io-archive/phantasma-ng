using Phantasma.Core.Cryptography;
using Phantasma.Infrastructure.Pay;
using Phantasma.Infrastructure.Pay.Chains;
using Phantasma.Infrastructure.Pay.Enums;

namespace Phantasma.Infrastructure.Tests.Pay.Chains;

public class EthereumWalletTests
{
    private readonly PhantasmaKeys _phantasmaKeys;
    //private readonly IHttpService _httpService;


    public EthereumWalletTests()
    {
        _phantasmaKeys = PhantasmaKeys.Generate();
        // Initialize _phantasmaKeys with the proper values.
    }

    [Fact]
    public void Constructor_InitializesAddressAndName()
    {
        // Arrange & Act
        var wallet = new EthereumWallet(_phantasmaKeys);

        // Assert
        var expectedAddress = EthereumWallet.EncodeAddress(wallet.Address);

        var decodedAddress = EthereumWallet.DecodeAddress(expectedAddress);
        Assert.Equal(decodedAddress, wallet.Address);
        Assert.Equal(wallet.Name, decodedAddress);
    }

    [Fact]
    public void GetCryptoCurrencyInfos_ReturnsExpectedInfos()
    {
        // Arrange
        var wallet = new EthereumWallet(_phantasmaKeys);

        // Act
        var infos = wallet.GetCryptoCurrencyInfos().ToArray();

        // Assert
        Assert.Equal(2, infos.Length);

        Assert.Equal("ETH", infos[0].Symbol);
        Assert.Equal("Ether", infos[0].Name);
        Assert.Equal(8, infos[0].Decimals);
        Assert.Equal(EthereumWallet.EthereumPlatform, infos[0].Platform);
        Assert.Equal(CryptoCurrencyCaps.Balance, infos[0].Caps);

        Assert.Equal("DAI", infos[1].Symbol);
        Assert.Equal("Dai", infos[1].Name);
        Assert.Equal(8, infos[1].Decimals);
        Assert.Equal(EthereumWallet.EthereumPlatform, infos[1].Platform);
        Assert.Equal(CryptoCurrencyCaps.Balance, infos[1].Caps);
    }

    [Fact]
    public void IsValidAddress_ReturnsCorrectResults()
    {
        // Arrange
        string validAddress = "0x1234567890123456789012345678901234567890";
        string invalidAddress1 = "1234567890123456789012345678901234567890"; // Missing "0x" prefix
        string invalidAddress2 = "0x123456789012345678901234567890123456789"; // Too short

        // Act & Assert
        Assert.True(EthereumWallet.IsValidAddress(validAddress));
        Assert.False(EthereumWallet.IsValidAddress(invalidAddress1));
        Assert.False(EthereumWallet.IsValidAddress(invalidAddress2));
    }

}
