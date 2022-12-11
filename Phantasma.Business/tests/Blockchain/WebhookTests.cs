using Phantasma.Business.Blockchain;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain;

public class WebhookTests
{
    [Fact]
    public void TestWebhook()
    {
        var url = "Something to send to...";
        Webhook.Channel = "X";
        Webhook.Prefix = "Testnet";
        Webhook.Token = "Token";
        Webhook.Notify(url);
        Assert.True(true);
    }
}
