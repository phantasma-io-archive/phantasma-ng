using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic.CompilerServices;
using Phantasma.Core.Domain;
using Phantasma.Core.Utils;
using Serilog;

namespace Phantasma.Business.Blockchain;

public static class Webhook
{
    private const string WebhookUrl = "https://discordapp.com/api/webhooks";
    public static string Prefix { get; set; } // For example testnet / mainnet
    public static string Token { get; set; }
    public static string Channel { get; set; }
    
    public static void Notify(string message)
    {
        try
        {
            Log.Logger.Error("Sending webhook notification: {message}", message);
            if (string.IsNullOrEmpty(Token) || string.IsNullOrEmpty(Channel))
            {
                return;
            }
            var client = new HttpClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
            var url = $"{WebhookUrl}/{Channel}/{Token}";
            var myMessaContent = "@everyone Chain Warning ( " + Prefix + " ) -- {" + message + "}";
            
            // Calculate the number of chunks we need
            int chunkCount = (int)Math.Ceiling((double)myMessaContent.Length / 2000);

            string[] messages = new string[chunkCount];
            
            // Split the message into chunks
            for (int i = 0; i < chunkCount; i++)
            {
                messages[i] = myMessaContent.Substring(i * 2000, Math.Min(2000, myMessaContent.Length - i * 2000));
                
                var msgContent = "{\"content\": \""+messages[i]+" \"," +
                                 "\"username\": \"Chain Notify\"," +
                                 "\"allowed_mentions\": {" +
                                 "\"parse\": [\"everyone\"]," +
                                 "\"users\": []" +
                                 "}" +
                                 "}";
                var content = new StringContent(msgContent, Encoding.UTF8, "application/json");
                var response = client.PostAsync(url, content).GetAwaiter().GetResult();
            }
            return;
        }
        catch (Exception e)
        {
            Log.Logger.Error(e, "Error sending webhook notification: {message}", message);
        }
    }
}
