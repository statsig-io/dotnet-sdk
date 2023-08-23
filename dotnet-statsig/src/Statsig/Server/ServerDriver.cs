#if NETCOREAPP3_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
#define SUPPORTS_ASYNC_DISPOSAL
#endif

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Statsig.Lib;
using Statsig.Network;
using Statsig.Server.Evaluation;
using ExposureCause = Statsig.EventLog.ExposureCause;

namespace Statsig.Server
{
    public class ServerDriver : IDisposable
#if SUPPORTS_ASYNC_DISPOSAL
        , IAsyncDisposable
#endif
    {
        readonly StatsigOptions _options;
        internal readonly string _serverSecret;
        bool _initialized;
        bool _disposed;
        RequestDispatcher _requestDispatcher;

        internal EventLogger _eventLogger;
        internal Evaluator evaluator;

        private ErrorBoundary _errorBoundary;
        private readonly string _sessionID = Guid.NewGuid().ToString();

        public ServerDriver(string serverSecret, StatsigOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(serverSecret))
            {
                throw new StatsigArgumentException("serverSecret cannot be empty.");
            }

            options ??= new StatsigOptions();

            if (!serverSecret.StartsWith("secret-"))
            {
                if (options.AdditionalHeaders.Count == 0)
                {
                    throw new StatsigArgumentException(
                        "Invalid key provided. Please check your Statsig console to get the right server key.");
                }
            }

            _serverSecret = serverSecret;
            _options = options;
            var serverOpts = _options as StatsigServerOptions ?? null;

            _errorBoundary = new ErrorBoundary(serverSecret, SDKDetails.GetServerSDKDetails());
            _errorBoundary.Swallow("Constructor", () =>
            {
                var sdkDetails = SDKDetails.GetServerSDKDetails();
                _requestDispatcher = new RequestDispatcher(_serverSecret, _options, sdkDetails, _sessionID);
                _eventLogger = new EventLogger(
                    _requestDispatcher,
                    sdkDetails,
                    serverOpts?.LoggingBufferMaxSize ?? Constants.SERVER_MAX_LOGGER_QUEUE_LENGTH,
                    serverOpts?.LoggingIntervalSeconds ?? Constants.SERVER_MAX_LOGGER_WAIT_TIME_IN_SEC,
                    Constants.SERVER_DEDUPE_INTERVAL
                );
                evaluator = new Evaluator(options, _requestDispatcher);
            });
        }

        public async Task Initialize()
        {
            await _errorBoundary.Swallow("Initialize", async () =>
            {
                await evaluator.Initialize();
                _initialized = true;
            });
        }

        public async Task Shutdown()
        {
            await _errorBoundary.Swallow("Shutdown", async () =>
            {
                await Task.WhenAll(
                    evaluator.Shutdown(),
                    _eventLogger.Shutdown());

#if SUPPORTS_ASYNC_DISPOSAL
                await ((IAsyncDisposable)this).DisposeAsync();
#else
                ((IDisposable)this).Dispose();
#endif
            });
        }

        public async Task<bool> CheckGate(StatsigUser user, string gateName)
        {
            return await _errorBoundary.Capture("CheckGate", async () =>
                {
                    var result = await CheckGateImpl(user, gateName, shouldLogExposure: true);
                    return result.Value;
                }
                , () => false);
        }

        public async Task<bool> CheckGateWithExposureLoggingDisabled(StatsigUser user, string gateName)
        {
            return await _errorBoundary.Capture("CheckGateWithExposureLoggingDisabled", async () =>
                {
                    var result = await CheckGateImpl(user, gateName, shouldLogExposure: false);
                    return result.Value;
                }
                , () => false);
        }

        public void LogGateExposure(StatsigUser user, string gateName)
        {
            _errorBoundary.Swallow("LogGateExposure", () =>
            {
                var gate = evaluator.CheckGate(user, gateName).GateValue;
                LogGateExposureImpl(user, gateName, gate, ExposureCause.Manual);
            });
        }

        public async Task<DynamicConfig> GetConfig(StatsigUser user, string configName)
        {
            return await _errorBoundary.Capture("GetConfig",
                async () => await GetConfigImpl(user, configName, shouldLogExposure: true),
                () => new DynamicConfig(configName));
        }

