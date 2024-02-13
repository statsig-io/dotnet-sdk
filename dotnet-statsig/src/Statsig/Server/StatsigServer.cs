using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Statsig.Lib;

namespace Statsig.Server
{
    public static class StatsigServer
    {
        static ServerDriver? _singleDriver;


        public static async Task<InitializeResult> Initialize(string serverSecret, StatsigOptions? options = null)
        {
            if (_singleDriver != null)
            {
                if (_singleDriver._serverSecret != serverSecret)
                {
                    System.Diagnostics.Debug.WriteLine("Cannot reinitialize SDK with a different serverSecret");
                    return InitializeResult.InvalidSDKKey;
                }
                else
                {
                    return InitializeResult.AlreadyInitialized;
                }
            }

            if (string.IsNullOrWhiteSpace(serverSecret))
            {
                System.Diagnostics.Debug.WriteLine("serverSecret cannot be empty.");
                return InitializeResult.InvalidSDKKey;
            }

            if (!serverSecret.StartsWith("secret-"))
            {
                System.Diagnostics.Debug.WriteLine("Invalid key provided. Please check your Statsig console to get the right server key.");
                return InitializeResult.InvalidSDKKey;
            }

            _singleDriver = new ServerDriver(serverSecret, options);
            return await _singleDriver.Initialize();
        }

        public static async Task Shutdown()
        {
            await EnforceInitialized().Shutdown();
            _singleDriver = null;
        }

        #region Local Overrides

        public static void OverrideGate(string gateName, bool value, string? userID = null)
        {
            EnforceInitialized().OverrideGate(gateName, value, userID);
        }

        public static void OverrideConfig(string configName, Dictionary<string, JToken> value, string? userID = null)
        {
            EnforceInitialized().OverrideConfig(configName, value, userID);
        }

        public static void OverrideLayer(string layerName, Dictionary<string, JToken> value, string? userID = null)
        {
            EnforceInitialized().OverrideLayer(layerName, value, userID);
        }

        #endregion

        #region CheckGate

        public static bool CheckGateSync(StatsigUser user, string gateName)
        {
            return EnforceInitialized().CheckGateSync(user, gateName);
        }

        public static bool CheckGateWithExposureLoggingDisabledSync(StatsigUser user, string gateName)
        {
            return EnforceInitialized().CheckGateWithExposureLoggingDisabledSync(user, gateName);
        }

        public static void ManuallyLogGateExposure(StatsigUser user, string gateName)
        {
            EnforceInitialized().LogGateExposure(user, gateName);
        }

        #endregion

        #region GetConfig

        public static DynamicConfig GetConfigSync(StatsigUser user, string configName)
        {
            return EnforceInitialized().GetConfigSync(user, configName);
        }

        public static DynamicConfig GetConfigWithExposureLoggingDisabledSync(
            StatsigUser user,
            string configName
        )
        {
            return EnforceInitialized().GetConfigWithExposureLoggingDisabledSync(user, configName);
        }

        public static void ManuallyLogConfigExposure(StatsigUser user, string configName)
        {
            EnforceInitialized().LogConfigExposure(user, configName);
        }

        #endregion

        #region GetExperiment

        public static DynamicConfig GetExperimentSync(StatsigUser user, string experimentName)
        {
            return EnforceInitialized().GetExperimentSync(user, experimentName);
        }

        public static DynamicConfig GetExperimentWithExposureLoggingDisabledSync(
            StatsigUser user,
            string experimentName
        )
        {
            return EnforceInitialized().GetExperimentWithExposureLoggingDisabledSync(user, experimentName);
        }

        public static void ManuallyLogExperimentExposure(StatsigUser user, string experimentName)
        {
            EnforceInitialized().LogExperimentExposure(user, experimentName);
        }

        #endregion

        #region GetLayer

        public static Layer GetLayerSync(StatsigUser user, string layerName)
        {
            return EnforceInitialized().GetLayerSync(user, layerName);
        }

        public static Layer GetLayerWithExposureLoggingDisabledSync(StatsigUser user, string layerName)
        {
            return EnforceInitialized().GetLayerWithExposureLoggingDisabledSync(user, layerName);
        }

        public static async void ManuallyLogLayerParameterExposure(
            StatsigUser user,
            string layerName,
            string parameterName
        )
        {
            EnforceInitialized().LogLayerParameterExposure(user, layerName, parameterName);
        }

        #endregion

        public static Dictionary<string, object> GetClientInitializeResponse(StatsigUser user, string? clientSDKKey = null, string? hash = null)
        {
            return EnforceInitialized().GenerateInitializeResponse(user, clientSDKKey, hash);
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

        #region Deprecated Async Functions

        [Obsolete("Please use CheckGateSync instead. " + ServerDriver.AsyncFuncDeprecationLink)]
        public static async Task<bool> CheckGate(StatsigUser user, string gateName)
        {
            return await EnforceInitialized().CheckGate(user, gateName);
        }

        [Obsolete(
            "Please use CheckGateWithExposureLoggingDisabledSync instead. " + ServerDriver.AsyncFuncDeprecationLink)]
        public static async Task<bool> CheckGateWithExposureLoggingDisabled(StatsigUser user, string gateName)
        {
            return await EnforceInitialized().CheckGateWithExposureLoggingDisabled(user, gateName);
        }

        [Obsolete("Please use GetConfigSync instead. " + ServerDriver.AsyncFuncDeprecationLink)]
        public static async Task<DynamicConfig> GetConfig(StatsigUser user, string configName)
        {
            return await EnforceInitialized().GetConfig(user, configName);
        }

        [Obsolete(
            "Please use GetConfigWithExposureLoggingDisabledSync instead. " + ServerDriver.AsyncFuncDeprecationLink)]
        public static async Task<DynamicConfig> GetConfigWithExposureLoggingDisabled(
            StatsigUser user,
            string configName
        )
        {
            return await EnforceInitialized().GetConfigWithExposureLoggingDisabled(user, configName);
        }

        [Obsolete("Please use GetExperimentSync instead. " + ServerDriver.AsyncFuncDeprecationLink)]
        public static async Task<DynamicConfig> GetExperiment(StatsigUser user, string experimentName)
        {
            return await EnforceInitialized().GetExperiment(user, experimentName);
        }

        [Obsolete("Please use GetExperimentWithExposureLoggingDisabledSync instead. " +
                  ServerDriver.AsyncFuncDeprecationLink)]
        public static async Task<DynamicConfig> GetExperimentWithExposureLoggingDisabled(
            StatsigUser user,
            string experimentName
        )
        {
            return await EnforceInitialized().GetExperimentWithExposureLoggingDisabled(user, experimentName);
        }

        [Obsolete("Please use GetLayerSync instead. " + ServerDriver.AsyncFuncDeprecationLink)]
        public static async Task<Layer> GetLayer(StatsigUser user, string layerName)
        {
            return await EnforceInitialized().GetLayer(user, layerName);
        }

        [Obsolete(
            "Please use GetLayerWithExposureLoggingDisabledSync instead. " + ServerDriver.AsyncFuncDeprecationLink)]
        public static async Task<Layer> GetLayerWithExposureLoggingDisabled(StatsigUser user, string layerName)
        {
            return await EnforceInitialized().GetLayerWithExposureLoggingDisabled(user, layerName);
        }

        #endregion

        #region Private

        private static ServerDriver EnforceInitialized()
        {
            if (_singleDriver == null)
            {
                throw new StatsigUninitializedException();
            }

            return _singleDriver;
        }

        #endregion
    }
}