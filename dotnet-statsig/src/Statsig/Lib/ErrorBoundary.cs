using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Statsig.Lib
{
    public class ErrorBoundary
    {
        public string ExceptionEndpoint = "https://statsigapi.net/v1/sdk_exception";

        private string _sdkKey;
        private SDKDetails _sdkDetails;
        private HashSet<string> _seen;

        private HttpClient _client;

        public ErrorBoundary(string sdkKey, SDKDetails sdkDetails, StatsigOptions options)
        {
            _sdkKey = sdkKey;
            _sdkDetails = sdkDetails;
            _seen = new HashSet<string>();
            if (options.Proxy != null)
            {
                var handler = new HttpClientHandler();
                handler.UseProxy = true;
                handler.Proxy = options.Proxy;
                _client = new HttpClient(handler);
            }
            else
            {
                _client = new HttpClient();
            }
            _client.Timeout = TimeSpan.FromSeconds(3);
        }

        public async Task Swallow(string tag, Func<Task> task)
        {
            await Capture(tag, async () =>
            {
                await task().ConfigureAwait(false);
                return true;
            }, () => false)
            .ConfigureAwait(false);
        }

        public void Swallow(string tag, Action task)
        {
            Capture(tag, () =>
            {
                task();
                return true;
            }, () => false);
        }

        public async Task<T> Capture<T>(string tag, Func<Task<T>> task, Func<T> recover)
        {
            try
            {
                var result = await task().ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                return OnCaught(tag, ex, recover);
            }
        }

        public T Capture<T>(string tag, Func<T> task, Func<T> recover)
        {
            try
            {
                var result = task();
                return result;
            }
            catch (Exception ex)
            {
                return OnCaught(tag, ex, recover);
            }
        }

        private T OnCaught<T>(string tag, Exception ex, Func<T> recover)
        {
            if (IsStatsigException(ex))
            {
                throw ex;
            }

            // [Statsig] An unexpected exception occurred.  TODO: Log this

            LogException(tag, ex);

            return recover();
        }

        public async void LogException(string tag, Exception ex, Dictionary<String, object>? extra = null, bool force = false)
        {
            try
            {
                if (string.IsNullOrEmpty(_sdkKey))
                {
                    return;
                }

                var name = ex?.GetType().FullName ?? "No Name";
                if (_seen.Contains(name) && !force)
                {
                    return;
                }

                _seen.Add(name);

                using var request = new HttpRequestMessage(HttpMethod.Post, ExceptionEndpoint);

                var info = ex?.StackTrace ?? ex?.Message ?? "No Info";
                var body = JsonConvert.SerializeObject(new Dictionary<string, object>()
                {
                    { "tag", tag },
                    { "exception", name },
                    { "info", info },
                    { "statsigMetadata", _sdkDetails.StatsigMetadata },
                    { "extra", extra ?? new Dictionary<string, object>()}
                });
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                var headers = new Dictionary<string, string>
                {
                    { "STATSIG-API-KEY", _sdkKey },
                    { "STATSIG-SDK-TYPE", _sdkDetails.SDKType },
                    { "STATSIG-SDK-VERSION", _sdkDetails.SDKVersion },
                };
                foreach (var kv in headers)
                {
                    request.Headers.Add(kv.Key, kv.Value);
                }

                await _client.SendAsync(request).ConfigureAwait(false);
            }
            catch
            {
                /* noop */
            }
        }

        private bool IsStatsigException(Exception ex)
        {
            return ex is StatsigArgumentException or StatsigInvalidOperationException
                or StatsigArgumentNullException or StatsigUninitializedException;
        }
    }
}