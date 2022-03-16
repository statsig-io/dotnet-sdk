using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Statsig.Server
{
    public static class StatsigServer
    {
        static ServerDriver _singleDriver;

        public static async Task Initialize(string serverSecret, StatsigOptions options = null)
        {
            if (_singleDriver != null)
            {
                if (_singleDriver._serverSecret != serverSecret)
                {
                    throw new InvalidOperationException("Cannot reinitialize SDK with a different serverSecret");
                }
                else
                {
                    return;
                }
            }

            _singleDriver = new ServerDriver(serverSecret, options);
            await _singleDriver.Initialize();
        }

        public static async Task Shutdown()
        {
            EnsureInitialized();
            await _singleDriver.Shutdown();
        }

        public static async Task<bool> CheckGate(StatsigUser user, string gateName)
        {
            EnsureInitialized();
            return await _singleDriver.CheckGate(user, gateName);
        }

        public static async Task<DynamicConfig> GetConfig(StatsigUser user, string configName)
        {
            EnsureInitialized();
            return await _singleDriver.GetConfig(user, configName);
        }

        public static async Task<DynamicConfig> GetExperiment(StatsigUser user, string experimentName)
        {
            EnsureInitialized();
            return await _singleDriver.GetConfig(user, experimentName);
        }

        public static void LogEvent(
            StatsigUser user,
            string eventName,
            string value = null,
            IReadOnlyDictionary<string, string> metadata = null)
        {
            EnsureInitialized();
            _singleDriver.LogEvent(user, eventName, value, metadata);
        }

        public static void LogEvent(
            StatsigUser user,
            string eventName,
            int value,
            IReadOnlyDictionary<string, string> metadata = null)
        {
            EnsureInitialized();
            _singleDriver.LogEvent(user, eventName, value, metadata);
        }

        public static void LogEvent(
            StatsigUser user,
            string eventName,
            double value,
            IReadOnlyDictionary<string, string> metadata = null)
        {
            EnsureInitialized();
            _singleDriver.LogEvent(user, eventName, value, metadata);
        }

        static void EnsureInitialized()
        {
            if (_singleDriver == null)
            {
                throw new InvalidOperationException("Must call Initialize() first.");
            }
        }
    }
}
