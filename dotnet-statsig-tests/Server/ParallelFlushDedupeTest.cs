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

namespace dotnet_statsig_tests.Server;

[Collection("Statsig Singleton Tests")]
public class ParallelFlushDedupeTest : IAsyncLifetime, IResponseProvider
{
    private WireMockServer _server;
    private int _flushedEventCount;

    public async Task InitializeAsync()
    {
        _server = WireMockServer.Start();
        _server.ResetLogEntries();
        _server.Given(
            Request.Create().WithPath("/v1/log_event").UsingPost()
        ).RespondWith(this);

        await StatsigServer.Initialize(
            "secret-key",
            new StatsigServerOptions(apiUrlBase: _server.Urls[0] + "/v1")
        );
    }

    public Task DisposeAsync()
    {
        _server.Stop();
        return Task.CompletedTask;
    }

    [Fact]
    public async void TestManyGateChecks()
    {
        const int limit = 1_000_000;
        var numbers = Enumerable.Range(0, limit).ToList();

        Parallel.ForEach(numbers, async number =>
        {
            await StatsigServer.CheckGate(
                new StatsigUser { UserID = "User", Email = "user@email.com" }, "a_gate");
        });

        await StatsigServer.Shutdown();

        Assert.Equal(1, _flushedEventCount);
    }


    public async Task<(ResponseMessage Message, IMapping Mapping)> ProvideResponseAsync(RequestMessage requestMessage,
        IWireMockServerSettings settings)
    {
        if (!requestMessage.AbsolutePath.Contains("/v1/log_event"))
        {
            throw new Exception("Not Mocked");
        }

        var body = (JObject)requestMessage.BodyAsJson;
        var events = ((JArray)body["events"])?.ToObject<List<JObject>>();
        _flushedEventCount += events?.Count ?? 0;

        return await Response.Create()
            .WithStatusCode(200)
            .ProvideResponseAsync(requestMessage, settings);
    }
}