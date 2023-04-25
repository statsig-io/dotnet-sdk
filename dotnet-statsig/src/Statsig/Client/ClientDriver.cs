﻿#if NETCOREAPP3_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
#define SUPPORTS_ASYNC_DISPOSAL
# endif

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.IsolatedStorage;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Statsig.Client.Storage;
using Statsig.Network;

namespace Statsig.Client
{
    public class ClientDriver : IDisposable
#if SUPPORTS_ASYNC_DISPOSAL
        , IAsyncDisposable
#endif
    {
        const string gatesStoreKey = "statsig::featureGates";
        const string configsStoreKey = "statsig::configs";
        const string layersStoreKey = "statsig::layers";

        readonly StatsigOptions _options;
        internal readonly string _clientKey;
        bool _disposed;
        RequestDispatcher _requestDispatcher;
        internal EventLogger _eventLogger;
        StatsigUser _user;
        Dictionary<string, FeatureGate> _gates;
        Dictionary<string, DynamicConfig> _configs;
        Dictionary<string, Layer> _layers;
        Dictionary<string, string>? _statsigMetadata;

        public ClientDriver(string clientKey, StatsigOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(clientKey))
            {
                throw new ArgumentException("clientKey cannot be empty.", "clientKey");
            }
            if (!clientKey.StartsWith("client-") && !clientKey.StartsWith("test-"))
            {
                throw new ArgumentException("Invalid key provided. Please check your Statsig console to get the right server key.", "serverSecret");
            }
            if (options == null)
            {
                options = new StatsigOptions();
            }
            _clientKey = clientKey;
            _options = options;
            _requestDispatcher = new RequestDispatcher(_clientKey, _options);
            _eventLogger = new EventLogger(
                _requestDispatcher,
                SDKDetails.GetClientSDKDetails(),
                Constants.CLIENT_MAX_LOGGER_QUEUE_LENGTH,
                Constants.CLIENT_MAX_LOGGER_WAIT_TIME_IN_SEC,
                Constants.CLIENT_DEDUPE_INTERVAL
            );
            _user = new StatsigUser();

            if (options.PersistentStorageFolder != null)
            {
                PersistentStore.SetStorageFolder(options.PersistentStorageFolder);
            }
            PersistentStore.Deserialize();
            _gates = PersistentStore.GetValue(gatesStoreKey, new Dictionary<string, FeatureGate>());
            _configs = PersistentStore.GetValue(configsStoreKey, new Dictionary<string, DynamicConfig>());
            _layers = PersistentStore.GetValue(layersStoreKey, new Dictionary<string, Layer>());
        }

        public async Task Initialize(StatsigUser? user)
        {
            if (user == null)
            {
                user = new StatsigUser();
            }

            _user = user;
            _user.statsigEnvironment = _options.StatsigEnvironment.Values;
            var response = await _requestDispatcher.Fetch(
                "initialize",
                new Dictionary<string, object>
                {
                    ["user"] = _user,
                    ["statsigMetadata"] = GetStatsigMetadata(),
                },
                SDKDetails.GetClientSDKDetails().StatsigMetadata,
                timeoutInMs: _options.ClientRequestTimeoutMs
            );
            if (response == null)
            {
                return;
            }

            ParseAndSaveInitResponse(response);
        }

        public async Task Shutdown()
        {
            await _eventLogger.Shutdown();
#if SUPPORTS_ASYNC_DISPOSAL
            await ((IAsyncDisposable)this).DisposeAsync();
#else
            ((IDisposable)this).Dispose();
#endif
        }

        public bool CheckGate(string gateName)
        {
            var hashedName = GetNameHash(gateName);
            FeatureGate? gate;
            if (!_gates.TryGetValue(hashedName, out gate))
            {
                if (!_gates.TryGetValue(gateName, out gate))
                {
                    gate = new FeatureGate(gateName, false, "");
                }
            }
            _eventLogger.Enqueue(EventLog.CreateGateExposureLog(_user, gateName, gate.Value, gate.RuleID, gate.SecondaryExposures));
            return gate.Value;
        }

        public DynamicConfig GetConfig(string configName)
        {
            var hashedName = GetNameHash(configName);
            DynamicConfig? value;
            if (!_configs.TryGetValue(hashedName, out value))
            {
                if (!_configs.TryGetValue(configName, out value))
                {
                    value = new DynamicConfig(configName);
                }
            }
            _eventLogger.Enqueue(EventLog.CreateConfigExposureLog(_user, configName, value.RuleID, value.SecondaryExposures));
            return value;
        }

        public Layer GetLayer(string layerName)
        {
            var hashedName = GetNameHash(layerName);
            Layer? value;
            if (!_layers.TryGetValue(hashedName, out value))
            {
                if (!_layers.TryGetValue(layerName, out value))
                {
                    value = new Layer(layerName);
                }
            }

            value.OnExposure = delegate (Layer layer, string parameterName)
            {
                var allocatedExperiment = "";
                var isExplicit = layer.ExplicitParameters.Contains(parameterName);
                var exposures = layer.UndelegatedSecondaryExposures;

                if (isExplicit)
                {
                    allocatedExperiment = layer.AllocatedExperimentName;
                    exposures = layer.SecondaryExposures;
                }

                _eventLogger.Enqueue(
                    EventLog.CreateLayerExposureLog(
                        _user,
                        layerName,
                        layer.RuleID,
                        allocatedExperiment,
                        parameterName,
                        isExplicit,
                        exposures ?? new List<IReadOnlyDictionary<string, string>>())
                    );
            };

            return value;
        }

