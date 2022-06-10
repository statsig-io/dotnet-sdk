#if NETCOREAPP3_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
#define SUPPORTS_ASYNC_DISPOSAL
#endif

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Statsig.Network;
using Statsig.Server.Evaluation;

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
        EventLogger _eventLogger;
        internal Evaluator evaluator;

        public ServerDriver(string serverSecret, StatsigOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(serverSecret))
            {
                throw new ArgumentException("serverSecret cannot be empty.", "serverSecret");
            }
            if (options == null)
            {
                options = new StatsigOptions();
            }
            if (!serverSecret.StartsWith("secret-"))
            {
                if (options.AdditionalHeaders.Count == 0) 
                {
                    throw new ArgumentException(
                        "Invalid key provided. Please check your Statsig console to get the right server key.", 
                        "serverSecret"
                    );
                }
            }
            _serverSecret = serverSecret;
            _options = options;

            _requestDispatcher = new RequestDispatcher(_serverSecret, _options.ApiUrlBase);
            _eventLogger = new EventLogger(
                _requestDispatcher,
                SDKDetails.GetServerSDKDetails(),
                Constants.SERVER_MAX_LOGGER_QUEUE_LENGTH,
                Constants.SERVER_MAX_LOGGER_WAIT_TIME_IN_SEC
            );
            evaluator = new Evaluator(serverSecret, options);
        }

        public async Task Initialize()
        {
            // No op for now
            await evaluator.Initialize();
            _initialized = true;
        }

        public async Task Shutdown()
        {
            await Task.WhenAll(
                evaluator.Shutdown(),
                _eventLogger.Shutdown());

#if SUPPORTS_ASYNC_DISPOSAL
            await ((IAsyncDisposable)this).DisposeAsync();
#else
            ((IDisposable)this).Dispose();
#endif
        }

        public async Task<bool> CheckGate(StatsigUser user, string gateName)
        {
            EnsureInitialized();
            ValidateUser(user);
            NormalizeUser(user);
            ValidateNonEmptyArgument(gateName, "gateName");

            bool result = false;
            var evaluation = evaluator.CheckGate(user, gateName);
            if (evaluation?.Result == EvaluationResult.FetchFromServer)
            {
                var response = await _requestDispatcher.Fetch("check_gate", new Dictionary<string, object>
                {
                    ["user"] = user,
                    ["gateName"] = gateName
                });

                if (response != null)
                {
                    JToken outVal;
                    if (response.TryGetValue("value", out outVal))
                    {
                        result = outVal.Value<bool>();
                    }
                }
            }
            else
            {
                result = evaluation?.GateValue?.Value ?? false;
                var ruleID = evaluation?.GateValue?.RuleID ?? "";
                var exposures = evaluation?.GateValue?.SecondaryExposures ?? new List<IReadOnlyDictionary<string, string>>();
                // Only log exposures for gates evaluated by the SDK itself
                _eventLogger.Enqueue(EventLog.CreateGateExposureLog(user, gateName, result, ruleID, exposures));
            }

            return result;
        }

        public async Task<DynamicConfig> GetConfig(StatsigUser user, string configName)
        {
            EnsureInitialized();
            ValidateUser(user);
            NormalizeUser(user);
            ValidateNonEmptyArgument(configName, "configName");

            var evaluation = evaluator.GetConfig(user, configName);
            var result = evaluation?.ConfigValue;
            if (evaluation == null)
            {
                result = new DynamicConfig(configName);
            }

            if (evaluation?.Result == EvaluationResult.FetchFromServer)
            {
                result = await FetchFromServer(user, configName);
            }
            else
            {
                var exposures = evaluation?.ConfigValue?.SecondaryExposures ?? new List<IReadOnlyDictionary<string, string>>();
                // Only log exposures for configs evaluated by the SDK itself
                _eventLogger.Enqueue(
                    EventLog.CreateConfigExposureLog(user, result.ConfigName, result.RuleID, exposures)
                );
            }

            return result;
        }

        public async Task<Layer> GetLayer(StatsigUser user, string layerName)
        {
            EnsureInitialized();
            ValidateUser(user);
            NormalizeUser(user);
            ValidateNonEmptyArgument(layerName, "layerName");

            var evaluation = evaluator.GetLayer(user, layerName);
            var config = evaluation?.ConfigValue;

            if (evaluation?.Result == EvaluationResult.FetchFromServer)
            {
                config = await FetchFromServer(user, evaluation?.ConfigDelegate);
            }

            return new Layer(layerName, config?.Value, config?.RuleID, delegate (Layer layer, string parameterName)
            {
                var allocatedExperiment = "";
                var isExplicit = evaluation?.ExplicitParameters?.Contains(parameterName) ?? false;
                var exposures = evaluation?.UndelegatedSecondaryExposures;

                if (isExplicit)
                {
                    allocatedExperiment = evaluation?.ConfigDelegate ?? "";
                    exposures = evaluation?.ConfigValue.SecondaryExposures;
                }

                _eventLogger.Enqueue(
                    EventLog.CreateLayerExposureLog(
                        user,
                        layer.Name,
                        layer.RuleID,
                        allocatedExperiment,
                        parameterName,
                        isExplicit,
                        exposures ?? new List<IReadOnlyDictionary<string, string>>())
                    );
            });
        }

        private async Task<DynamicConfig> FetchFromServer(StatsigUser user, string name)
        {
            var result = new DynamicConfig(name);
            var response = await _requestDispatcher.Fetch("get_config", new Dictionary<string, object>
            {
                ["user"] = user,
                ["configName"] = name
            });
            if (response != null)
            {
                JToken outVal;
                if (response.TryGetValue("value", out outVal))
                {
                    var configVal = outVal.ToObject<Dictionary<string, JToken>>();
                    JToken ruleID;
                    if (!response.TryGetValue("rule_id", out ruleID))
                    {
                        ruleID = "";
                    }
                    result = new DynamicConfig(name, configVal, ruleID.Value<string>());
                }
            }

            return result;
        }

        public Dictionary<string, object> GenerateInitializeResponse(StatsigUser user) 
        {
            EnsureInitialized();
            ValidateUser(user);
            NormalizeUser(user);

            var allEvals = evaluator.GetAllEvaluations(user);
            allEvals.Add("generator", "Dotnet Server");
            return allEvals;
        }

        public void LogEvent(
            StatsigUser user,
            string eventName,
            string value = null,
            IReadOnlyDictionary<string, string> metadata = null)
        {
            if (value != null && value.Length > Constants.MAX_SCALAR_LENGTH)
            {
                value = value.Substring(0, Constants.MAX_SCALAR_LENGTH);
            }

            LogEventHelper(user, eventName, value, metadata);
        }

        public void LogEvent(
            StatsigUser user,
            string eventName,
            int value,
            IReadOnlyDictionary<string, string> metadata = null)
        {
            LogEventHelper(user, eventName, value, metadata);
        }

        public void LogEvent(
            StatsigUser user,
            string eventName,
            double value,
            IReadOnlyDictionary<string, string> metadata = null)
        {
            LogEventHelper(user, eventName, value, metadata);
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

        #region Private helpers

        void ValidateUser(StatsigUser user)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user", "A StatsigUser with a valid UserID must be provided for the" +
                    "server SDK to work. See https://docs.statsig.com/messages/serverRequiredUserID/ for more details.");
            }
            if (user.UserID == null)
            {
                throw new ArgumentNullException("UserID", "A StatsigUser with a valid UserID must be provided for the" +
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
                throw new InvalidOperationException("This object has already been shut down.");
            }
            if (!_initialized)
            {
                throw new InvalidOperationException("Must call Initialize() first.");
            }
        }

        void ValidateNonEmptyArgument(string argument, string argName)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                throw new ArgumentException($"{argName} cannot be empty.", argName);
            }
        }

        void LogEventHelper(
            StatsigUser user,
            string eventName,
            object value,
            IReadOnlyDictionary<string, string> metadata = null)
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
