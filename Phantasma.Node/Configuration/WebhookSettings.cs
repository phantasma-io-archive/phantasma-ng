using Microsoft.Extensions.Configuration;
using Phantasma.Business.Blockchain;
using Serilog;

namespace Phantasma.Node;

public class WebhookSettings
{
    public string Token { get; }
    public string Channel { get; }
    public string Prefix { get; }
        
    public WebhookSettings(IConfigurationSection section)
    {
        this.Token = section.GetString("webhook.token");
        this.Channel = section.GetString("webhook.channel");
        this.Prefix = section.GetString("webhook.prefix");
            
        Webhook.Token = Token; 
        Webhook.Channel = Channel; 
        Webhook.Prefix = Prefix; 
        Log.Logger.Information($"Webhook settings loaded");
        Log.Logger.Information($"Webhook Token {Webhook.Token}");
        Log.Logger.Information($"Webhook Channel {Webhook.Channel}");
        Log.Logger.Information($"Webhook Prefix {Webhook.Prefix}");
    }
}
