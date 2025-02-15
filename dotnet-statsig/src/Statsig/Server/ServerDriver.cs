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
        private readonly StatsigOptions _options;
        private readonly StatsigServerOptions? _serverOpts;
        internal readonly string _serverSecret;
        private bool _initialized;
        private bool _disposed;
        private RequestDispatcher _requestDispatcher;

        internal EventLogger _eventLogger;
        internal Evaluator evaluator;

        public ErrorBoundary _errorBoundary;
        private readonly string _sessionID = Guid.NewGuid().ToString();

        public ServerDriver(string serverSecret, StatsigOptions? options = null)
        {
            options ??= new StatsigServerOptions();

            _serverSecret = serverSecret;
            _options = options;
            _serverOpts = _options as StatsigServerOptions ?? null;

            _errorBoundary = new ErrorBoundary(serverSecret, SDKDetails.GetServerSDKDetails());
            _errorBoundary.Swallow("Constructor", () =>
            {
                var sdkDetails = SDKDetails.GetServerSDKDetails();
                _requestDispatcher = new RequestDispatcher(_serverSecret, _options, sdkDetails, _sessionID);
                _eventLogger = new EventLogger(
                    _requestDispatcher,
                    sdkDetails,
                    _serverOpts?.LoggingBufferMaxSize ?? Constants.SERVER_MAX_LOGGER_QUEUE_LENGTH,
                    _serverOpts?.LoggingIntervalSeconds ?? Constants.SERVER_MAX_LOGGER_WAIT_TIME_IN_SEC,
                    _errorBoundary,
                    Constants.SERVER_DEDUPE_INTERVAL
                );
                evaluator = new Evaluator(options, _requestDispatcher, serverSecret, _errorBoundary);
            });
        }

        public async Task<InitializeResult> Initialize()
        {
            return await _errorBoundary.Capture("Initialize", async () =>
            {
                var result = await evaluator.Initialize().ConfigureAwait(false);
                _initialized = true;
                return result;
            }, () => { return InitializeResult.Failure; }).ConfigureAwait(false);
        }

        public async Task Shutdown()
        {
            await _errorBoundary.Swallow("Shutdown", async () =>
            {
                await Task.WhenAll(
                    evaluator.Shutdown(),
                    _eventLogger.Shutdown()).ConfigureAwait(false);

#if SUPPORTS_ASYNC_DISPOSAL
                await ((IAsyncDisposable)this).DisposeAsync().ConfigureAwait(false);
#else
                ((IDisposable)this).Dispose();
#endif
            }).ConfigureAwait(false);
        }

        #region Local Overrides

        public void OverrideGate(string gateName, bool value, string? userID = null)
        {
            _errorBoundary.Swallow("OverrideGate", () =>
            {
                evaluator.OverrideGate(gateName, value, userID);
            });
        }

        public void OverrideConfig(string configName, Dictionary<string, JToken> value, string? userID = null)
        {
            _errorBoundary.Swallow("OverrideConfig", () =>
            {
                evaluator.OverrideConfig(configName, value, userID);
            });
        }

        public void OverrideLayer(string layerName, Dictionary<string, JToken> value, string? userID = null)
        {
            _errorBoundary.Swallow("OverrideLayer", () =>
            {
                evaluator.OverrideLayer(layerName, value, userID);
            });
        }

        #endregion

        #region CheckGate

        public bool CheckGateSync(StatsigUser user, string gateName)
        {
            return _errorBoundary.Capture(
                "CheckGateSync",
                () => CheckGateImpl(user, gateName, shouldLogExposure: true).Value,
                () => false
            );
        }

        public bool CheckGateWithExposureLoggingDisabledSync(StatsigUser user, string gateName)
        {
            return _errorBoundary.Capture(
                "CheckGateWithExposureLoggingDisabledSync",
                () => CheckGateImpl(user, gateName, shouldLogExposure: false).Value,
                () => false
            );
        }

        public FeatureGate GetFeatureGate(StatsigUser user, string gateName)
        {
            return _errorBoundary.Capture(
                "GetFeatureGate",
                () => CheckGateImpl(user, gateName, shouldLogExposure: true),
                () => new FeatureGate(gateName)
            );
        }

        public FeatureGate GetFeatureGateWithExposureLoggingDisabled(StatsigUser user, string gateName)
        {
            return _errorBoundary.Capture(
                "GetFeatureGateWithExposureLoggingDisabled",
                () => CheckGateImpl(user, gateName, shouldLogExposure: false),
                () => new FeatureGate(gateName)
            );
        }

        public void LogGateExposure(StatsigUser user, string gateName)
        {
            _errorBoundary.Swallow("LogGateExposure", () =>
            {
                NormalizeUser(user);
                var evaluation = evaluator.CheckGate(user, gateName);
                var gate = evaluation.GateValue;
                LogGateExposureImpl(user, gateName, gate, ExposureCause.Manual, evaluation.Reason);
            });
        }

        #endregion


        #region GetConfig

        public DynamicConfig GetConfigSync(StatsigUser user, string configName)
        {
            return _errorBoundary.Capture(
                "GetConfigSync",
                () => GetConfigImpl(user, configName, shouldLogExposure: true, null),
                () => new DynamicConfig(configName)
            );
        }

        public DynamicConfig GetConfigWithExposureLoggingDisabledSync(StatsigUser user, string configName)
        {
            return _errorBoundary.Capture(
                "GetConfigWithExposureLoggingDisabledSync",
                () => GetConfigImpl(user, configName, shouldLogExposure: false, null),
                () => new DynamicConfig(configName)
            );
        }

        public void LogConfigExposure(StatsigUser user, string configName)
        {
            _errorBoundary.Swallow("LogConfigExposure", () =>
            {
                NormalizeUser(user);
                var evaluation = evaluator.GetConfig(user, configName, null);
                var config = evaluation.ConfigValue;
                LogConfigExposureImpl(user, configName, config, ExposureCause.Manual, evaluation.Reason, evaluation.GateValue.Value);
            });
        }

        #endregion


        #region GetExperiment

        public DynamicConfig GetExperimentSync(StatsigUser user, string experimentName, Dictionary<string, StickyValue>? userPersistedValues = null)
        {
            return _errorBoundary.Capture(
                "GetExperimentSync",
                () => GetConfigImpl(user, experimentName, shouldLogExposure: true, userPersistedValues),
                () => new DynamicConfig(experimentName)
            );
        }

        public DynamicConfig GetExperimentWithExposureLoggingDisabledSync(
            StatsigUser user,
            string experimentName,
            Dictionary<string, StickyValue>? userPersistedValues = null
        )
        {
            return _errorBoundary.Capture(
                "GetExperimentWithExposureLoggingDisabledSync",
                () => GetConfigImpl(user, experimentName, shouldLogExposure: false, userPersistedValues),
                () => new DynamicConfig(experimentName)
            );
        }

        public void LogExperimentExposure(StatsigUser user, string experimentName, Dictionary<string, StickyValue>? userPersistedValues = null)
        {
            _errorBoundary.Swallow("LogExperimentExposure", () =>
            {
                NormalizeUser(user);
                var evaluation = evaluator.GetConfig(user, experimentName, userPersistedValues);
                var config = evaluation.ConfigValue;
                LogConfigExposureImpl(user, experimentName, config, ExposureCause.Manual, evaluation.Reason, evaluation.GateValue.Value);
            });
        }

        #endregion


        #region GetLayer

        public Layer GetLayerSync(StatsigUser user, string layerName, Dictionary<string, StickyValue>? userPersistedValues = null)
        {
            return _errorBoundary.Capture(
                "GetLayerSync",
                () => GetLayerImpl(user, layerName, shouldLogExposure: true, userPersistedValues),
                () => new Layer(layerName)
            );
        }

        public Layer GetLayerWithExposureLoggingDisabledSync(StatsigUser user, string layerName, Dictionary<string, StickyValue>? userPersistedValues = null)
        {
            return _errorBoundary.Capture(
                "GetLayerWithExposureLoggingDisabledSync",
                () => GetLayerImpl(user, layerName, shouldLogExposure: false, userPersistedValues),
                () => new Layer(layerName)
            );
        }

        public void LogLayerParameterExposure(StatsigUser user, string layerName, string parameterName, Dictionary<string, StickyValue>? userPersistedValues = null)
        {
            _errorBoundary.Swallow("LogLayerParameterExposure", () =>
            {
                NormalizeUser(user);
                var evaluation = evaluator.GetLayer(user, layerName, userPersistedValues);
                LogLayerParameterExposureImpl(user, layerName, parameterName, evaluation, ExposureCause.Manual);
            });
        }

        #endregion

        public Dictionary<string, object> GenerateInitializeResponse(StatsigUser user, string? clientSDKKey = null, string? hash = null, bool includeLocalOverrides = false)
        {
            return _errorBoundary.Capture("GenerateInitializeResponse", () =>
            {
                EnsureInitialized();
                var userIsValid = ValidateUser(user);
                if (!userIsValid)
                {
                    return new Dictionary<string, object>();
                }
                NormalizeUser(user);

                var allEvals = evaluator.GetAllEvaluations(user, clientSDKKey, hash, includeLocalOverrides) ?? new Dictionary<string, object>();
                if (!allEvals.ContainsKey("feature_gates"))
                {
                    var extra = new Dictionary<string, object>();
                    if (clientSDKKey != null)
                    {
                        extra["clientSDKKey"] = clientSDKKey;
                    }
                    if (hash != null)
                    {
                        extra["hash"] = hash;
                    }
                    _errorBoundary.LogException("getClientInitializeResponse", new Exception("Failed to generate initialize response"), extra);
                }
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

        public async Task<Dictionary<string, StickyValue>?> GetUserPersistedValues(StatsigUser user, string idType)
        {
            return await evaluator.GetUserPersistedValues(user, idType);
        }

        void IDisposable.Dispose()
        {
            if (_disposed)
            {
                System.Diagnostics.Debug.WriteLine("ServerDriver has already been shut down.");
                return;
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
                System.Diagnostics.Debug.WriteLine("ServerDriver has already been shut down.");
                return;
            }

            await _eventLogger.FlushEvents().ConfigureAwait(false);
            _disposed = true;
        }
#endif

        #region Deprecated Async Methods

        internal const string AsyncFuncDeprecationLink = "See https://docs.statsig.com/server/deprecation-notices";

        [Obsolete("Please use CheckGateSync instead. " + AsyncFuncDeprecationLink)]
        public Task<bool> CheckGate(StatsigUser user, string gateName)
        {
            return Task.FromResult(CheckGateSync(user, gateName));
        }

        [Obsolete("Please use CheckGateWithExposureLoggingDisabledSync instead. " + AsyncFuncDeprecationLink)]
        public Task<bool> CheckGateWithExposureLoggingDisabled(StatsigUser user, string gateName)
        {
            return Task.FromResult(CheckGateWithExposureLoggingDisabledSync(user, gateName));
        }

        [Obsolete("Please use GetConfigSync instead. " + AsyncFuncDeprecationLink)]
        public Task<DynamicConfig> GetConfig(StatsigUser user, string configName)
        {
            return Task.FromResult(GetConfigSync(user, configName));
        }

        [Obsolete("Please use GetConfigWithExposureLoggingDisabledSync instead. " + AsyncFuncDeprecationLink)]
        public Task<DynamicConfig> GetConfigWithExposureLoggingDisabled(StatsigUser user, string configName)
        {
            return Task.FromResult(GetConfigWithExposureLoggingDisabledSync(user, configName));
        }

        [Obsolete("Please use GetExperimentSync instead. " + AsyncFuncDeprecationLink)]
        public Task<DynamicConfig> GetExperiment(StatsigUser user, string experimentName)
        {
            return Task.FromResult(GetExperimentSync(user, experimentName, null));
        }

        [Obsolete("Please use GetExperimentWithExposureLoggingDisabledSync instead. " + AsyncFuncDeprecationLink)]
        public Task<DynamicConfig> GetExperimentWithExposureLoggingDisabled(
            StatsigUser user,
            string experimentName
        )
        {
            return Task.FromResult(GetExperimentWithExposureLoggingDisabledSync(user, experimentName, null));
        }

        [Obsolete("Please use GetLayerSync instead. " + AsyncFuncDeprecationLink)]
        public Task<Layer> GetLayer(StatsigUser user, string layerName)
        {
            return Task.FromResult(GetLayerSync(user, layerName, null));
        }

        [Obsolete("Please use GetLayerWithExposureLoggingDisabledSync instead. " + AsyncFuncDeprecationLink)]
        public Task<Layer> GetLayerWithExposureLoggingDisabled(StatsigUser user, string layerName)
        {
            return Task.FromResult(GetLayerWithExposureLoggingDisabledSync(user, layerName, null));
        }

        #endregion

        #region Private Methods

        private FeatureGate CheckGateImpl(StatsigUser user, string gateName, bool shouldLogExposure)
        {
            var isInitialized = EnsureInitialized();
            if (!isInitialized)
            {
                return new FeatureGate(gateName, false, null, null, EvaluationReason.Uninitialized, evaluator.GetEvaluationDetails(EvaluationReason.Uninitialized));
            }
            var userIsValid = ValidateUser(user);
            if (!userIsValid)
            {
                return new FeatureGate(gateName, false, null, null, EvaluationReason.Error, evaluator.GetEvaluationDetails(EvaluationReason.Error));
            }
            NormalizeUser(user);
            var nameValid = ValidateNonEmptyArgument(gateName, "gateName");
            if (!nameValid)
            {
                return new FeatureGate(gateName, false, null, null, EvaluationReason.Error, evaluator.GetEvaluationDetails(EvaluationReason.Error));
            }

            var evaluation = evaluator.CheckGate(user, gateName);

            if (evaluation.Result == EvaluationResult.Unsupported)
            {
                var gateValue = new FeatureGate(gateName, false, null, null, EvaluationReason.Unsupported, evaluator.GetEvaluationDetails(EvaluationReason.Unsupported));
                if (shouldLogExposure)
                {
                    LogGateExposureImpl(user, gateName, gateValue, ExposureCause.Automatic, evaluation.Reason);
                }
                return gateValue;
            }

            if (evaluation.Reason == EvaluationReason.Unrecognized || evaluation.Reason == EvaluationReason.Uninitialized)
            {
                var gateValue = new FeatureGate(gateName, false, null, null, EvaluationReason.Unrecognized, evaluator.GetEvaluationDetails(evaluation.Reason));
                if (shouldLogExposure)
                {
                    LogGateExposureImpl(user, gateName, gateValue, ExposureCause.Automatic, evaluation.Reason);
                }
                return gateValue;
            }

            if (shouldLogExposure)
            {
                LogGateExposureImpl(user, gateName, evaluation.GateValue, ExposureCause.Automatic, evaluation.Reason);
            }

            return evaluation.GateValue;
        }

        private void LogGateExposureImpl(StatsigUser user, string gateName, FeatureGate gate, ExposureCause cause, string reason)
        {
            _eventLogger.Enqueue(EventLog.CreateGateExposureLog(user, gateName,
                gate?.Value ?? false,
                gate?.RuleID ?? "",
                gate?.SecondaryExposures ?? new List<IReadOnlyDictionary<string, string>>(),
                cause,
                reason.ToString()
            ));
        }

        private DynamicConfig GetConfigImpl(StatsigUser user, string configName, bool shouldLogExposure, Dictionary<string, StickyValue>? userPersistedValues)
        {
            var isInitialized = EnsureInitialized();
            if (!isInitialized)
            {
                return new DynamicConfig(configName, null, null, null, null, null, false, false, evaluator.GetEvaluationDetails(EvaluationReason.Uninitialized));
            }
            var userIsValid = ValidateUser(user);
            if (!userIsValid)
            {
                return new DynamicConfig(configName, null, null, null, null, null, false, false, evaluator.GetEvaluationDetails(EvaluationReason.Error));
            }
            NormalizeUser(user);
            var nameValid = ValidateNonEmptyArgument(configName, "configName");
            if (!nameValid)
            {
                return new DynamicConfig(configName, null, null, null, null, null, false, false, evaluator.GetEvaluationDetails(EvaluationReason.Error));
            }


            var evaluation = evaluator.GetConfig(user, configName, userPersistedValues);

            if (evaluation.Result == EvaluationResult.Unsupported)
            {
                var configValue = new DynamicConfig(configName, null, null, null, null, null, false, false, evaluator.GetEvaluationDetails(EvaluationReason.Unsupported));
                if (shouldLogExposure)
                {
                    LogConfigExposureImpl(user, configName, configValue, ExposureCause.Automatic, evaluation.Reason, evaluation.GateValue.Value);
                }
                return configValue;
            }

            if (evaluation.Reason == EvaluationReason.Unrecognized || evaluation.Reason == EvaluationReason.Uninitialized)
            {
                var configValue = new DynamicConfig(configName, null, null, null, null, null, false, false, evaluator.GetEvaluationDetails(evaluation.Reason));
                if (shouldLogExposure)
                {
                    LogConfigExposureImpl(user, configName, configValue, ExposureCause.Automatic, evaluation.Reason, evaluation.GateValue.Value);
                }
                return configValue;
            }

            if (shouldLogExposure)
            {
                LogConfigExposureImpl(user, configName, evaluation.ConfigValue, ExposureCause.Automatic, evaluation.Reason, evaluation.GateValue.Value);
            }

            return evaluation.ConfigValue;
        }

        private void LogConfigExposureImpl(StatsigUser user, string configName, DynamicConfig config,
            ExposureCause cause, string reason, bool rulePassed)
        {
            _eventLogger.Enqueue(EventLog.CreateConfigExposureLog(user, configName,
                config?.RuleID ?? "",
                config?.SecondaryExposures ?? new List<IReadOnlyDictionary<string, string>>(),
                cause,
                reason.ToString(),
                rulePassed
            ));
        }

        private Layer GetLayerImpl(StatsigUser user, string layerName, bool shouldLogExposure, Dictionary<string, StickyValue>? userPersistedValues)
        {
            EnsureInitialized();
            var userIsValid = ValidateUser(user);
            if (!userIsValid)
            {
                return new Layer(layerName, null, null, null, null, null, null, evaluator.GetEvaluationDetails(EvaluationReason.Error));
            }
            NormalizeUser(user);
            var nameIsValid = ValidateNonEmptyArgument(layerName, "layerName");
            if (!nameIsValid)
            {
                return new Layer(layerName, null, null, null, null, null, null, evaluator.GetEvaluationDetails(EvaluationReason.Error));
            }

            var evaluation = evaluator.GetLayer(user, layerName, userPersistedValues);

            if (evaluation.Result == EvaluationResult.Unsupported)
            {
                return new Layer(layerName, null, null, null, null, null, null, evaluator.GetEvaluationDetails(EvaluationReason.Unsupported));
            }

            void OnExposure(Layer layer, string parameterName)
            {
                if (!shouldLogExposure)
                {
                    return;
                }

                LogLayerParameterExposureImpl(user, layerName, parameterName, evaluation, ExposureCause.Automatic);
            }

            var dc = evaluation.ConfigValue;
            return new Layer(layerName, dc.Value, dc.RuleID, evaluation.ConfigDelegate, evaluation.ExplicitParameters, OnExposure, dc.GroupName, evaluator.GetEvaluationDetails(evaluation.Reason));
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
                    cause,
                    evaluation.Reason.ToString())
            );
        }

        private bool ValidateUser(StatsigUser user)
        {
            if (user == null)
            {
                System.Diagnostics.Debug.WriteLine("A StatsigUser with a valid UserID or CustomID must be provided for the" +
                    "server SDK to work. See https://docs.statsig.com/messages/serverRequiredUserID/ for more details.");
                return false;
            }

            if ((user.UserID == null || user.UserID == "") && user.CustomIDs.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("A StatsigUser with a valid UserID or CustomID must be provided for the" +
                    "server SDK to work. See https://docs.statsig.com/messages/serverRequiredUserID/ for more details.");
                return false;
            }
            return true;
        }

        private void NormalizeUser(StatsigUser user)
        {
            var customEnv = _serverOpts?.CustomEnvironment;
            if (user != null && customEnv != null)
            {
                user.SetEnvironment(customEnv);
                return;
            }
            if (user != null && _options.StatsigEnvironment != null && _options.StatsigEnvironment.Values.Count > 0)
            {
                user.statsigEnvironment = _options.StatsigEnvironment.Values;
            }
        }

        private bool EnsureInitialized()
        {
            if (_disposed)
            {
                System.Diagnostics.Debug.WriteLine("Statsig SDK has already been shutdown.");
                return false;
            }

            if (!_initialized)
            {
                System.Diagnostics.Debug.WriteLine("Statsig SDK has not been initialized yet.");
                return false;
            }
            return true;
        }

        private bool ValidateNonEmptyArgument(string argument, string argName)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                System.Diagnostics.Debug.WriteLine($"{argName} cannot be empty.");
                return false;
            }
            return true;
        }

        private void LogEventHelper(
            StatsigUser user,
            string eventName,
            object? value,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            // User can be null for logEvent
            EnsureInitialized();
            var nameIsValid = ValidateNonEmptyArgument(eventName, "eventName");
            if (!nameIsValid)
            {
                return;
            }
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