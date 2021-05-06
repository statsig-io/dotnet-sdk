using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Statsig.Network
{
    public class RequestDispatcher
    {
        public string Key { get; }
        public string ApiBaseUrl { get; }
        public RequestDispatcher(string key, string apiBaseUrl = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be empty.", "key");
            }
            if (string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                apiBaseUrl = Constants.DEFAULT_API_URL_BASE;
            }

            Key = key;
            ApiBaseUrl = apiBaseUrl;
        }

        public async Task<IReadOnlyDictionary<string, JToken>> Fetch(
            string endpoint,
            IReadOnlyDictionary<string, object> body)
        {
            try
            {
                var url = ApiBaseUrl.EndsWith("/") ? ApiBaseUrl + endpoint : ApiBaseUrl + "/" + endpoint;
                var request = WebRequest.CreateHttp(url);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Headers.Add("STATSIG-API-KEY", Key);

                var jsonSettings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                };
                using (var writer = new StreamWriter(request.GetRequestStream()))
                {
                    var bodyJson = JsonConvert.SerializeObject(body, Formatting.None, jsonSettings);
                    writer.Write(bodyJson);
                }

                var json = await FetchInternal(request);
                return JsonConvert.DeserializeObject<Dictionary<string, JToken>>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        async Task<string> FetchInternal(HttpWebRequest request)
        {
            var response = (HttpWebResponse)await request.GetResponseAsync();
            if (response.StatusCode == HttpStatusCode.Accepted ||
                response.StatusCode == HttpStatusCode.OK)
            {
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    return reader.ReadToEnd();
                }
            }

            return null;
        }
    }
}
