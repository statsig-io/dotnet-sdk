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
        private const int BackoffMultiplier = 2;
        private static readonly HashSet<int> RetryCodes = new() { 408, 500, 502, 503, 504, 522, 524, 599 };

        public string Key { get; }
        public string ApiBaseUrl { get; }
        public IReadOnlyDictionary<string, string> AdditionalHeaders { get; }

        private readonly JsonSerializer _defaultSerializer;
        private readonly StatsigOptions _options;


        public RequestDispatcher(
            string key,
            StatsigOptions options
        )
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be empty.", nameof(key));
            }

            ApiBaseUrl = string.IsNullOrWhiteSpace(options.ApiUrlBase)
                ? Constants.DEFAULT_API_URL_BASE
                : options.ApiUrlBase;
            Key = key;
            AdditionalHeaders = options.AdditionalHeaders;

            var jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };
            _defaultSerializer = JsonSerializer.CreateDefault(jsonSettings);
            _options = options;
        }

        public async Task<IReadOnlyDictionary<string, JToken>?> Fetch(
            string endpoint,
            IReadOnlyDictionary<string, object> body,
            IReadOnlyDictionary<string, string> metadata,
            int retries = 0,
            int backoff = 1,
            int timeoutInMs = 0)
        {
            if (_options is StatsigServerOptions { LocalMode: true })
            {
                return null;
            }

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
                            return _defaultSerializer.Deserialize<Dictionary<string, JToken>>(jsonReader);
                        }
                    }
                    else if (retries > 0 && RetryCodes.Contains((int)response.StatusCode))
                    {
                        return await retry(endpoint, body, metadata, retries, backoff);
                    }
                }
            }
            catch (Exception)
            {
                if (retries > 0)
                {
                    return await retry(endpoint, body, metadata, retries, backoff);
                }
            }

            return null;
        }

        private async Task<IReadOnlyDictionary<string, JToken>?> retry(
            string endpoint,
            IReadOnlyDictionary<string, object> body,
            IReadOnlyDictionary<string, string> metadata,
            int retries = 0,
            int backoff = 1)
        {
            await Task.Delay(backoff * 1000);
            return await Fetch(endpoint, body, metadata, retries - 1, backoff * BackoffMultiplier);
        }
    }
}