using System;
using System.Threading.Tasks;
using Moq;
using StackExchange.Redis;
using Statsig.Server.Interfaces;
using StatsigRedis;
using Xunit;

namespace dotnet_statsig_tests.Server;

[Collection("Statsig Singleton Tests")]
public class RedisDataStoreTest : IAsyncLifetime
{
    private RedisDataStore _store;

    private string _latestSetKey;
    private string _latestSetValue;

    public Task InitializeAsync()
    {
        var mock = new Mock<IDatabase>();

        mock.Setup(x => x.StringGetAsync("a_key", CommandFlags.None))
            .Returns(Task.FromResult(new RedisValue("a_value")));

        mock.Setup(x =>
                x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), null, false, When.Always,
                    CommandFlags.None))
            .Callback((RedisKey key, RedisValue value, TimeSpan? ts, bool b, When w, CommandFlags cf) =>
            {
                _latestSetKey = key.ToString();
                _latestSetValue = value.ToString();
            });

        _store = new RedisDataStore(mock.Object);

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async void TestGettingValues()
    {
        var result = await _store.Get("a_key");
        Assert.Equal("a_value", result);
    }

    [Fact]
    public async void TestSettingValues()
    {
        _latestSetKey = null;
        _latestSetValue = null;

        await _store.Set("a_key", "new_value");

        Assert.Equal("a_key", _latestSetKey);
        Assert.Equal("new_value", _latestSetValue);
    }

    [Fact]
    public void TestConfigPolling()
    {
        Assert.False(_store.SupportsPollingUpdates(DataStoreKey.Rulesets));

        _store.AllowConfigSpecPolling = true;
        Assert.True(_store.SupportsPollingUpdates(DataStoreKey.Rulesets));
    }
}