        public async Task<DynamicConfig> GetConfigWithExposureLoggingDisabled(StatsigUser user, string configName)
        {
            return await _errorBoundary.Capture("GetConfigWithExposureLoggingDisabled",
                async () => await GetConfigImpl(user, configName, shouldLogExposure: false),
                () => new DynamicConfig(configName));
        }

        public void LogConfigExposure(StatsigUser user, string configName)
        {
            _errorBoundary.Swallow("LogConfigExposure", () =>
            {
                var config = evaluator.GetConfig(user, configName).ConfigValue;
                LogConfigExposureImpl(user, configName, config, ExposureCause.Manual);
            });
        }

        public async Task<DynamicConfig> GetExperiment(StatsigUser user, string experimentName)
        {
            return await _errorBoundary.Capture("GetExperiment",
                async () => await GetConfigImpl(user, experimentName, shouldLogExposure: true),
                () => new DynamicConfig(experimentName));
        }

        public async Task<DynamicConfig> GetExperimentWithExposureLoggingDisabled(StatsigUser user,
            string experimentName)
        {
            return await _errorBoundary.Capture("GetExperimentWithExposureLoggingDisabled",
                async () => await GetConfigImpl(user, experimentName, shouldLogExposure: false),
                () => new DynamicConfig(experimentName));
        }

        public void LogExperimentExposure(StatsigUser user, string experimentName)
        {
            _errorBoundary.Swallow("LogExperimentExposure", () =>
            {
                var config = evaluator.GetConfig(user, experimentName).ConfigValue;
                LogConfigExposureImpl(user, experimentName, config, ExposureCause.Manual);
            });
        }

        public async Task<Layer> GetLayer(StatsigUser user, string layerName)
        {
            return await _errorBoundary.Capture("GetLayer",
                async () => await GetLayerImpl(user, layerName, shouldLogExposure: true),
                () => new Layer(layerName));
        }

        public async Task<Layer> GetLayerWithExposureLoggingDisabled(StatsigUser user, string layerName)
        {
            return await _errorBoundary.Capture("GetLayerWithExposureLoggingDisabled",
                async () => await GetLayerImpl(user, layerName, shouldLogExposure: false),
                () => new Layer(layerName));
        }

        public void LogLayerParameterExposure(StatsigUser user, string layerName, string parameterName)
        {
            _errorBoundary.Swallow("LogLayerParameterExposure", () =>
            {
                var evaluation = evaluator.GetLayer(user, layerName);
                LogLayerParameterExposureImpl(user, layerName, parameterName, evaluation, ExposureCause.Manual);
            });
        }

        public Dictionary<string, object> GenerateInitializeResponse(StatsigUser user)
        {
            return _errorBoundary.Capture("GenerateInitializeResponse", () =>
            {
                EnsureInitialized();
                ValidateUser(user);
                NormalizeUser(user);

                var allEvals = evaluator.GetAllEvaluations(user) ?? new Dictionary<string, object>();
                allEvals.Add("generator", "Dotnet Server");

                var evaluatedKeys = new Dictionary<string, object>();
                if (user.UserID != null)
                {
                    evaluatedKeys["userID"] = user.UserID;
                }

                if (user.CustomIDs.Count != 0)
                {
                    evaluatedKeys["customIDs"] = user.customIDs;
                }

                allEvals.Add("evaluated_keys", evaluatedKeys);

                return allEvals;
            }, () => new Dictionary<string, object>());
        }

        public void LogEvent(
            StatsigUser user,
            string eventName,
            string? value = null,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            _errorBoundary.Swallow("LogEvent:String", () =>
            {
                if (value != null && value.Length > Constants.MAX_SCALAR_LENGTH)
                {
                    value = value.Substring(0, Constants.MAX_SCALAR_LENGTH);
                }

                LogEventHelper(user, eventName, value, metadata);
            });
        }

        public void LogEvent(
            StatsigUser user,
            string eventName,
            int value,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            _errorBoundary.Swallow("LogEvent:Int", () => LogEventHelper(user, eventName, value, metadata));
        }

        public void LogEvent(
            StatsigUser user,
            string eventName,
            double value,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            _errorBoundary.Swallow("LogEvent:Double", () => LogEventHelper(user, eventName, value, metadata));
        }

