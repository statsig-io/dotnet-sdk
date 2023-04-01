using System.Threading.Tasks;
using StackExchange.Redis;
using Statsig.Server.Interfaces;

namespace StatsigRedis;

public class RedisDataStore : IDataStore
{
    /**
     * Set to true if you would like to attempt to pull all config updates from the DataStore.
     * When false, the DataStore will only be queried during Statsig.initialize 
     */
    public bool AllowConfigSpecPolling = false;

    private readonly IDatabase _database;

    public RedisDataStore(IDatabase database)
    {
        _database = database;
    }

    public RedisDataStore(string host, int port, string password)
    {
        var config = new ConfigurationOptions();
        config.EndPoints.Add($"{host}:{port}");
        config.Password = password;

        _database = ConnectionMultiplexer.Connect(config).GetDatabase();
    }

    public bool SupportsPollingUpdates(string key)
    {
        if (AllowConfigSpecPolling)
        {
            return key == DataStoreKey.Rulesets;
        }

        return false;
    }

    public async Task<string?> Get(string key)
    {
        var result = await _database.StringGetAsync(key);
        return result.IsNull ? null : result.ToString();
    }

    public async Task Set(string key, string value)
    {
        await _database.StringSetAsync(key, value);
    }

    // noop

    public Task Init() => Task.CompletedTask;
    public Task Shutdown() => Task.CompletedTask;
}