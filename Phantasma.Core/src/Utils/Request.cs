using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Phantasma.Core.Utils
{
    public enum RequestType
    {
        GET,
        POST
    }

    public static class RequestUtils
    {
        public static T Request<T>(RequestType requestType, string url, out string stringResponse, int timeoutInSeconds = 0, int maxAttempts = 1, string postString = "")
        {
            stringResponse = null;

            int max = maxAttempts;
            for (int i = 1; i <= max; i++)
            {
                try
                {
                    using (var httpClient = new HttpClient())
                    {
                        httpClient.Timeout = Timeout.InfiniteTimeSpan;
                        using var cts = new CancellationTokenSource();
                        cts.CancelAfter(TimeSpan.FromSeconds(timeoutInSeconds > 0 ? timeoutInSeconds : 100));

                        switch (requestType)
                        {
                            case RequestType.GET:
                                var response = httpClient.GetAsync(url, cts.Token).Result;
                                using (var content = response.Content)
                                {
                                    stringResponse = content.ReadAsStringAsync(cts.Token).Result;
                                }
                                break;
                            case RequestType.POST:
                                {
                                    var content = new StringContent(postString, System.Text.Encoding.UTF8, "application/json");
                                    var responseContent = httpClient.PostAsync(url, content, cts.Token).Result.Content;
                                    stringResponse = responseContent.ReadAsStringAsync(cts.Token).Result;
                                    break;
                                }
                            default:
                                throw new Exception("Unknown RequestType");
                        }
                    }

                    if (string.IsNullOrEmpty(stringResponse))
                        return default;

                    if (typeof(T) == typeof(JsonDocument))
                    {
                        var node = JsonDocument.Parse(stringResponse);
                        return (T)(object)node;
                    }
                    else if (typeof(T) == typeof(JsonNode))
                    {
                        var node = JsonNode.Parse(stringResponse);
                        return (T)(object)node;
                    }
                    else if (typeof(T) == typeof(string))
                    {
                        return (T)(object)stringResponse;
                    }
                    else
                    {
                        throw new Exception($"Unsupported output type {typeof(T).FullName}");
                    }
                }
                catch
                {
                    if (i < max)
                    {
                        Thread.Sleep(1000 * i);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return default;
        }

        public static async Task<T> RequestAsync<T>(RequestType requestType, string url, int timeoutInSeconds = 0, int maxAttempts = 1, string postString = "")
        {
            string stringResponse = null;

            int max = maxAttempts;
            for (int i = 1; i <= max; i++)
            {
                try
                {
                    using (var httpClient = new HttpClient())
                    {
                        httpClient.Timeout = Timeout.InfiniteTimeSpan;
                        using var cts = new CancellationTokenSource();
                        cts.CancelAfter(TimeSpan.FromSeconds(timeoutInSeconds > 0 ? timeoutInSeconds : 100));

                        switch (requestType)
                        {
                            case RequestType.GET:
                                var response = await httpClient.GetAsync(url, cts.Token);
                                using (var content = response.Content)
                                {
                                    stringResponse = await content.ReadAsStringAsync(cts.Token);
                                }
                                break;
                            case RequestType.POST:
                                {
                                    using var content = new StringContent(postString, System.Text.Encoding.UTF8, "application/json");
                                    using var responseContent = (await httpClient.PostAsync(url, content, cts.Token)).Content;
                                    stringResponse = await responseContent.ReadAsStringAsync(cts.Token);
                                    break;
                                }
                            default:
                                throw new Exception("Unknown RequestType");
                        }
                    }

                    if (string.IsNullOrEmpty(stringResponse))
                        return default;

                    if (typeof(T) == typeof(JsonDocument))
                    {
                        var node = JsonDocument.Parse(stringResponse);
                        return (T)(object)node;
                    }
                    else if (typeof(T) == typeof(JsonNode))
                    {
                        var node = JsonNode.Parse(stringResponse);
                        return (T)(object)node;
                    }
                    else if (typeof(T) == typeof(string))
                    {
                        return (T)(object)stringResponse;
                    }
                    else
                    {
                        throw new Exception($"Unsupported output type {typeof(T).FullName}");
                    }
                }
                catch
                {
                    if (i < max)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(1000 * i));
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return default;
        }

        private class RpcRequest
        {
            public string jsonrpc { get; set; }
            public string method { get; set; }
            public string id { get; set; }
            public object[] @params { get; set; }
        }
        public static JsonDocument RPCRequest(string url, string method, out string stringResponse, int timeoutInSeconds = 0, int maxAttempts = 1, params object[] parameters)
        {
            var rpcRequest = new RpcRequest
            {
                jsonrpc = "2.0",
                method = method,
                id = "1",
                @params = parameters
            };

            var json = JsonSerializer.Serialize(rpcRequest);

            return Request<JsonDocument>(RequestType.POST, url, out stringResponse, timeoutInSeconds, maxAttempts, json);
        }

    }
}
