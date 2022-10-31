using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
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
            string? apiBaseUrl = null, 
            IReadOnlyDictionary<string, string>? headers = null
        )
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be empty.", "key");
            }
            ApiBaseUrl = string.IsNullOrWhiteSpace(apiBaseUrl) ? 
                Constants.DEFAULT_API_URL_BASE : apiBaseUrl!;
            Key = key;
            AdditionalHeaders = headers ?? new Dictionary<string, string>();

            var jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };
            defaultSerializer = JsonSerializer.CreateDefault(jsonSettings);
        }

        public async Task<IReadOnlyDictionary<string, JToken>?> Fetch(
            string endpoint,
            IReadOnlyDictionary<string, object> body,
            int retries = 0,
            int backoff = 1,
            int timeoutInMs = 0)
        {
            try
            {
                var url = ApiBaseUrl.EndsWith("/") ? ApiBaseUrl + endpoint : ApiBaseUrl + "/" + endpoint;
                var client = new HttpClient();
                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    var bodyJson = JsonConvert.SerializeObject(body);
                    request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
                    request.Headers.Add("STATSIG-API-KEY", Key);
                    request.Headers.Add("STATSIG-CLIENT-TIME",
                        (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds.ToString());
                    
                    var metadata = (endpoint.Equals("initialize")) ? SDKDetails.GetClientSDKDetails().StatsigMetadata: SDKDetails.GetServerSDKDetails().StatsigMetadata;
                    request.Headers.Add("STATSIG-SDK-VERSION", metadata["sdkVersion"]);
                    request.Headers.Add("STATSIG-SDK-TYPE", metadata["sdkType"]);
                    
                    foreach (var kv in AdditionalHeaders)
                    {
                        request.Headers.Add(kv.Key, kv.Value);
                    }
                    if (timeoutInMs > 0)
                    {
                        client.Timeout = TimeSpan.FromMilliseconds(timeoutInMs);
                    }
                
                    var response = await client.SendAsync(request);
                    if (response == null)
                    {
                        return null;
                    }
                    if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
                    {
                        var stream = await response.Content.ReadAsStreamAsync();
                        using (var reader = new StreamReader(stream))
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

        private async Task<IReadOnlyDictionary<string, JToken>?> retry(
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
