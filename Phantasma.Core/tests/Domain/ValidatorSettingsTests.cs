using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract.Validator;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class ValidatorSettingsTests
{
    [Fact]
    public void TestValidatorSettingsConstructor()
    {
        // Arrange
        var address = PhantasmaKeys.Generate().Address;
        string name = "Test Name";
        string host = "Test Host";
        uint port = 8080;

        // Act
        var validatorSettings = new ValidatorSettings(address, name, host, port);

        // Assert
        Assert.Equal(address, validatorSettings.Address);
        Assert.Equal(name, validatorSettings.Name);
        Assert.Equal(host, validatorSettings.Host);
        Assert.Equal(port, validatorSettings.Port);
        Assert.Equal($"http://{host}:{port}", validatorSettings.URL);
    }
}
