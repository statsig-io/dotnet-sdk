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
using System;

namespace dotnet_statsig_tests
{
    [Collection("Statsig Singleton Tests")]
    public class LayerExposureTest : IAsyncLifetime, IResponseProvider
    {
        WireMockServer _server;
        StatsigUser _user = new()
        {
            UserID = "123",
        };
        List<JObject> _events;

        Task IAsyncLifetime.InitializeAsync()
        {
            _events = new List<JObject>();

            _server = WireMockServer.Start();
            _server.ResetLogEntries();
            _server.Given(
                Request.Create().WithPath("/v1/download_config_specs/secret-server-key.json").UsingGet()
            ).RespondWith(this);
            _server.Given(
                Request.Create().WithPath("/v1/log_event").UsingPost()
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
                    .WithBody(TestData.layerExposuresDownloadConfigSpecs)
                    .ProvideResponseAsync(requestMessage, settings);
            }

            if (requestMessage.AbsolutePath.Contains("/v1/log_event"))
            {
                var body = (requestMessage.BodyAsJson as JObject);
                _events = ((JArray)body["events"]).ToObject<List<JObject>>();
                return await Response.Create()
                    .WithStatusCode(200)
                    .ProvideResponseAsync(requestMessage, settings);
            }

            return await Response.Create()
                .WithStatusCode(404)
                .ProvideResponseAsync(requestMessage, settings);
        }

        [Fact]
        public async void TestDoesNotLogOnGetLayer()
        {
            await Start();

            await StatsigServer.GetLayer(_user, "unallocated_layer");
            await StatsigServer.Shutdown();

            Assert.Empty(_events);
        }

        [Fact]
        public async void TestDoesNotLogOnInvalidType()
        {
            await Start();

            var layer = await StatsigServer.GetLayer(_user, "unallocated_layer");
            layer.Get("an_int", new List<string>());
            await StatsigServer.Shutdown();

            Assert.Empty(_events);
        }

        [Fact]
        public async void TestDoesNotLogNonExistentKeys()
        {
            await Start();

            var layer = await StatsigServer.GetLayer(_user, "unallocated_layer");
            layer.Get("a_string", "err");
            await StatsigServer.Shutdown();

            Assert.Empty(_events);
        }

        [Fact]
        public async void TestUnallocatedLayerLogging()
        {
            await Start();

            var layer = await StatsigServer.GetLayer(_user, "unallocated_layer");
            layer.Get("an_int", 0);
            await StatsigServer.Shutdown();

            Assert.Single(_events);
            Assert.Equal(JObject.Parse(@"{
                'config': 'unallocated_layer',
                'ruleID': 'default',
                'allocatedExperiment': '',
                'parameterName': 'an_int',
                'isExplicitParameter': 'false',
                'reason': 'Network',
            }"), _events[0]["metadata"]);
        }

        [Fact]
        public async void TestExplicitVsImplicitParameterLogging()
        {
            await Start();

            var layer = await StatsigServer.GetLayer(_user, "explicit_vs_implicit_parameter_layer");
            layer.Get("an_int", 0);
            layer.Get("a_string", "err");
            await StatsigServer.Shutdown();

            Assert.Equal(2, _events.Count);
            Assert.Equal(JObject.Parse(@"{
                'config': 'explicit_vs_implicit_parameter_layer',
                'ruleID': 'alwaysPass',
                'allocatedExperiment': 'experiment',
                'parameterName': 'an_int',
                'isExplicitParameter': 'true',
                'reason': 'Network',
            }"), _events[0]["metadata"]);
            Assert.Equal(JObject.Parse(@"{'arr': []}")["arr"], _events[0]["secondaryExposures"]);

            Assert.Equal(JObject.Parse(@"{
                'config': 'explicit_vs_implicit_parameter_layer',
                'ruleID': 'alwaysPass',
                'allocatedExperiment': '',
                'parameterName': 'a_string',
                'isExplicitParameter': 'false',
                'reason': 'Network',
            }"), _events[1]["metadata"]);
            Assert.Equal(JObject.Parse(@"{'arr': []}")["arr"], _events[1]["secondaryExposures"]);
        }

        [Fact]
        public async void TestDifferentObjectTypeLogging()
        {
            await Start();

            var layer = await StatsigServer.GetLayer(_user, "different_object_type_logging_layer");
            layer.Get("a_bool", false);
            layer.Get("an_int", 0);
            layer.Get("a_float", 0.0f);
            layer.Get("a_double", 0.0d);
            layer.Get("a_long", 0L);
            layer.Get<ulong>("a_ulong", 0);
            layer.Get("a_string", "err");
            layer.Get("an_array", new string[] { });
            layer.Get("an_object", new Dictionary<string, object> { });
            await StatsigServer.Shutdown();

            Assert.Equal("a_bool", _events[0]["metadata"]["parameterName"]);
            Assert.Equal("an_int", _events[1]["metadata"]["parameterName"]);
            Assert.Equal("a_float", _events[2]["metadata"]["parameterName"]);
            Assert.Equal("a_double", _events[3]["metadata"]["parameterName"]);
            Assert.Equal("a_long", _events[4]["metadata"]["parameterName"]);
            Assert.Equal("a_ulong", _events[5]["metadata"]["parameterName"]);
            Assert.Equal("a_string", _events[6]["metadata"]["parameterName"]);
            Assert.Equal("an_array", _events[7]["metadata"]["parameterName"]);
            Assert.Equal("an_object", _events[8]["metadata"]["parameterName"]);

            Assert.Equal(9, _events.Count);
        }

        [Fact]
        public async void TestLogsUserAndEventName()
        {
            await Start();

            var user = new StatsigUser { UserID = "dan", Email = "dan@theman.com" };

            var layer = await StatsigServer.GetLayer(user, "unallocated_layer");
            layer.Get("an_int", 0);
            await StatsigServer.Shutdown();
            Assert.Equal(JObject.Parse(@"{
                'customIDs': {},
                'userID': 'dan',
                'email': 'dan@theman.com',
            }"), _events[0]["user"]);

            Assert.Equal("statsig::layer_exposure", _events[0]["eventName"]);

            Assert.Single(_events);
        }

        private async Task Start()
        {
            await StatsigServer.Initialize(
               "secret-server-key",
               new StatsigServerOptions(_server.Urls[0] + "/v1")
           );
        }
    }
}
