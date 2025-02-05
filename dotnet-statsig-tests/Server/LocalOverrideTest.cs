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
    public class LocalOverrideTest : IAsyncLifetime, IResponseProvider
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
        public async void TestOverrideGate()
        {
            await Start();

            _serverDriver.OverrideGate("override_gate", true, "1");
            _serverDriver.OverrideGate("override_gate", false, "2");
            _serverDriver.OverrideGate("global_gate", true);

            var user1 = new StatsigUser
            {
                UserID = "1",
            };
            var user2 = new StatsigUser
            {
                UserID = "2",
            };
            var user3 = new StatsigUser
            {
                UserID = "3",
            };
            var result1 = _serverDriver.CheckGateSync(user1, "override_gate");
            Assert.Equal(true, result1);
            var result2 = _serverDriver.CheckGateSync(user2, "override_gate");
            Assert.Equal(false, result2);
            var result3 = _serverDriver.CheckGateSync(user3, "override_gate");
            Assert.Equal(false, result3);
            var result4 = _serverDriver.CheckGateSync(user1, "global_gate");
            Assert.Equal(true, result4);
            var result5 = _serverDriver.CheckGateSync(user3, "global_gate");
            Assert.Equal(true, result5);
            var result6 = _serverDriver.CheckGateSync(user1, "bad_gate");
            Assert.Equal(false, result6);

            await _serverDriver.Shutdown();
        }

        [Fact]
        public async void TestOverrideConfig()
        {
            await Start();
            Dictionary<string, JToken> dict1 = new()
            {
                { "key1", "a" },
            };
            Dictionary<string, JToken> dict2 = new()
            {
                { "key1", "b" },
            };
            Dictionary<string, JToken> dict3 = new()
            {
                { "key1", "c" },
            };
            _serverDriver.OverrideConfig("override_config", dict1, "1");
            _serverDriver.OverrideConfig("override_config", dict2, "2");
            _serverDriver.OverrideConfig("global_config", dict3);

            var user1 = new StatsigUser
            {
                UserID = "1",
            };
            var user2 = new StatsigUser
            {
                UserID = "2",
            };
            var user3 = new StatsigUser
            {
                UserID = "3",
            };
            var result1 = _serverDriver.GetConfigSync(user1, "override_config");
            Assert.Equal("a", result1.Get<string>("key1"));
            var result2 = _serverDriver.GetConfigSync(user2, "override_config");
            Assert.Equal("b", result2.Get<string>("key1"));
            var result3 = _serverDriver.GetConfigSync(user3, "override_config");
            Assert.Equal("", result3.Get<string>("key1", ""));
            var result4 = _serverDriver.GetConfigSync(user1, "global_config");
            Assert.Equal("c", result4.Get<string>("key1"));
            var result5 = _serverDriver.GetConfigSync(user3, "global_config");
            Assert.Equal("c", result5.Get<string>("key1"));
            var result6 = _serverDriver.GetConfigSync(user1, "bad_config");
            Assert.Equal("", result6.Get<string>("key1", ""));

            await _serverDriver.Shutdown();
        }

        [Fact]
        public async void TestOverrideLayer()
        {
            await Start();
            Dictionary<string, JToken> dict1 = new()
            {
                { "key1", "a" },
            };
            Dictionary<string, JToken> dict2 = new()
            {
                { "key1", "b" },
            };
            Dictionary<string, JToken> dict3 = new()
            {
                { "key1", "c" },
            };
            _serverDriver.OverrideLayer("override_layer", dict1, "1");
            _serverDriver.OverrideLayer("override_layer", dict2, "2");
            _serverDriver.OverrideLayer("global_layer", dict3);

            var user1 = new StatsigUser
            {
                UserID = "1",
            };
            var user2 = new StatsigUser
            {
                UserID = "2",
            };
            var user3 = new StatsigUser
            {
                UserID = "3",
            };
            var result1 = _serverDriver.GetLayerSync(user1, "override_layer");
            Assert.Equal("a", result1.Get<string>("key1"));
            var result2 = _serverDriver.GetLayerSync(user2, "override_layer");
            Assert.Equal("b", result2.Get<string>("key1"));
            var result3 = _serverDriver.GetLayerSync(user3, "override_layer");
            Assert.Equal("", result3.Get<string>("key1", ""));
            var result4 = _serverDriver.GetLayerSync(user1, "global_layer");
            Assert.Equal("c", result4.Get<string>("key1"));
            var result5 = _serverDriver.GetLayerSync(user3, "global_layer");
            Assert.Equal("c", result5.Get<string>("key1"));
            var result6 = _serverDriver.GetLayerSync(user1, "bad_layer");
            Assert.Equal("", result6.Get<string>("key1", ""));

            await _serverDriver.Shutdown();
        }

        private async Task Start()
        {
            _serverDriver = new ServerDriver(
                "secret-server-key",
                new StatsigServerOptions(_server.Urls[0] + "/v1") { RulesetsSyncInterval = 0.01, IDListsSyncInterval = 0.01 }
            );
            await _serverDriver.Initialize();
        }
    }
}