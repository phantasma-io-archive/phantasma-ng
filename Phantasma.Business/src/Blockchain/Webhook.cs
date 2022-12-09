using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Phantasma.Core.Domain;
using Phantasma.Core.Utils;

namespace Phantasma.Business.Blockchain;

public static class Webhook
{
    private const string WebhookUrl = "https://discordapp.com/api/webhooks/";
    public static string Prefix { get; set; } // For example testnet / mainnet
    public static string Token { get; set; }
    public static string Channel { get; set; }
    
    public static Task Notify(string message)
    {
        if (string.IsNullOrEmpty(Token) || string.IsNullOrEmpty(Channel))
        {
            return Task.CompletedTask;
        }
        
        var client = new HttpClient();
        var msgContent = "{\"content\": \"@everyone Chain Warning ( "+Prefix+" ) \n{"+message+"}\"," +
                             "\"username\": \"Chain Notify\"," +
                             "\"allowed_mentions\": {" +
                                 "\"parse\": [\"everyone\"]" +
                                 "\"users\": []" +
                             "}" +
                         "}";
                         
        var content = new StringContent(msgContent, Encoding.UTF8, "application/json");
        //var response = await client.PostAsync(WebhookUrl, content);
        return client.PostAsync($"{WebhookUrl}/{Channel}/{Token}", content);
    }
}
