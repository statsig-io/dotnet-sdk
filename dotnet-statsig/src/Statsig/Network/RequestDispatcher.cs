using System;
using System.Collections.Generic;
using System.Net;
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
        private readonly SDKDetails _sdkDetails;
        private readonly string _sessionID;

        public RequestDispatcher(
            string key,
            StatsigOptions options,
            SDKDetails sdkDetails,
            string sessionID
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
            _sdkDetails = sdkDetails;
            _sessionID = sessionID;
        }

        public async Task<IReadOnlyDictionary<string, JToken>?> Fetch(
            string endpoint,
            IReadOnlyDictionary<string, object> body,
            int retries = 0,
            int backoff = 1,
            int timeoutInMs = 0)
        {
            var result = await FetchAsString(endpoint, body, retries, backoff, timeoutInMs);
            return JsonConvert.DeserializeObject<IReadOnlyDictionary<string, JToken>>(result ?? "");
        }

        public async Task<string?> FetchAsString(
            string endpoint,
            IReadOnlyDictionary<string, object> body,
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
                var client = new HttpClient(new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.GZip
                });
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                var bodyJson = JsonConvert.SerializeObject(body);
                request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
                request.Headers.Add("STATSIG-API-KEY", Key);
                request.Headers.Add("STATSIG-CLIENT-TIME",
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
                request.Headers.Add("STATSIG-SDK-VERSION", _sdkDetails.SDKVersion);
                request.Headers.Add("STATSIG-SDK-TYPE", _sdkDetails.SDKType);
                if (_sdkDetails.SDKType == "dotnet-server")
                {
                    request.Headers.Add("STATSIG-SERVER-SESSION-ID", _sessionID);
                }
                request.Headers.Add("Accept-Encoding", "gzip");

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
                    var result = await response.Content.ReadAsStringAsync();
                    return result;
                }

                if (retries > 0 && RetryCodes.Contains((int)response.StatusCode))
                {
                    return await Retry(endpoint, body, retries, backoff);
                }
            }
            catch (Exception)
            {
                if (retries > 0)
                {
                    return await Retry(endpoint, body, retries, backoff);
                }
            }

            return null;
        }

        private async Task<string?> Retry(
            string endpoint,
            IReadOnlyDictionary<string, object> body,
            int retries = 0,
            int backoff = 1)
        {
            await Task.Delay(backoff * 1000);
            return await FetchAsString(endpoint, body, retries - 1, backoff * BackoffMultiplier);
        }
    }
}