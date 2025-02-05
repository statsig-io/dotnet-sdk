using System.Collections.Generic;
using Xunit;
using Statsig;
using Statsig.Server;
using Statsig.Lib;
using System.Threading.Tasks;
using Statsig.Network;
using WireMock.ResponseProviders;
using WireMock;
using WireMock.Settings;
using WireMock.Server;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Newtonsoft.Json.Linq;
using Statsig.Server.Evaluation;

namespace dotnet_statsig_tests
{
    [Collection("Statsig Singleton Tests")]
    public class GetFeatureGateTest : IAsyncLifetime, IResponseProvider
    {
        WireMockServer _server;
        string baseURL;
        List<JObject> _events;

        Task IAsyncLifetime.InitializeAsync()
        {
            _events = new List<JObject>();
            _server = WireMockServer.Start();
            baseURL = _server.Urls[0];
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
        public async void GetFeatureGate()
        {
            await Start();

            var user = new StatsigUser
            {
                UserID = "test",
                Email = "test@statsig.com"
            };

            var gate = StatsigServer.GetFeatureGate(user, "on_for_statsig_email");
            Assert.True(gate.Value);
            Assert.Equal("7w9rbTSffLT89pxqpyhuqK", gate.RuleID);
            Assert.Equal(EvaluationReason.Network, gate.Reason);
            Assert.Equal(EvaluationReason.Network, gate.EvaluationDetails?.Reason);
            Assert.Equal(1631638014811, gate.EvaluationDetails?.ConfigSyncTime);
            Assert.Equal(1631638014811, gate.EvaluationDetails?.InitTime);

            var gate2 = StatsigServer.GetFeatureGateWithExposureLoggingDisabled(user, "fake_gate");
            Assert.False(gate2.Value);
            Assert.Equal(EvaluationReason.Unrecognized, gate2.Reason);
            Assert.Equal(EvaluationReason.Unrecognized, gate2.EvaluationDetails?.Reason);
            Assert.Equal(1631638014811, gate.EvaluationDetails?.ConfigSyncTime);
            Assert.Equal(1631638014811, gate.EvaluationDetails?.InitTime);

            await StatsigServer.Shutdown();

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