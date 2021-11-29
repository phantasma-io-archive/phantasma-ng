using Phantasma.Shared;
using Serilog.Core;
using System;
using System.Net.Http;
using System.Threading;

namespace LunarLabs.Parser.JSON
{
    public class JSONRPC_Client
    {
        private HttpClient client;

        public JSONRPC_Client()
        {
            client = new HttpClient(); 
        }

        public DataNode SendRequest(Logger logger, string url, string method, params object[] parameters)
        {
            Throw.IfNull(logger, nameof(logger));

            DataNode paramData = DataNode.CreateArray("params");

            if (parameters!=null && parameters.Length > 0)
            {
                foreach (var obj in parameters)
                {
                    paramData.AddField(null, obj);
                }
            }

            var jsonRpcData = DataNode.CreateObject(null);
            jsonRpcData.AddField("jsonrpc", "2.0");
            jsonRpcData.AddField("method", method);
            jsonRpcData.AddField("id", "1");

            jsonRpcData.AddNode(paramData);

            int retries = 5;
            string response = null;

            int retryDelay = 500;

            while (retries > 0)
            {
                try
                {
                    var json = JSONWriter.WriteToString(jsonRpcData);
                    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json-rpc");
                    var responseContent = client.PostAsync(url, content).Result.Content;
                    response = responseContent.ReadAsStringAsync().Result;
                }
                catch (Exception e)
                {
                    retries--;
                    if (retries <= 0)
                    {
                        logger.Error(e.ToString());
                        return null;
                    }
                    else
                    {
                        logger.Warning($"Retrying connection to {url} after {retryDelay}ms...");
                        Thread.Sleep(retryDelay);
                        retryDelay *= 2;
                        continue;
                    }
                }

                break;
            }

            if (string.IsNullOrEmpty(response))
            {
                return null;
            }

            //File.WriteAllText("response.json", contents);

            var root = JSONReader.ReadFromString(response);

            if (root == null)
            {
                return null;
            }

            if (root.HasNode("result"))
            {
                return root["result"];
            }

            return root;
        }
   }
}
