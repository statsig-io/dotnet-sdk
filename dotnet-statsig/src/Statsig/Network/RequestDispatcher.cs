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
        private static readonly HashSet<int> retryCodes = new HashSet<int> { 408, 500, 502, 503, 504, 522, 524, 599 };
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

                var jsonSettings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                };
                using (var writer = new StreamWriter(request.GetRequestStream()))
                {
                    var bodyJson = JsonConvert.SerializeObject(body, Formatting.None, jsonSettings);
                    writer.Write(bodyJson);
                }

                var response = (HttpWebResponse)await request.GetResponseAsync();
                if (response == null)
                {
                    return null;
                }
                if (response.StatusCode == HttpStatusCode.Accepted ||
                response.StatusCode == HttpStatusCode.OK)
                {
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        var json = reader.ReadToEnd();
                        return JsonConvert.DeserializeObject<Dictionary<string, JToken>>(json);
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
            System.Threading.Thread.Sleep(backoff * 1000);
            return await Fetch(endpoint, body, retries - 1, backoff * backoffMultiplier);
        }
    }
}
