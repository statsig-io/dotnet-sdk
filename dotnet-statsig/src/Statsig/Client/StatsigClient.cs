using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Statsig.Client
{
    public static class StatsigClient
    {
        static ClientDriver _singleDriver;

        public static async Task Initialize(string clientKey, StatsigUser user = null, ConnectionOptions options = null)
        {
            if (_singleDriver != null)
            {
                throw new InvalidOperationException("Cannot reinitialize client.");
            }

            _singleDriver = new ClientDriver(clientKey, options);
            await _singleDriver.Initialize(user);
        }

        public static void Shutdown()
        {
            EnsureInitialized();
            _singleDriver.Shutdown();
        }

        public static bool CheckGate(string gateName)
        {
            EnsureInitialized();
            return _singleDriver.CheckGate(gateName);
        }

        public static DynamicConfig GetConfig(string configName)
        {
            EnsureInitialized();
            return _singleDriver.GetConfig(configName);
        }

        public static DynamicConfig GetExperiment(string experimentName)
        {
            EnsureInitialized();
            return _singleDriver.GetConfig(experimentName);
        }

        public static void LogEvent(
            string eventName,
            string value = null,
            IReadOnlyDictionary<string, string> metadata = null)
        {
            EnsureInitialized();
            _singleDriver.LogEvent(eventName, value, metadata);
        }

        public static void LogEvent(
            string eventName,
            int value,
            IReadOnlyDictionary<string, string> metadata = null)
        {
            EnsureInitialized();
            _singleDriver.LogEvent(eventName, value, metadata);
        }

        public static void LogEvent(
            string eventName,
            double value,
            IReadOnlyDictionary<string, string> metadata = null)
        {
            EnsureInitialized();
            _singleDriver.LogEvent(eventName, value, metadata);
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
