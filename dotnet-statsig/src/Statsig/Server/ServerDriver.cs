using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Statsig.Network;
using Statsig.Server.Evaluation;

namespace Statsig.Server
{
    public class ServerDriver : IDisposable
    {
        readonly StatsigOptions _options;
        internal readonly string _serverSecret;
        bool _initialized;
        bool _disposed;
        RequestDispatcher _requestDispatcher;
        EventLogger _eventLogger;
        Evaluator _evaluator;

        public ServerDriver(string serverSecret, StatsigOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(serverSecret))
            {
                throw new ArgumentException("serverSecret cannot be empty.", "serverSecret");
            }
            if (!serverSecret.StartsWith("secret-"))
            {
                throw new ArgumentException("Invalid key provided. Please check your Statsig console to get the right server key.", "serverSecret");
            }
            if (options == null)
            {
                options = new StatsigOptions();
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
            _evaluator = new Evaluator(serverSecret, options);
        }

        public async Task Initialize()
        {
            // No op for now
            await _evaluator.Initialize();
            _initialized = true;
        }

        public void Shutdown()
        {
            ((IDisposable)this).Dispose();
        }

        public async Task<bool> CheckGate(StatsigUser user, string gateName)
        {
            EnsureInitialized();
            ValidateUser(user);
            NormalizeUser(user);
            ValidateNonEmptyArgument(gateName, "gateName");

            bool result = false;
            string ruleID = "";
            var evaluation = _evaluator.CheckGate(user, gateName);
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
                    if (response.TryGetValue("rule_id", out outVal))
                    {
                        ruleID = outVal.Value<string>();
                    }
                }
            }
            else
            {
                result = evaluation?.GateValue?.Value ?? false;
                ruleID = evaluation?.GateValue?.RuleID ?? "";
            }

            _eventLogger.Enqueue(EventLog.CreateGateExposureLog(user, gateName, result.ToString(), ruleID));
            return result;
        }

        public async Task<DynamicConfig> GetConfig(StatsigUser user, string configName)
        {
            EnsureInitialized();
            ValidateUser(user);
            NormalizeUser(user);
            ValidateNonEmptyArgument(configName, "configName");

            
            var evaluation = _evaluator.GetConfig(user, configName);
            var result = evaluation?.ConfigValue;
            if (evaluation?.Result == EvaluationResult.FetchFromServer)
            {
                var response = await _requestDispatcher.Fetch("get_config", new Dictionary<string, object>
                {
                    ["user"] = user,
                    ["configName"] = configName
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
                        result = new DynamicConfig(configName, configVal, ruleID.Value<string>());
                    }
                }
            }
            else if (evaluation == null)
            {
                result = new DynamicConfig(configName);
            }
            
            _eventLogger.Enqueue(
                EventLog.CreateConfigExposureLog(user, result.ConfigName, result.RuleID)
            );
            return result;
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

            _eventLogger.ForceFlush();
            _disposed = true;
        }

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
