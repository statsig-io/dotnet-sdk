


using System.Collections.Generic;
using System.Threading.Tasks;
using Statsig.Server.Evaluation;
using Statsig.Server.Interfaces;

namespace Statsig.Server
{
    public class UserPersistentStorageHandler
    {
        private readonly IUserPersistentStorage? _persistentStorage;

        public UserPersistentStorageHandler(IUserPersistentStorage? persistentStorage)
        {
            _persistentStorage = persistentStorage;
        }

        public async Task<Dictionary<string, StickyValue>?> Load(StatsigUser user, string idType)
        {
            if (_persistentStorage == null)
            {
                return null;
            }
            var key = GetKey(user, idType);
            try
            {
                return await _persistentStorage.Load(key);
            }
            catch
            {
                return null;
            }
        }

        internal async Task Save(StatsigUser user, string idType, string configName, ConfigEvaluation value, double configSyncTime)
        {
            if (_persistentStorage == null)
            {
                return;
            }
            var key = GetKey(user, idType);
            try
            {
                await _persistentStorage.Save(key, configName, value.ToStickyValue(configSyncTime));
            }
            catch
            {
                // ignored
            }
        }

        public async Task Delete(StatsigUser user, string idType, string configName)
        {
            if (_persistentStorage == null)
            {
                return;
            }
            var key = GetKey(user, idType);
            try
            {
                await _persistentStorage.Delete(key, configName);
            }
            catch
            {
                // ignored
            }
        }

        private string GetKey(StatsigUser user, string idType)
        {
            var unitID = Evaluator.GetUnitID(user, idType);
            return unitID + ":" + idType;
        }
    }
}