
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Statsig.Server.Interfaces;
public interface IUserPersistentStorage
{
    Task<Dictionary<string, StickyValue>?> Load(string key);
    Task Save(string key, string configName, StickyValue value);
    Task Delete(string key, string configName);
}