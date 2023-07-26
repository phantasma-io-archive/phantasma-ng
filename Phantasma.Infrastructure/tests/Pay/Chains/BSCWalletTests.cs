using Phantasma.Core.Cryptography;
using Phantasma.Infrastructure.Pay.Chains;

namespace Phantasma.Infrastructure.Tests.Pay.Chains;

public class BSCWalletTests
{
    private readonly PhantasmaKeys _phantasmaKeys;

    public BSCWalletTests()
    {
        _phantasmaKeys =PhantasmaKeys.Generate();
        // Initialize _httpService with a mock instance.
    }

    [Fact]
    public void Constructor_InitializesAddressAndName()
    {
        // Arrange & Act
        var wallet = new BSCWallet(_phantasmaKeys);

        // Assert
        var expectedAddress = BSCWallet.EncodeAddress(wallet.Address);
        
        var decodedAddress = BSCWallet.DecodeAddress(expectedAddress);
        Assert.Equal(decodedAddress, wallet.Address);
        Assert.Equal(decodedAddress, wallet.Name);
    }

    [Fact]
    public void IsValidAddress_ReturnsTrue_WhenAddressIsValid()
    {
        // Arrange
        var validAddress = "0x" + new String('1', 40);

        // Act
        var result = BSCWallet.IsValidAddress(validAddress);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidAddress_ReturnsFalse_WhenAddressIsInvalid()
    {
        // Arrange
        var invalidAddress = "0x" + new String('1', 39);

        // Act
        var result = BSCWallet.IsValidAddress(invalidAddress);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void DecodeAddress_ReturnsCorrectAddress_WhenAddressIsBSCInterop()
    {
        // Arrange
        var expectedAddress = "0x" + new String('1', 40);
        var address = BSCWallet.EncodeAddress(expectedAddress);

        // Act
        var result = BSCWallet.DecodeAddress(address);

        // Assert
        Assert.Equal(expectedAddress, result);
    }

    [Fact]
    public void DecodeAddress_ThrowsException_WhenAddressIsNotBSCInterop()
    {
        // Arrange
        var notBSCAddress = "0x" + new String('2', 40);
        var address = EthereumWallet.EncodeAddress(notBSCAddress);

        // Act & Assert
        Assert.Throws<Exception>(() => BSCWallet.DecodeAddress(address));
    }
    
    // Tests for SyncBalances and MakePayment would be similar to EthereumWallet, 
    // but currently not implementable due to NotImplementedException thrown.
}
