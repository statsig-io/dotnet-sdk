using System.Collections.Generic;
using System.Threading.Tasks;
using Statsig;
using Xunit;
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using Statsig.Server;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.ResponseProviders;
using WireMock.Server;
using WireMock.Settings;

namespace dotnet_statsig_tests.Server
{
    [Collection("Statsig Singleton Tests")]
    public class GetListsTest : IAsyncLifetime
    {
        private WireMockServer _server;
        private int _flushedEventCount;

        public async Task InitializeAsync()
        {
            _server = WireMockServer.Start();
            _server.ResetLogEntries();
            _server.Given(Request.Create()
                .WithPath("/v1/download_config_specs/secret-key.json").UsingGet()
            ).RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(SpecStoreResponseData.downloadConfigSpecResponse)
            );

            await StatsigServer.Initialize(
                "secret-key",
                new StatsigServerOptions(_server.Urls[0] + "/v1")
            );
        }

        public async Task DisposeAsync()
        {
            await StatsigServer.Shutdown();
            _server.Stop();
        }

        [Fact]
        public void TestGettingFeatureGateList()
        {
            var gates = StatsigServer.GetFeatureGateList();
            Assert.Equal(new List<string> { "always_on_gate", "on_for_statsig_email", "on_for_id_list" }, gates);
        }

        [Fact]
        public void TestGettingExperimentList()
        {
            var experiments = StatsigServer.GetExperimentList();
            Assert.Equal(new List<string> { "sample_experiment", }, experiments);
        }
    }
}