using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Statsig;
using Statsig.Lib;
using Statsig.Server;
using Statsig.Server.Interfaces;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.ResponseProviders;
using WireMock.Server;
using WireMock.Settings;
using Xunit;

namespace dotnet_statsig_tests;

public class DataStoreTest : IAsyncLifetime, IResponseProvider
{
    private WireMockServer _server;
    private IResponseBuilder _responseBuilder;

    private readonly StatsigUser _user = new()
    {
        UserID = "a_user"
    };

    private class DummyStore : IDataStore
    {
        private readonly CountdownEvent _onWriteCountdownEvent;
        public DummyStore(CountdownEvent onWriteCountdownEvent = null)
        {
            _onWriteCountdownEvent = onWriteCountdownEvent;
        }

        internal Dictionary<string, string> Store = new()
        {
            {
                DataStoreKey.Rulesets,
                TestData.DataStoreTestBootstrap
            },
        };

        public Task Init() => Task.CompletedTask;
        public Task Shutdown() => Task.CompletedTask;

        public Task<string> Get(string key)
        {
            Store.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public Task Set(string key, string value)
        {
            if (value == null)
            {
                Store.Remove(key);
                return Task.CompletedTask;
            }

            Store[key] = value;
            _onWriteCountdownEvent?.Signal();
            return Task.CompletedTask;
        }
    }

    public Task InitializeAsync()
    {
        _server = WireMockServer.Start();
        _server.Given(Request.Create().WithPath("*").UsingAnyMethod()).RespondWith(this);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await StatsigServer.Shutdown();
        Delay.Reset();
    }

    public Task<(ResponseMessage Message, IMapping Mapping)> ProvideResponseAsync(RequestMessage requestMessage,
        IWireMockServerSettings settings)
    {
        var body = "{}";
        if (requestMessage.AbsolutePath.Contains("download_config_specs"))
        {
            body = TestData.DataStoreTestNetwork;
        }

        return Response.Create()
            .WithStatusCode(200)
            .WithBody(body)
            .ProvideResponseAsync(requestMessage, settings);
    }

    [Fact]
    public async void TestBootingFromDataStore()
    {
        await StatsigServer.Initialize("secret-key", new StatsigServerOptions()
        {
            LocalMode = true,
            DataStore = new DummyStore()
        });

        var result = await StatsigServer.CheckGate(_user, "gate_from_adapter");
        Assert.True(result);
    }

    [Fact]
    public async void TestBootingFromDataStoreFallsBackToNetwork()
    {
        var store = new DummyStore
        {
            // Remove any values inside the store
            Store = new Dictionary<string, string>()
        };

        await StatsigServer.Initialize("secret-key", new StatsigServerOptions(_server.Urls[0])
        {
            DataStore = store
        });

        var result = await StatsigServer.CheckGate(_user, "gate_from_network");
        Assert.True(result);
    }

    
    [Fact]
    public async void TestDataStoreGetsUpdatedNetworkValue()
    {
        var countdown = new CountdownEvent(1);
        Delay.Wait = (_, _) => TestUtil.WaitFor(countdown.Wait);

        var onDummyStoreWriteCountdownEvent = new CountdownEvent(1);
        var store = new DummyStore(onDummyStoreWriteCountdownEvent);
        await StatsigServer.Initialize("secret-key", new StatsigServerOptions(_server.Urls[0])
        {
            DataStore = store
        });

        countdown.Signal();
        onDummyStoreWriteCountdownEvent.Wait();

        var result = await store.Get(DataStoreKey.Rulesets);
        Assert.Contains("gate_from_network", result);
    }
}