using System.Collections.Generic;
using Xunit;
using WireMock.Server;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using System.Threading.Tasks;
using WireMock;
using WireMock.Settings;
using Statsig;
using Statsig.Server;
using Newtonsoft.Json.Linq;
using WireMock.ResponseProviders;

namespace dotnet_statsig_tests
{
    [Collection("Statsig Singleton Tests")]
    public class DedupeTest : IAsyncLifetime, IResponseProvider
    {
        WireMockServer _server;
        string _baseURL;
        int _flushedEventCount = 0;
        int getIDListCount = 0;
        int list1Count = 0;
        ServerDriver _serverDriver;

        Task IAsyncLifetime.InitializeAsync()
        {
            _server = WireMockServer.Start();
            _baseURL = _server.Urls[0];
            _server.ResetLogEntries();
            _server.Given(
                Request.Create().WithPath("/v1/download_config_specs").UsingPost()
            ).RespondWith(this);
            _server.Given(
                Request.Create().WithPath("/v1/log_event").UsingPost()
            ).RespondWith(this);
            _server.Given(
                Request.Create().WithPath("/v1/get_id_lists").UsingPost()
            ).RespondWith(this);
            _server.Given(
                Request.Create().WithPath("/list_1").UsingAnyMethod()
            ).RespondWith(this);

            return Task.CompletedTask;
        }

        Task IAsyncLifetime.DisposeAsync()
        {
            _server.Stop();
            return Task.CompletedTask;
        }

        // IResponseProvider
        public async Task<(ResponseMessage Message, IMapping Mapping)> ProvideResponseAsync(RequestMessage requestMessage, IWireMockServerSettings settings)
        {
            if (requestMessage.AbsolutePath.Contains("/v1/download_config_specs"))
            {
                return await Response.Create()
                    .WithStatusCode(200)
                    .WithBody(SpecStoreResponseData.downloadConfigSpecResponse)
                    .ProvideResponseAsync(requestMessage, settings);
            }

            if (requestMessage.AbsolutePath.Contains("/v1/get_id_lists"))
            {
                getIDListCount++;
                var url = _baseURL + "/list_1";
                var body = $@"{{
                    'list_1': {{
                        'name': 'list_1',
                        'size': {3 * getIDListCount},
                        'url': '{url}',
                        'creationTime': 1,
                        'fileID': 'file_id_1',
                    }},
                }}";

                return await Response.Create()
                    .WithStatusCode(200)
                    .WithBody(body)
                    .ProvideResponseAsync(requestMessage, settings);
            }

            if (requestMessage.AbsolutePath.Contains("/v1/log_event"))
            {
                var body = (requestMessage.BodyAsJson as JObject);
                _flushedEventCount += ((JArray)body["events"]).ToObject<List<JObject>>().Count;
                return await Response.Create()
                    .WithStatusCode(200)
                    .ProvideResponseAsync(requestMessage, settings);
            }

            if (requestMessage.AbsolutePath.Contains("/list_1"))
            {
                var body = "+7/rrkvF6\n";
                list1Count++;
                if (list1Count > 1)
                {
                    body = string.Format("+{0}\n-{0}\n", list1Count);
                }
                return await Response.Create()
                    .WithStatusCode(200)
                    .WithBody(body)
                    .ProvideResponseAsync(requestMessage, settings);
            }

            return await Response.Create()
                .WithStatusCode(404)
                .ProvideResponseAsync(requestMessage, settings);
        }

        [Fact]
        public async void TestDedupeOnce()
        {
            await Start();
            await RunChecksAndAssert(10, 20);
            
            await _serverDriver.Shutdown();
            // make sure we ultimately flushed exactly 80 events (20 loops/thread x 4 events/loop)
            Assert.Equal(80, _flushedEventCount);
        }

        [Fact]
        public async void TestDedupeConcurrently()
        {
            await Start();
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(RunChecksWithoutAssert(10, 20));
            }
            await Task.WhenAll(tasks);

            await _serverDriver.Shutdown();
            // make sure we ultimately flushed exactly 80 events (10 threads x 20 loops/thread x 4 events/loop, deduped)
            Assert.Equal(800, _flushedEventCount);
        }

        private async Task RunChecksAndAssert(int delay, int times)
        {
            for (int i = 0; i < times; i++)
            {
                var user = new StatsigUser
                {
                    UserID = $"user_id_{i}",
                    Email = "testuser@statsig.com",
                };
                user.AddCustomProperty("key", "value");

                _serverDriver.LogEvent(user, "test_event", 1, new Dictionary<string, string>() { { "Key", "Value" } });
                _serverDriver.LogEvent(user, "test_event_2", 1, new Dictionary<string, string>() { { "Key", "Value" } });
                Assert.Equal(4 * i + 2, _serverDriver._eventLogger._eventLogQueue.Count);

                _serverDriver.LogEvent(user, "test_event_2", 1, new Dictionary<string, string>() { { "Key", "Value" } });
                Assert.Equal(4 * i + 2, _serverDriver._eventLogger._eventLogQueue.Count);

                _serverDriver.LogEvent(user, "test_event_2", 1, new Dictionary<string, string>() { { "Key", "Value" }, { "Key2", "Value2" } });
                Assert.Equal(4 * i + 3, _serverDriver._eventLogger._eventLogQueue.Count);

                _serverDriver.LogEvent(user, "test_event_2", 1, new Dictionary<string, string>() { { "Key", "Value" }, { "Key2", "Value2" } });
                Assert.Equal(4 * i + 3, _serverDriver._eventLogger._eventLogQueue.Count);

                _serverDriver.LogEvent(user, "test_event_3", 1, new Dictionary<string, string>() { { "Key", "Value" }, { "Key2", "Value2" } });
                Assert.Equal(4 * i + 4, _serverDriver._eventLogger._eventLogQueue.Count);

                await Task.Delay(delay);
            }
        }

        private async Task RunChecksWithoutAssert(int delay, int times)
        {
            for (int i = 0; i < times; i++)
            {
                var user = new StatsigUser
                {
                    UserID = $"user_id_{i}",
                    Email = "testuser@statsig.com",
                };
                user.AddCustomProperty("key", "value");

                _serverDriver.LogEvent(user, "test_event", 1, new Dictionary<string, string>() { { "Key", "Value" } });
                _serverDriver.LogEvent(user, "test_event_2", 1, new Dictionary<string, string>() { { "Key", "Value" } });
                _serverDriver.LogEvent(user, "test_event_2", 1, new Dictionary<string, string>() { { "Key", "Value" } });
                _serverDriver.LogEvent(user, "test_event_2", 1, new Dictionary<string, string>() { { "Key", "Value" }, { "Key2", "Value2" } });
                _serverDriver.LogEvent(user, "test_event_2", 1, new Dictionary<string, string>() { { "Key", "Value" }, { "Key2", "Value2" } });
                _serverDriver.LogEvent(user, "test_event_3", 1, new Dictionary<string, string>() { { "Key", "Value" }, { "Key2", "Value2" } });
                
                await Task.Delay(delay);
            }
        }

        private async Task Start()
        {
            _serverDriver = new ServerDriver(
               "secret-server-key", 
               new StatsigOptions(_server.Urls[0] + "/v1") { RulesetsSyncInterval = 0.01, IDListsSyncInterval = 0.01 }
           );
           await _serverDriver.Initialize();
        }
    }
}
