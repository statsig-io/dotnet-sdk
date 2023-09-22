using System.Collections.Generic;
using Xunit;
using WireMock.Server;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using System.Threading.Tasks;
using WireMock;
using WireMock.Settings;
using Statsig;
using Statsig.Client;
using Newtonsoft.Json.Linq;
using WireMock.ResponseProviders;

namespace dotnet_statsig_tests
{
    [Collection("Statsig Singleton Tests")]
    public class LayerTest : IAsyncLifetime, IResponseProvider
    {
        WireMockServer _server;
        List<JObject> _events;
        IResponseBuilder _initResponseBuilder;

        Task IAsyncLifetime.InitializeAsync()
        {
            _events = new List<JObject>();

            _server = WireMockServer.Start();
            _server.ResetLogEntries();
            _server.Given(
                Request.Create().WithPath("/v1/initialize").UsingPost()
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
            if (requestMessage.AbsolutePath.Contains("/v1/initialize"))
            {
                return await _initResponseBuilder
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
        public async void TestGettingValues()
        {
            await Start();

            var layer = StatsigClient.GetLayer("unallocated_layer");
            Assert.True(layer.Get("a_bool", false));
            Assert.Equal(99, layer.Get("an_int", 0));
            Assert.Equal(12.34, layer.Get("a_double", 0.0));
            Assert.Equal(9223372036854775806L, layer.Get("a_long", 0L));
            Assert.Equal(new string[] { "a", "b" }, layer.Get("an_array", new string[] { }));
            Assert.Equal(new List<string> { "a", "b" }, layer.Get("an_array", new List<string> { }));
            Assert.Equal(new HashSet<string> { "a", "b" }, layer.Get("an_array", new HashSet<string> { }));
            Assert.Equal(new Dictionary<string, object> { { "c", "d" } }, layer.Get("an_object", new Dictionary<string, object> { }));
            Assert.Equal(new Dictionary<string, object> { { "c", "d" } }, layer.Get("an_object", new Dictionary<string, object> { }));
            await StatsigClient.Shutdown();
        }

        [Fact]
        public async void TestUnallocatedLayerExposure()
        {
            await Start();

            var layer = StatsigClient.GetLayer("unallocated_layer");
            layer.Get("an_int", 0);
            await StatsigClient.Shutdown();

            Assert.Equal(JObject.Parse(@"{
                'config': 'unallocated_layer',
                'ruleID': 'default',
                'allocatedExperiment': '',
                'parameterName': 'an_int',
                'isExplicitParameter': 'false',
            }"), _events[0]["metadata"]);

            Assert.Equal(JObject.Parse(@"{'arr': [{
                'gate': 'undelegated_secondary_exp',
                'gateValue': 'false',
                'ruleID': 'default'
            }]}")["arr"], _events[0]["secondaryExposures"]);

            Assert.Single(_events);
        }

        [Fact]
        public async void TestAllocatedLayerExposure()
        {
            await Start();

            var layer = StatsigClient.GetLayer("allocated_layer");
            layer.Get("explicit_key", "err");
            layer.Get("implicit_key", "err");
            await StatsigClient.Shutdown();

            Assert.Equal(JObject.Parse(@"{
                'config': 'allocated_layer',
                'ruleID': 'default',
                'allocatedExperiment': 'an_experiment',
                'parameterName': 'explicit_key',
                'isExplicitParameter': 'true',
            }"), _events[0]["metadata"]);

            Assert.Equal(JObject.Parse(@"{'arr': [{
                'gate': 'secondary_exp',
                'gateValue': 'false',
                'ruleID': 'default'
            }]}")["arr"], _events[0]["secondaryExposures"]);

            Assert.Equal(JObject.Parse(@"{
                'config': 'allocated_layer',
                'ruleID': 'default',
                'allocatedExperiment': '',
                'parameterName': 'implicit_key',
                'isExplicitParameter': 'false',
            }"), _events[1]["metadata"]);

            Assert.Equal(JObject.Parse(@"{'arr': [{
                'gate': 'undelegated_secondary_exp',
                'gateValue': 'false',
                'ruleID': 'default'
            }]}")["arr"], _events[1]["secondaryExposures"]);

            Assert.Equal(2, _events.Count);
        }

        private async Task Start(IResponseBuilder initResponseBuilder = null)
        {
            _initResponseBuilder = initResponseBuilder ?? Response.Create()
                    .WithStatusCode(200)
                    .WithBody(TestData.layerInitialize);

            await StatsigClient.Initialize(
               "client-sdk-key",
               new StatsigUser() { UserID = "dloomb" },
               new StatsigOptions(_server.Urls[0] + "/v1")
           );
        }
    }
}
