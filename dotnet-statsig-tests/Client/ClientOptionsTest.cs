using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Statsig;
using Statsig.Client;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.ResponseProviders;
using WireMock.Server;
using WireMock.Settings;
using Xunit;

namespace dotnet_statsig_tests
{
    [Collection("Statsig Singleton Tests")]
    public class ClientOptionsTest : IAsyncLifetime
    {
        WireMockServer _server;

        Task IAsyncLifetime.InitializeAsync()
        {
            _server = WireMockServer.Start();
            _server.ResetLogEntries();
            _server.Given(
                Request.Create().WithPath("/v1/initialize").UsingPost()
            ).RespondWith(
                Response.Create().WithStatusCode(200).WithBodyAsJson(TestData.ClientInitializeResponse)
                    .WithDelay(TimeSpan.FromSeconds(2))
            );
            _server.Given(
                Request.Create().WithPath("/v1/log_event").UsingPost()
            ).RespondWith(
                Response.Create().WithStatusCode(200)
            );
            return Task.CompletedTask;
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            await TestUtil.EnsureShutdown();
            _server.Stop();
        }

        [Fact]
        public async void TestClientTimeout()
        {
            var startTime = DateTime.Now;
            var user = new StatsigUser
            {
                UserID = "123",
                customIDs = new Dictionary<string, string> { { "random_id", "id123" } },
            };
            user.AddPrivateAttribute("value", "secret");
            user.AddCustomProperty("value", "public");
            await StatsigClient.Initialize
            (
                "client-fake-key",
                user,
                // use a fake persistent store path to disable local storage
                new StatsigOptions(apiUrlBase: _server.Urls[0] + "/v1")
                { ClientRequestTimeoutMs = 10, PersistentStorageFolder = "abc/def" }
            );

            Assert.False(StatsigClient.CheckGate("test_gate"));
            var endTime = DateTime.Now;

            Assert.True(endTime.Subtract(TimeSpan.FromMilliseconds(600)) < startTime); // make sure it took less than 600 ms to complete
            await StatsigClient.Shutdown();

            startTime = DateTime.Now;
            await StatsigClient.Initialize
            (
                "client-fake-key",
                user,
                // use a fake persistent store path to disable local storage\
                new StatsigOptions(apiUrlBase: _server.Urls[0] + "/v1") { PersistentStorageFolder = "abc/def" }
            );

            Assert.True(StatsigClient.CheckGate("test_gate"));
            endTime = DateTime.Now;
            Assert.True(endTime.Subtract(TimeSpan.FromSeconds(1)) >=
                        startTime); // should've taken >= 3 seconds given the artificial delay setup above
            await StatsigClient.Shutdown();
        }
    }
}