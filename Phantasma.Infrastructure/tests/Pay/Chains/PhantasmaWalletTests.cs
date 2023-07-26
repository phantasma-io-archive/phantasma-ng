using Phantasma.Core.Cryptography;
using Phantasma.Infrastructure.Pay;
using Phantasma.Infrastructure.Pay.Chains;
using Phantasma.Infrastructure.Pay.Enums;

namespace Phantasma.Infrastructure.Tests.Pay.Chains;

public class PhantasmaWalletTests
{
    private readonly PhantasmaKeys _phantasmaKeys;

    public PhantasmaWalletTests()
    {
        // Initialize _phantasmaKeys with the proper values.
    }

    [Fact(Skip = "Not implemented yet")]
    public void Constructor_InitializesAddressAndName()
    {
        // Arrange
        string rpcUrl = "http://example.com/";

        // Act
        var wallet = new PhantasmaWallet(_phantasmaKeys, rpcUrl);

        // Assert
        Assert.Equal(_phantasmaKeys.Address.Text, wallet.Address);
        Assert.Equal(_phantasmaKeys.Address.Text, wallet.Name);
    }

    [Fact(Skip = "Not implemented yet")]
    public void GetCryptoCurrencyInfos_ReturnsExpectedInfos()
    {
        // Arrange
        string rpcUrl = "http://example.com/";
        var wallet = new PhantasmaWallet(_phantasmaKeys, rpcUrl);

        // Act
        var infos = wallet.GetCryptoCurrencyInfos().ToArray();

        // Assert
        Assert.Equal(2, infos.Length);

        Assert.Equal("SOUL", infos[0].Symbol);
        Assert.Equal("Phantasma Stake", infos[0].Name);
        Assert.Equal(8, infos[0].Decimals);
        Assert.Equal(PhantasmaWallet.PhantasmaPlatform, infos[0].Platform);
        Assert.Equal(CryptoCurrencyCaps.Balance | CryptoCurrencyCaps.Transfer | CryptoCurrencyCaps.Stake, infos[0].Caps);

        Assert.Equal("KCAL", infos[1].Symbol);
        Assert.Equal("Phantasma Energy", infos[1].Name);
        Assert.Equal(10, infos[1].Decimals);
        Assert.Equal(PhantasmaWallet.PhantasmaPlatform, infos[1].Platform);
        Assert.Equal(CryptoCurrencyCaps.Balance | CryptoCurrencyCaps.Transfer, infos[1].Caps);
    }
}