        public async Task UpdateUser(StatsigUser newUser)
        {
            _statsigMetadata = null;
            await Initialize(newUser);
        }

        public void LogEvent(
            string eventName,
            string? value = null,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            if (value != null && value.Length > Constants.MAX_SCALAR_LENGTH)
            {
                value = value.Substring(0, Constants.MAX_SCALAR_LENGTH);
            }

            LogEventHelper(eventName, value, metadata);
        }

        public void LogEvent(
            string eventName,
            int value,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            LogEventHelper(eventName, value, metadata);
        }

        public void LogEvent(
            string eventName,
            double value,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            LogEventHelper(eventName, value, metadata);
        }

        void IDisposable.Dispose()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("ClientDriver");
            }

            // Blocking wait is gross, but there isn't much we can do better as this point as we
            // need to synchronously dispose of this object
            _eventLogger.FlushEvents().Wait();
            _disposed = true;
        }

#if SUPPORTS_ASYNC_DISPOSAL
        async ValueTask IAsyncDisposable.DisposeAsync()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("ClientDriver");
            }

            await _eventLogger.FlushEvents();
            _disposed = true;
        }
#endif

        #region Private helpers

        void LogEventHelper(
            string eventName,
            object? value,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            if (eventName == null)
            {
                return;
            }

            if (eventName.Length > Constants.MAX_SCALAR_LENGTH)
            {
                eventName = eventName.Substring(0, Constants.MAX_SCALAR_LENGTH);
            }

            var eventLog = new EventLog
            {
                EventName = eventName,
                Value = value,
                User = _user,
                Metadata = EventLog.TrimMetadataAsNeeded(metadata),
            };

            _eventLogger.Enqueue(eventLog);
        }

        string GetNameHash(string name)
        {
            using (var sha = SHA256.Create())
            {
                var buffer = sha.ComputeHash(Encoding.UTF8.GetBytes(name));
                return Convert.ToBase64String(buffer);
            }
        }

        void ParseAndSaveInitResponse(IReadOnlyDictionary<string, JToken> response)
        {
            try
            {
                JToken? objVal;
                if (response.TryGetValue("feature_gates", out objVal))
                {
                    var gateMap = objVal.ToObject<Dictionary<string, object>>();
                    if (gateMap != null)
                    {
                        foreach (var kv in gateMap)
                        {
                            var gate = FeatureGate.FromJObject(kv.Key, kv.Value as JObject);
                            if (gate != null)
                            {
                                _gates[kv.Key] = gate;
                            }
                        }
                    }
                    PersistentStore.SetValue(gatesStoreKey, _gates);
                }
            }
            catch
            {
                // Gates parsing failed.  TODO: Log this
            }

            try
            {
                JToken? objVal;
                if (response.TryGetValue("dynamic_configs", out objVal))
                {
                    var configMap = objVal.ToObject<Dictionary<string, object>>();
                    if (configMap != null)
                    {
                        foreach (var kv in configMap)
                        {
                            var config = DynamicConfig.FromJObject(kv.Key, kv.Value as JObject);
                            if (config != null)
                            {
                                _configs[kv.Key] = config;
                            }
                        }
                    }
                    PersistentStore.SetValue(configsStoreKey, _configs);
                }
            }
            catch
            {
                // Configs parsing failed.  TODO: Log this
            }

            try
            {
                JToken? objVal;
                if (response.TryGetValue("layer_configs", out objVal))
                {
                    var configMap = objVal.ToObject<Dictionary<string, object>>();
                    if (configMap != null)
                    {
                        foreach (var kv in configMap)
                        {
                            var layer = Layer.FromJObject(kv.Key, kv.Value as JObject);
                            if (layer != null)
                            {
                                _layers[kv.Key] = layer;
                            }
                        }
                    }
                    PersistentStore.SetValue(layersStoreKey, _layers);
                }
            }
            catch
            {
                // Layers parsing failed.  TODO: Log this
            }
        }

        IReadOnlyDictionary<string, string> GetStatsigMetadata()
        {
            if (_statsigMetadata == null)
            {
                string systemName = "unknown";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    systemName = "Mac OS";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    systemName = "Windows";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    systemName = "Linux";
                }
                _statsigMetadata = new Dictionary<string, string>
                {
                    ["sessionID"] = Guid.NewGuid().ToString(),
                    ["stableID"] = PersistentStore.StableID,
                    ["locale"] = CultureInfo.CurrentUICulture.Name,
                    ["appVersion"] = Assembly.GetEntryAssembly()!.GetName()!.Version!.ToString()!,
                    ["systemVersion"] = Environment.OSVersion.Version.ToString(),
                    ["systemName"] = systemName,
                    ["sdkType"] = SDKDetails.GetClientSDKDetails().SDKType,
                    ["sdkVersion"] = SDKDetails.GetClientSDKDetails().SDKVersion,
                };
            }
            return _statsigMetadata;
        }

        #endregion
    }
}
