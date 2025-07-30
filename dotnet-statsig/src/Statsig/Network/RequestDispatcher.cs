using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Statsig.Lib;

namespace Statsig.Network
{
    public class RequestDispatcher
    {
        private const int BackoffMultiplier = 2;
        private static readonly HashSet<int> RetryCodes = new() { 408, 500, 502, 503, 504, 522, 524, 599 };

        public string Key { get; }
        public string ApiBaseUrl { get; }
        public string ApiBaseUrlForDownloadConfigSpecs { get; }
        public IReadOnlyDictionary<string, string> AdditionalHeaders { get; }

        private readonly JsonSerializer _defaultSerializer;
        private readonly StatsigOptions _options;
        private readonly SDKDetails _sdkDetails;
        private readonly string _sessionID;

        private readonly HttpClient _client;

        public RequestDispatcher(
            string key,
            StatsigOptions options,
            SDKDetails sdkDetails,
            string sessionID
        )
        {
            ApiBaseUrl = string.IsNullOrWhiteSpace(options.ApiUrlBase)
                ? Constants.DEFAULT_API_URL_BASE
                : options.ApiUrlBase;
            if (!string.IsNullOrWhiteSpace(options.ApiUrlForDownloadConfigSpecs))
            {
                ApiBaseUrlForDownloadConfigSpecs = options.ApiUrlForDownloadConfigSpecs;
            }
            else if (!string.IsNullOrWhiteSpace(options.ApiUrlBase))
            {
                ApiBaseUrlForDownloadConfigSpecs = options.ApiUrlBase;
            }
            else
            {
                ApiBaseUrlForDownloadConfigSpecs = Constants.DEFAULT_CDN_URL_BASE;
            }
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
            var handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip
            };
            if (options.Proxy != null)
            {
                handler.UseProxy = true;
                handler.Proxy = options.Proxy;
            }
            _client = new HttpClient(handler);
        }

        public async Task<IReadOnlyDictionary<string, JToken>?> Fetch(
            string endpoint,
            string body,
            int retries = 0,
            int backoff = 1,
            int timeoutInMs = 0,
            IReadOnlyDictionary<string, string>? additionalHeaders = null)
        {
            var (result, status) = await FetchAsString(endpoint, body, 0, retries, backoff, timeoutInMs, additionalHeaders).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<IReadOnlyDictionary<string, JToken>>(result ?? "");
        }

        public async Task<InitializeResult> FetchStatus(
            string endpoint,
            string body,
            int retries = 0,
            int backoff = 1,
            int timeoutInMs = 0,
            IReadOnlyDictionary<string, string>? additionalHeaders = null,
            bool zipped = false)
        {
            var (result, status) = await FetchAsString(endpoint, body, 0, retries, backoff, timeoutInMs, additionalHeaders, zipped).ConfigureAwait(false);
            return status;
        }

        public static byte[] Zip(string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);

            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(mso, CompressionMode.Compress))
                {
                    CopyTo(msi, gs);
                }

                return mso.ToArray();
            }
        }

        public static void CopyTo(Stream src, Stream dest)
        {
            byte[] bytes = new byte[4096];

            int cnt;

            while ((cnt = src.Read(bytes, 0, bytes.Length)) != 0)
            {
                dest.Write(bytes, 0, cnt);
            }
        }

        public async Task<(string?, InitializeResult)> FetchAsString(
            string endpoint,
            string body,
            long sinceTime = 0,
            int retries = 0,
            int backoff = 1,
            int timeoutInMs = 0,
            IReadOnlyDictionary<string, string>? additionalHeaders = null,
            bool zipped = false)
        {
            if (_options is StatsigServerOptions { LocalMode: true })
            {
                return (null, InitializeResult.LocalMode);
            }

            try
            {
                var url = ApiBaseUrl.EndsWith("/") ? ApiBaseUrl + endpoint : ApiBaseUrl + "/" + endpoint;
                if (endpoint.Equals("download_config_specs"))
                {
                    url = (ApiBaseUrlForDownloadConfigSpecs.EndsWith("/") ? ApiBaseUrlForDownloadConfigSpecs + endpoint : ApiBaseUrlForDownloadConfigSpecs + "/" + endpoint) + "/" + Key + ".json?sinceTime=" + sinceTime;
                }

                if (timeoutInMs > 0)
                {
                    _client.Timeout = TimeSpan.FromMilliseconds(timeoutInMs);
                }

                using var request = new HttpRequestMessage(endpoint.Equals("download_config_specs") ? HttpMethod.Get : HttpMethod.Post, url);
                if (zipped)
                {
                    var zippedBody = Zip(body);
                    request.Content = new ByteArrayContent(zippedBody);
                    request.Content.Headers.Add("Content-Encoding", "gzip");
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                }
                else if (!string.IsNullOrWhiteSpace(body))
                {
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                }
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

                if (additionalHeaders != null)
                {
                    foreach (var kv in additionalHeaders)
                    {
                        request.Headers.Add(kv.Key, kv.Value);
                    }
                }

                var response = await _client.SendAsync(request).ConfigureAwait(false);
                if (response == null)
                {
                    return (null, InitializeResult.Success);
                }

                if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
                {
                    var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return (result, InitializeResult.Success);
                }

                if (retries > 0 && RetryCodes.Contains((int)response.StatusCode))
                {
                    return await Retry(endpoint, body, retries, backoff, timeoutInMs, additionalHeaders, zipped).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                if (retries > 0)
                {
                    return await Retry(endpoint, body, retries, backoff, timeoutInMs, additionalHeaders, zipped).ConfigureAwait(false);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Request timed out");
                    return (null, InitializeResult.Timeout);
                }
            }
            catch (HttpRequestException)
            {
                if (retries > 0)
                {
                    return await Retry(endpoint, body, retries, backoff, timeoutInMs, additionalHeaders, zipped).ConfigureAwait(false);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Request failed due to network error");
                    return (null, InitializeResult.NetworkError);
                }
            }
            catch (Exception)
            {
                if (retries > 0)
                {
                    return await Retry(endpoint, body, retries, backoff, timeoutInMs, additionalHeaders, zipped).ConfigureAwait(false);
                }
            }

            return (null, InitializeResult.Failure);
        }

        private async Task<(string?, InitializeResult)> Retry(
            string endpoint,
            string body,
            int retries = 0,
            int backoff = 1,
            int timeoutInMs = 0,
            IReadOnlyDictionary<string, string>? additionalHeaders = null,
            bool zipped = false)
        {
            await Task.Delay(backoff * 1000).ConfigureAwait(false);
            return await FetchAsString(endpoint, body, 0, retries - 1, backoff * BackoffMultiplier, timeoutInMs, additionalHeaders, zipped).ConfigureAwait(false);
        }

        internal async Task<HttpResponseMessage?> DownloadIDList(IDList list)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, list.URL))
            {
                request.Headers.Add("Range", string.Format("bytes={0}-", list.Size));
                return await _client.SendAsync(request).ConfigureAwait(false);

            }
        }
    }
}