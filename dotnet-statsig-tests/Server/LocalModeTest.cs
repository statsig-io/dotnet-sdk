using System.Threading.Tasks;
using Statsig;
using Statsig.Server;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.ResponseProviders;
using WireMock.Server;
using WireMock.Settings;
using Xunit;

namespace dotnet_statsig_tests.Server;

public class LocalModeTest : IAsyncLifetime, IResponseProvider
{
    private WireMockServer _server;
    private ServerDriver _driver;
    private int _networkCalls;

    public Task InitializeAsync()
    {
        _networkCalls = 0;
        _server = WireMockServer.Start();
        _server.Given(Request.Create().WithPath("*").UsingAnyMethod()).RespondWith(this);
        
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_driver == null)
        {
            return;
        }

        await _driver.Shutdown();
    }

    [Fact]
    public async void TestNetworkCallsAreMadeWhenLocalModeIsFalse()
    {
        await InitializeWithLocalMode(false);
        Assert.NotEqual(0, _networkCalls);
    }

    [Fact]
    public async void TestNetworkCallsAreNotMadeWhenLocalModeIsTrue()
    {
        await InitializeWithLocalMode(true);
        Assert.Equal(0, _networkCalls);
    }

    public async Task<(ResponseMessage Message, IMapping Mapping)> ProvideResponseAsync(RequestMessage requestMessage,
        IWireMockServerSettings settings)
    {
        _networkCalls++;

        return await Response.Create()
            .WithStatusCode(200)
            .WithBody("{}")
            .ProvideResponseAsync(requestMessage, settings);
    }

    private async Task InitializeWithLocalMode(bool localMode)
    {
        _driver = new ServerDriver("secret-key", new StatsigServerOptions(_server.Urls[0])
        {
            LocalMode = localMode
        });

        await _driver.Initialize();
    }
}