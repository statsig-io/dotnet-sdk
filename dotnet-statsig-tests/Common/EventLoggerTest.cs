using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Statsig;
using Statsig.Network;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.ResponseProviders;
using WireMock.Server;
using WireMock.Settings;
using Xunit;

namespace dotnet_statsig_tests;

public class EventLoggerTest : IAsyncLifetime, IResponseProvider
{
    private WireMockServer _server;
    private int _flushedEventCount;
    private EventLogger _logger;
    private static int ThresholdSeconds = 2;

    public Task InitializeAsync()
    {
        _server = WireMockServer.Start();
        _server.ResetLogEntries();
        _server.Given(
            Request.Create().WithPath("/log_event").UsingPost()
        ).RespondWith(this);

        var sdkDetails = SDKDetails.GetClientSDKDetails();
        var dispatcher = new RequestDispatcher("a-key", new StatsigOptions(apiUrlBase: _server.Urls[0]), sdkDetails, "my-session");
        _logger = new EventLogger(dispatcher, sdkDetails, maxQueueLength: 3, maxThresholdSecs: ThresholdSeconds);

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _server.Stop();
        return Task.CompletedTask;
    }

    [Fact]
    public void TestPeriodicScheduling()
    {
        _logger.Enqueue(new EventLog { EventName = "one" });
        _logger.Enqueue(new EventLog { EventName = "two" });
        Task.Delay(TimeSpan.FromSeconds(ThresholdSeconds * 2));
        Assert.Equal(2, _flushedEventCount);
    }

    [Fact]
    public async Task TestShutdownWithInFlightLogs()
    {
        _logger.Enqueue(new EventLog { EventName = "one" });
        _logger.Enqueue(new EventLog { EventName = "two" });
        await _logger.Shutdown();
        Assert.Equal(2, _flushedEventCount);
    }

    public async
        Task<(ResponseMessage Message, IMapping Mapping)>
        ProvideResponseAsync(
            RequestMessage requestMessage,
            IWireMockServerSettings settings
        )
    {
        if (!requestMessage.AbsolutePath.Contains("/log_event"))
        {
            throw new Exception("Not Mocked");
        }

        var body = (JObject)requestMessage.BodyAsJson;
        var events = ((JArray)body["events"])?.ToObject<List<JObject>>();
        _flushedEventCount += events?.Count ?? 0;

        return await Response.Create()
            .WithStatusCode(200)
            .WithDelay(TimeSpan.FromMilliseconds(100))
            .ProvideResponseAsync(requestMessage, settings);
    }
}