        public List<string> GetFeatureGateList()
        {
            return _errorBoundary.Capture("GetFeatureGateList",
                () => evaluator.GetSpecNames("gate"),
                () => new List<string>()
            );
        }

        public List<string> GetExperimentList()
        {
            return _errorBoundary.Capture("GetExperimentList",
                () => evaluator.GetSpecNames("config"),
                () => new List<string>()
            );
        }

        void IDisposable.Dispose()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("ServerDriver");
            }

            // Blocking wait is gross, but there isn't much we can do better as this point as we
            // need to synchronously dispose this object
            _eventLogger.FlushEvents().Wait();
            _disposed = true;
        }

#if SUPPORTS_ASYNC_DISPOSAL
        async ValueTask IAsyncDisposable.DisposeAsync()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("ServerDriver");
            }

            await _eventLogger.FlushEvents();
            _disposed = true;
        }
#endif

        #region Private Methods

        private async Task<FeatureGate> CheckGateImpl(StatsigUser user, string gateName, bool shouldLogExposure)
        {
            EnsureInitialized();
            ValidateUser(user);
            NormalizeUser(user);
            ValidateNonEmptyArgument(gateName, "gateName");

            var evaluation = evaluator.CheckGate(user, gateName);
            if (evaluation.Result != EvaluationResult.FetchFromServer)
            {
                if (shouldLogExposure)
                {
                    LogGateExposureImpl(user, gateName, evaluation.GateValue, ExposureCause.Automatic);
                }

                return evaluation.GateValue;
            }

            var details = SDKDetails.GetServerSDKDetails();
            var response = await _requestDispatcher.Fetch("check_gate", new Dictionary<string, object>
            {
                ["user"] = user,
                ["gateName"] = gateName,
                ["statsigMetadata"] = new Dictionary<string, object>
                {
                    { "sdkType", details.SDKType },
                    { "sdkVersion", details.SDKVersion },
                    { "exposureLoggingDisabled", !shouldLogExposure }
                }
            });

            if (response == null)
            {
                return new FeatureGate(gateName);
            }

            response.TryGetValue("value", out var value);
            response.TryGetValue("rule_id", out var ruleId);

            return new FeatureGate(gateName, value?.Value<bool>() ?? false, ruleId?.Value<string>());
        }

        private void LogGateExposureImpl(StatsigUser user, string gateName, FeatureGate gate, ExposureCause cause)
        {
            _eventLogger.Enqueue(EventLog.CreateGateExposureLog(user, gateName,
                gate?.Value ?? false,
                gate?.RuleID ?? "",
                gate?.SecondaryExposures ?? new List<IReadOnlyDictionary<string, string>>(),
                cause
            ));
        }

        private async Task<DynamicConfig> GetConfigImpl(StatsigUser user, string configName, bool shouldLogExposure)
        {
            EnsureInitialized();
            ValidateUser(user);
            NormalizeUser(user);
            ValidateNonEmptyArgument(configName, "configName");

            var evaluation = evaluator.GetConfig(user, configName);
            if (evaluation.Result != EvaluationResult.FetchFromServer)
            {
                if (shouldLogExposure)
                {
                    LogConfigExposureImpl(user, configName, evaluation.ConfigValue, ExposureCause.Automatic);
                }

                return evaluation.ConfigValue;
            }

            return await FetchConfigFromServer(user, configName, shouldLogExposure);
        }

        private void LogConfigExposureImpl(StatsigUser user, string configName, DynamicConfig config,
            ExposureCause cause)
        {
            _eventLogger.Enqueue(EventLog.CreateConfigExposureLog(user, configName,
                config?.RuleID ?? "",
                config?.SecondaryExposures ?? new List<IReadOnlyDictionary<string, string>>(),
                cause
            ));
        }

        private async Task<Layer> GetLayerImpl(StatsigUser user, string layerName, bool shouldLogExposure)
        {
            EnsureInitialized();
            ValidateUser(user);
            NormalizeUser(user);
            ValidateNonEmptyArgument(layerName, "layerName");

            var evaluation = evaluator.GetLayer(user, layerName);

            void OnExposure(Layer layer, string parameterName)
            {
                if (!shouldLogExposure)
                {
                    return;
                }

                LogLayerParameterExposureImpl(user, layerName, parameterName, evaluation, ExposureCause.Automatic);
            }

            if (evaluation.Result != EvaluationResult.FetchFromServer)
            {
                var dc = evaluation.ConfigValue;
                return new Layer(layerName, dc.Value, dc.RuleID, OnExposure);
            }

            if (evaluation.ConfigDelegate == null)
            {
                return new Layer(layerName);
            }

            var config = await FetchConfigFromServer(user, evaluation.ConfigDelegate, shouldLogExposure: false);
            return new Layer(layerName, config.Value, config.RuleID, OnExposure);
        }

        private void LogLayerParameterExposureImpl(
            StatsigUser user,
            string layerName,
            string parameterName,
            ConfigEvaluation evaluation,
            ExposureCause cause
        )
        {
            var allocatedExperiment = "";
            var isExplicit = evaluation.ExplicitParameters.Contains(parameterName);
            var exposures = evaluation.UndelegatedSecondaryExposures;

            if (isExplicit)
            {
                allocatedExperiment = evaluation?.ConfigDelegate ?? "";
                exposures = evaluation?.ConfigValue.SecondaryExposures;
            }

            _eventLogger.Enqueue(
                EventLog.CreateLayerExposureLog(
                    user,
                    layerName,
                    evaluation.ConfigValue.RuleID,
                    allocatedExperiment,
                    parameterName,
                    isExplicit,
                    exposures ?? new List<IReadOnlyDictionary<string, string>>(),
                    cause)
            );
        }


        private async Task<DynamicConfig> FetchConfigFromServer(StatsigUser user, string name, bool shouldLogExposure)
        {
            var details = SDKDetails.GetServerSDKDetails();
            var response = await _requestDispatcher.Fetch("get_config", new Dictionary<string, object>
            {
                ["user"] = user,
                ["configName"] = name,
                ["statsigMetadata"] = new Dictionary<string, object>
                {
                    { "sdkType", details.SDKType },
                    { "sdkVersion", details.SDKVersion },
                    { "exposureLoggingDisabled", !shouldLogExposure }
                }
            });

            if (response == null)
            {
                return new DynamicConfig(name);
            }

            response.TryGetValue("value", out var value);
            response.TryGetValue("rule_id", out var ruleId);

            return new DynamicConfig(name, value?.ToObject<Dictionary<string, JToken>>(), ruleId?.Value<string>());
        }

        void ValidateUser(StatsigUser user)
        {
            if (user == null)
            {
                throw new StatsigArgumentNullException("user",
                    "A StatsigUser with a valid UserID must be provided for the" +
                    "server SDK to work. See https://docs.statsig.com/messages/serverRequiredUserID/ for more details.");
            }

            if ((user.UserID == null || user.UserID == "") && user.CustomIDs.Count == 0)
            {
                throw new StatsigArgumentNullException("UserID",
                    "A StatsigUser with a valid UserID or CustomID must be provided for the" +
                    "server SDK to work. See https://docs.statsig.com/messages/serverRequiredUserID/ for more details.");
            }
        }

        void NormalizeUser(StatsigUser user)
        {
            if (user != null && _options.StatsigEnvironment != null && _options.StatsigEnvironment.Values.Count > 0)
            {
                user.statsigEnvironment = _options.StatsigEnvironment.Values;
            }
        }

        void EnsureInitialized()
        {
            if (_disposed)
            {
                throw new StatsigInvalidOperationException("This object has already been shut down.");
            }

            if (!_initialized)
            {
                throw new StatsigUninitializedException();
            }
        }

        void ValidateNonEmptyArgument(string argument, string argName)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                throw new StatsigArgumentException($"{argName} cannot be empty.");
            }
        }

        void LogEventHelper(
            StatsigUser user,
            string eventName,
            object? value,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            // User can be null for logEvent
            EnsureInitialized();
            ValidateNonEmptyArgument(eventName, "eventName");
            NormalizeUser(user);

            if (eventName.Length > Constants.MAX_SCALAR_LENGTH)
            {
                eventName = eventName.Substring(0, Constants.MAX_SCALAR_LENGTH);
            }

            var eventLog = new EventLog
            {
                EventName = eventName,
                Value = value,
                User = user,
                Metadata = EventLog.TrimMetadataAsNeeded(metadata),
            };

            _eventLogger.Enqueue(eventLog);
        }

        #endregion
    }
}