using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Statsig.Lib;

namespace Statsig.Server
{
    public static class StatsigServer
    {
        static ServerDriver? _singleDriver;

        public static async Task Initialize(string serverSecret, StatsigOptions? options = null)
        {
            if (_singleDriver != null)
            {
                if (_singleDriver._serverSecret != serverSecret)
                {
                    throw new StatsigInvalidOperationException("Cannot reinitialize SDK with a different serverSecret");
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
            await EnforceInitialized().Shutdown();
            _singleDriver = null;
        }

        public static async Task<bool> CheckGate(StatsigUser user, string gateName)
        {
            return await EnforceInitialized().CheckGate(user, gateName);
        }

        public static async Task<bool> CheckGateWithExposureLoggingDisabled(StatsigUser user, string gateName)
        {
            return await EnforceInitialized().CheckGateWithExposureLoggingDisabled(user, gateName);
        }

        public static void ManuallyLogGateExposure(StatsigUser user, string gateName)
        {
            EnforceInitialized().LogGateExposure(user, gateName);
        }

        public static async Task<DynamicConfig> GetConfig(StatsigUser user, string configName)
        {
            return await EnforceInitialized().GetConfig(user, configName);
        }

        public static async Task<DynamicConfig> GetConfigWithExposureLoggingDisabled(
            StatsigUser user,
            string configName
        )
        {
            return await EnforceInitialized().GetConfigWithExposureLoggingDisabled(user, configName);
        }

        public static void ManuallyLogConfigExposure(StatsigUser user, string configName)
        {
            EnforceInitialized().LogConfigExposure(user, configName);
        }

        public static async Task<DynamicConfig> GetExperiment(StatsigUser user, string experimentName)
        {
            return await EnforceInitialized().GetExperiment(user, experimentName);
        }

        public static async Task<DynamicConfig> GetExperimentWithExposureLoggingDisabled(
            StatsigUser user,
            string experimentName
        )
        {
            return await EnforceInitialized().GetConfigWithExposureLoggingDisabled(user, experimentName);
        }

        public static void ManuallyLogExperimentExposure(StatsigUser user, string experimentName)
        {
            EnforceInitialized().LogExperimentExposure(user, experimentName);
        }

        public static async Task<Layer> GetLayer(StatsigUser user, string layerName)
        {
            return await EnforceInitialized().GetLayer(user, layerName);
        }

        public static async Task<Layer> GetLayerWithExposureLoggingDisabled(StatsigUser user, string layerName)
        {
            return await EnforceInitialized().GetLayerWithExposureLoggingDisabled(user, layerName);
        }

        public static async void ManuallyLogLayerParameterExposure(
            StatsigUser user,
            string layerName,
            string parameterName
        )
        {
            EnforceInitialized().LogLayerParameterExposure(user, layerName, parameterName);
        }

        public static Dictionary<string, object> GetClientInitializeResponse(StatsigUser user)
        {
            return EnforceInitialized().GenerateInitializeResponse(user);
        }

        public static void LogEvent(
            StatsigUser user,
            string eventName,
            string? value = null,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            EnforceInitialized().LogEvent(user, eventName, value, metadata);
        }

        public static void LogEvent(
            StatsigUser user,
            string eventName,
            int value,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            EnforceInitialized().LogEvent(user, eventName, value, metadata);
        }

        public static void LogEvent(
            StatsigUser user,
            string eventName,
            double value,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            EnforceInitialized().LogEvent(user, eventName, value, metadata);
        }

        public static List<string> GetFeatureGateList()
        {
            return EnforceInitialized().GetFeatureGateList();
        }

        public static List<string> GetExperimentList()
        {
            return EnforceInitialized().GetExperimentList();
        }

        private static ServerDriver EnforceInitialized()
        {
            if (_singleDriver == null)
            {
                throw new StatsigUninitializedException();
            }

            return _singleDriver;
        }
    }
}