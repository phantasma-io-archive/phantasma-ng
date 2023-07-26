using Phantasma.Infrastructure.API;

namespace Phantasma.Infrastructure.Tests.API;

public class APIExceptionTests
{
    [Fact]
    public void APIException_StoresMessage()
    {
        var message = "Test Message";
        var apiException = new APIException(message);

        Assert.Equal(message, apiException.Message);
    }

    [Fact]
    public void APIException_StoresInnerException()
    {
        var message = "Test Message";
        var innerException = new Exception("Inner Exception");
        var apiException = new APIException(message, innerException);

        Assert.Equal(message, apiException.Message);
        Assert.Equal(innerException, apiException.InnerException);
    }
}
