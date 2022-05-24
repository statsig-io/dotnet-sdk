using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Statsig.Network
{
    public class RequestDispatcher
    {
        const int backoffMultiplier = 2;
        private JsonSerializer defaultSerializer;
        private static readonly HashSet<int> retryCodes = new HashSet<int> { 408, 500, 502, 503, 504, 522, 524, 599 };
        public string Key { get; }
        public string ApiBaseUrl { get; }
        public IReadOnlyDictionary<string, string> AdditionalHeaders { get; }
        public RequestDispatcher(
            string key, 
            string apiBaseUrl = null, 
            IReadOnlyDictionary<string, string> headers = null
        )
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be empty.", "key");
            }
            if (string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                apiBaseUrl = Constants.DEFAULT_API_URL_BASE;
            }
            if (headers == null)
            {
                headers = new Dictionary<string, string>();
            }

            Key = key;
            ApiBaseUrl = apiBaseUrl;
            AdditionalHeaders = headers;

            var jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };
            defaultSerializer = JsonSerializer.CreateDefault(jsonSettings);
        }

        public async Task<IReadOnlyDictionary<string, JToken>> Fetch(
            string endpoint,
            IReadOnlyDictionary<string, object> body,
            int retries = 0,
            int backoff = 1)
        {
            try
            {
                var url = ApiBaseUrl.EndsWith("/") ? ApiBaseUrl + endpoint : ApiBaseUrl + "/" + endpoint;
                var request = WebRequest.CreateHttp(url);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Headers.Add("STATSIG-API-KEY", Key);
                request.Headers.Add("STATSIG-CLIENT-TIME",
                    (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds.ToString());

                foreach (var kv in AdditionalHeaders)
                {
                    request.Headers.Add(kv.Key, kv.Value);
                }

                using (var writer = new StreamWriter(request.GetRequestStream()))
                {
                    var jsonWriter = new JsonTextWriter(writer);
                    defaultSerializer.Serialize(writer, body);
                }

                var response = (HttpWebResponse)await request.GetResponseAsync();
                if (response == null)
                {
                    return null;
                }
                if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
                {
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        var jsonReader = new JsonTextReader(reader);
                        return defaultSerializer.Deserialize<Dictionary<string, JToken>>(jsonReader);
                    }
                }
                else if (retries > 0 && retryCodes.Contains((int)response.StatusCode))
                {
                    return await retry(endpoint, body, retries, backoff);
                }

            }
            catch (Exception)
            {
                if (retries > 0)
                {
                    return await retry(endpoint, body, retries, backoff);
                }
            }
            return null;
        }

        private async Task<IReadOnlyDictionary<string, JToken>> retry(
            string endpoint,
            IReadOnlyDictionary<string, object> body,
            int retries = 0,
            int backoff = 1)
        {
            await Task.Delay(backoff * 1000);
            return await Fetch(endpoint, body, retries - 1, backoff * backoffMultiplier);
        }
    }
}
