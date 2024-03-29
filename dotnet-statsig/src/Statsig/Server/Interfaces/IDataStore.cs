using System.Threading.Tasks;

namespace Statsig.Server.Interfaces;

public abstract class DataStoreKey
{
    public const string Rulesets = "statsig.cache";
}

public interface IDataStore
{
    bool SupportsPollingUpdates(string key);
    Task Init();
    Task Shutdown();
    Task<string?> Get(string key);
    Task Set(string key, string value);
}