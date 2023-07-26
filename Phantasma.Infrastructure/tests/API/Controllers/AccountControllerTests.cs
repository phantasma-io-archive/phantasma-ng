using Moq;
using Phantasma.Core.Types.Structs;
using Phantasma.Infrastructure.API;
using Phantasma.Infrastructure.API.Controllers;

namespace Phantasma.Infrastructure.Tests.API.Controllers;

public class AccountControllerTests
{
    /*private Mock<NexusAPI> _mockNexusAPI;


    public AccountControllerTests()
    {
        _mockNexusAPI = new Mock<NexusAPI>();
    }

    [Fact]
    public void GetAccount_ValidAddress_ReturnsAccountResult()
    {
        // Arrange
        string validAddress = "validAddress";
        AccountResult expectedResult = new AccountResult();

        _mockNexusAPI.Setup(n => n.FillAccount(validAddress)).Returns(expectedResult);
        var controller = new AccountController(_mockNexusAPI.Object, _mockAddress.Object, _mockTimestamp.Object);

        // Act
        var result = controller.GetAccount(validAddress);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void GetAccount_InvalidAddress_ThrowsAPIException()
    {
        // Arrange
        string invalidAddress = "invalidAddress";
        var controller = new AccountController();

        // Act and Assert
        Assert.Throws<APIException>(() => controller.GetAccount(invalidAddress));
    }

    [Fact]
    public void GetAccounts_ValidAddresses_ReturnsAccountResults()
    {
        // Arrange
        string validAddresses = "validAddress1,validAddress2";
        AccountResult[] expectedResult = new AccountResult[]{new AccountResult(), new AccountResult()};

        _mockAddress.Setup(a => a.IsValidAddress(It.IsAny<string>())).Returns(true);
        _mockNexusAPI.Setup(n => n.FillAccount(It.IsAny<string>())).Returns(expectedResult[0]);
        var controller = new AccountController(_mockNexusAPI.Object, _mockAddress.Object, _mockTimestamp.Object);

        // Act
        var results = controller.GetAccounts(validAddresses);

        // Assert
        Assert.Equal(expectedResult, results);
    }

    [Fact]
    public void GetAccounts_InvalidAddress_ThrowsAPIException()
    {
        // Arrange
        string invalidAddresses = "validAddress,invalidAddress";
        _mockAddress.Setup(a => a.IsValidAddress("validAddress")).Returns(true);
        _mockAddress.Setup(a => a.IsValidAddress("invalidAddress")).Returns(false);
        var controller = new AccountController(_mockNexusAPI.Object, _mockAddress.Object, _mockTimestamp.Object);

        // Act and Assert
        Assert.Throws<APIException>(() => controller.GetAccounts(invalidAddresses));
    }*/
}
