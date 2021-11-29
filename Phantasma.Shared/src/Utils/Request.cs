using LunarLabs.Parser;
using LunarLabs.Parser.JSON;
using System.Net.Http;

namespace Phantasma.Shared.Utils
{
    public enum RequestType
    {
        GET,
        POST
    }

    public static class RequestUtils
    {
        public static DataNode Request(RequestType kind, string url, DataNode data = null)
        {
            string contents;

            if (!url.Contains("://"))
            {
                url = "http://" + url;
            }

            try
            {
                switch (kind)
                {
                    case RequestType.GET:
                        {
                            contents = GetWebRequest(url); break;
                        }
                    case RequestType.POST:
                        {
                            var paramData = data != null ? JSONWriter.WriteToString(data) : "{}";
                            contents = PostWebRequest(url, paramData);
                            break;
                        }
                    default: return null;
                }
            }
            catch
            {
                return null;
            }

            if (string.IsNullOrEmpty(contents))
            {
                return null;
            }

            //File.WriteAllText("response.json", contents);

            var root = JSONReader.ReadFromString(contents);
            return root;
        }

        public static string GetWebRequest(string url)
        {
            if (!url.Contains("://"))
            {
                url = "http://" + url;
            }

            using (var httpClient = new HttpClient())
            {
                var response = httpClient.GetAsync(url).Result;
                using (var content = response.Content)
                {
                    return content.ReadAsStringAsync().Result;
                }
            }
        }

        public static string PostWebRequest(string url, string paramData)
        {
            using (var httpClient = new HttpClient())
            {
                var content = new StringContent(paramData, System.Text.Encoding.UTF8, "application/json-rpc");
                var responseContent = httpClient.PostAsync(url, content).Result.Content;
                return responseContent.ReadAsStringAsync().Result;
            }
        }
    }
}
