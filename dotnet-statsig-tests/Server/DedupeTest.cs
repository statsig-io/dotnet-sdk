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
    public class DedupeTest : IAsyncLifetime, IResponseProvider
    {
        private const int NUM_EVENTS = 8;
        private const int NUM_LOOOPS = 20;
        private const int NUM_THREADS = 10;

        private WireMockServer _server;
        private string _baseUrl;
        private int _flushedEventCount;
        private int _getIdListCount;
        private int _list1Count;
        private ServerDriver _serverDriver;

        Task IAsyncLifetime.InitializeAsync()
        {
            _server = WireMockServer.Start();
            _baseUrl = _server.Urls[0];
            _server.ResetLogEntries();
            _server.Given(
                Request.Create().WithPath("/v1/download_config_specs/secret-server-key.json").UsingGet()
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
        public async Task<(ResponseMessage Message, IMapping Mapping)> ProvideResponseAsync(
            RequestMessage requestMessage, IWireMockServerSettings settings)
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
                _getIdListCount++;
                var url = _baseUrl + "/list_1";
                var body = $@"{{
                    'list_1': {{
                        'name': 'list_1',
                        'size': {3 * _getIdListCount},
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
                _list1Count++;
                if (_list1Count > 1)
                {
                    body = string.Format("+{0}\n-{0}\n", _list1Count);
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
            await RunChecksAndAssert();

            await _serverDriver.Shutdown();
            // make sure we ultimately flushed all events
            Assert.Equal(NUM_EVENTS * NUM_LOOOPS, _flushedEventCount);
        }

        [Fact]
        public async void TestDedupeConcurrently()
        {
            await Start();
            var tasks = new List<Task>();
            for (var i = 0; i < NUM_THREADS; i++)
            {
                tasks.Add(RunChecksWithoutAssert(i));
            }

            await Task.WhenAll(tasks);

            await _serverDriver.Shutdown();
            // make sure we ultimately flushed all events
            Assert.Equal(NUM_EVENTS * NUM_LOOOPS * NUM_THREADS, _flushedEventCount);
        }

        private async Task RunChecksAndAssert()
        {
            for (var i = 0; i < NUM_LOOOPS; i++)
            {
                var user = new StatsigUser
                {
                    UserID = $"user_id_{i}",
                    Email = "testuser@statsig.com",
                };
                user.AddCustomProperty("key", "value");

                var queue = _serverDriver._eventLogger._eventLogQueue;

                await _serverDriver.CheckGate(user, "always_on_gate"); // Log 1
                await _serverDriver.CheckGate(user, "always_on_gate"); // Dedupe
                await _serverDriver.CheckGate(user, "on_for_statsig_email"); // Log 2
                Assert.Equal(NUM_EVENTS * i + 2, queue.Count);

                await _serverDriver.GetConfig(user, "sample_experiment"); // Log 3
                await _serverDriver.GetConfig(user, "sample_experiment"); // Dedupe
                await _serverDriver.GetConfig(user, "test_config"); // Log 4
                Assert.Equal(NUM_EVENTS * i + 4, queue.Count);

                var aLayer = await _serverDriver.GetLayer(user, "a_layer");
                aLayer.Get("layer_param", false); // Log 5
                aLayer.Get("layer_param", false); // Dedupe
                var bLayer = await _serverDriver.GetLayer(user, "b_layer_no_alloc");
                bLayer.Get("b_param", "err"); // Log 6
                Assert.Equal(NUM_EVENTS * i + 6, queue.Count);

                _serverDriver.LogEvent(user, "custom_event"); // Log 7
                _serverDriver.LogEvent(user, "custom_event"); // Log 8

                Assert.Equal(NUM_EVENTS * i + 8, queue.Count);

                await Task.Delay(10);
            }
        }

        private async Task RunChecksWithoutAssert(int taskId)
        {
            for (var i = 0; i < NUM_LOOOPS; i++)
            {
                var user = new StatsigUser
                {
                    UserID = $"user_id_{i}_{taskId}",
                    Email = "testuser@statsig.com",
                };
                user.AddCustomProperty("key", "value");

                await _serverDriver.CheckGate(user, "always_on_gate"); // Log 1
                await _serverDriver.CheckGate(user, "always_on_gate"); // Dedupe
                await _serverDriver.CheckGate(user, "on_for_statsig_email"); // Log 2

                await _serverDriver.GetConfig(user, "sample_experiment"); // Log 3
                await _serverDriver.GetConfig(user, "sample_experiment"); // Dedupe
                await _serverDriver.GetConfig(user, "test_config"); // Log 4

                var aLayer = await _serverDriver.GetLayer(user, "a_layer");
                aLayer.Get("layer_param", false); // Log 5
                aLayer.Get("layer_param", false); // Dedupe
                var bLayer = await _serverDriver.GetLayer(user, "b_layer_no_alloc");
                bLayer.Get("b_param", "err"); // Log 6

                _serverDriver.LogEvent(user, "custom_event"); // Log 7
                _serverDriver.LogEvent(user, "custom_event"); // Log 8

                await Task.Delay(10);
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