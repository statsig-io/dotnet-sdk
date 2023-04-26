using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Statsig.Client
{
    public static class StatsigClient
    {
        static ClientDriver? _singleDriver;

        public static async Task Initialize(string clientKey, StatsigUser? user = null, StatsigOptions? options = null)
        {
            if (_singleDriver != null)
            {
                throw new InvalidOperationException("Cannot reinitialize client.");
            }

            _singleDriver = new ClientDriver(clientKey, options);
            await _singleDriver.Initialize(user);
        }

        public static async Task Shutdown()
        {
            EnsureInitialized();
            await _singleDriver!.Shutdown();
            _singleDriver = null;
        }

        public static bool CheckGate(string gateName)
        {
            EnsureInitialized();
            return _singleDriver!.CheckGate(gateName);
        }

        public static DynamicConfig GetConfig(string configName)
        {
            EnsureInitialized();
            return _singleDriver!.GetConfig(configName);
        }

        public static DynamicConfig GetExperiment(string experimentName)
        {
            EnsureInitialized();
            return _singleDriver!.GetExperiment(experimentName);
        }

        public static Layer GetLayer(string layerName)
        {
            EnsureInitialized();
            return _singleDriver!.GetLayer(layerName);
        }

        public static void LogEvent(
            string eventName,
            string? value = null,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            EnsureInitialized();
            _singleDriver!.LogEvent(eventName, value, metadata);
        }

        public static void LogEvent(
            string eventName,
            int value,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            EnsureInitialized();
            _singleDriver!.LogEvent(eventName, value, metadata);
        }

        public static void LogEvent(
            string eventName,
            double value,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            EnsureInitialized();
            _singleDriver!.LogEvent(eventName, value, metadata);
        }

        public static async Task UpdateUser(StatsigUser user)
        {
            if (user == null)
            {
                throw new InvalidOperationException("user cannot be null.");
            }
            EnsureInitialized();
            await _singleDriver!.UpdateUser(user);
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
