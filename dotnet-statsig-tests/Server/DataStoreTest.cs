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

[Collection("Statsig Singleton Tests")]
public class DataStoreTest : IAsyncLifetime, IResponseProvider
{
    private WireMockServer _server;

    private readonly StatsigUser _user = new()
    {
        UserID = "a_user"
    };

    private class DummyStore : IDataStore
    {
        internal CountdownEvent OnWriteCountdownEvent;
        internal CountdownEvent OnReadCountdownEvent;
        internal bool SupportPollingUpdates;

        internal Dictionary<string, string> Store = new()
        {
            {
                DataStoreKey.Rulesets,
                TestData.DataStoreTestBootstrap
            },
        };

        public bool SupportsPollingUpdates(string key)
        {
            return SupportPollingUpdates;
        }

        public Task Init() => Task.CompletedTask;
        public Task Shutdown() => Task.CompletedTask;

        public Task<string> Get(string key)
        {
            Store.TryGetValue(key, out var value);
            DeferredSignalReadCountDownEvent();
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
            OnWriteCountdownEvent?.Signal();
            return Task.CompletedTask;
        }

        private async void DeferredSignalReadCountDownEvent()
        {
            var captured = OnReadCountdownEvent;
            if (OnReadCountdownEvent == null)
            {
                return;
            }

            OnReadCountdownEvent = null;
            await Task.Delay(100);
            captured.Signal();
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
        var backgroundSyncBlocker = new CountdownEvent(1);
        Delay.Wait = (_, _) => TestUtil.WaitFor(backgroundSyncBlocker.Wait);

        var onDummyStoreWriteCountdownEvent = new CountdownEvent(1);
        var store = new DummyStore
        {
            OnWriteCountdownEvent = onDummyStoreWriteCountdownEvent
        };

        await StatsigServer.Initialize("secret-key", new StatsigServerOptions(_server.Urls[0])
        {
            DataStore = store
        });

        backgroundSyncBlocker.Signal(); // Let the background sync happen
        onDummyStoreWriteCountdownEvent.Wait(); // Wait for the dummy store to be written to

        var result = await store.Get(DataStoreKey.Rulesets);
        Assert.Contains("gate_from_network", result);
    }

    [Fact]
    public async void TestDataStoreReturnsPolledUpdates()
    {
        var backgroundSyncBlocker = new CountdownEvent(1);
        Delay.Wait = (_, _) => TestUtil.WaitFor(backgroundSyncBlocker.Wait);

        var store = new DummyStore
        {
            SupportPollingUpdates = true,
        };

        await StatsigServer.Initialize("secret-key", new StatsigServerOptions()
        {
            LocalMode = true,
            DataStore = store,
            RulesetsSyncInterval = 0.0001
        });

        var onDummyStoreReadCountdownEvent = new CountdownEvent(1);
        store.OnReadCountdownEvent = onDummyStoreReadCountdownEvent;
        store.Store = new Dictionary<string, string>
        {
            {
                DataStoreKey.Rulesets,
                TestData.DataStoreTestBackgroundSync
            },
        };

        backgroundSyncBlocker.Signal();
        onDummyStoreReadCountdownEvent.Wait();

        var result = await StatsigServer.CheckGate(_user, "updated_gate_from_adapter");
        Assert.True(result);
    }
}