using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Statsig.Network;

namespace Statsig.Server
{
    public class ServerDriver : IDisposable
    {
        readonly ConnectionOptions _options;
        internal readonly string _serverSecret;
        bool _initialized;
        bool _disposed;
        RequestDispatcher _requestDispatcher;
        EventLogger _eventLogger;

        public ServerDriver(string serverSecret, ConnectionOptions options = null)
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
                options = new ConnectionOptions();
            }
            _serverSecret = serverSecret;
            _options = options;

            _requestDispatcher = new RequestDispatcher(_serverSecret, options.ApiUrlBase);
            _eventLogger = new EventLogger(
                _requestDispatcher,
                Constants.SERVER_MAX_LOGGER_QUEUE_LENGTH,
                Constants.SERVER_MAX_LOGGER_WAIT_TIME_IN_SEC
            );
        }

        public void Initialize()
        {
            // No op for now
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
            ValidateNonEmptyArgument(gateName, "gateName");

            bool result = false;
            var response = await _requestDispatcher.Fetch("check_gate", new Dictionary<string, object> {
                ["user"] = user,
                ["gateName"] = gateName
            });
            if (response != null)
            {
                object outVal;
                if (response.TryGetValue("value", out outVal))
                {
                    bool.TryParse(outVal.ToString(), out result);
                }
            }

            _eventLogger.Enqueue(EventLog.CreateGateExposureLog(user, gateName, result.ToString()));
            return result;
        }

        public async Task<DynamicConfig> GetConfig(StatsigUser user, string configName)
        {
            EnsureInitialized();
            ValidateUser(user);
            ValidateNonEmptyArgument(configName, "configName");

            var result = new DynamicConfig(configName);
            var response = await _requestDispatcher.Fetch("get_config", new Dictionary<string, object>
            {
                ["user"] = user,
                ["configName"] = configName
            });
            if (response != null)
            {
                object outVal;
                if (response.TryGetValue("value", out outVal))
                {
                    if (outVal is JObject)
                    {
                        var configVal = ((JObject)outVal).ToObject<Dictionary<string, object>>();
                        object groupName = null;
                        if (!response.TryGetValue("group", out groupName))
                        {
                            groupName = "";
                        }
                        result = new DynamicConfig(configName, configVal, groupName.ToString());
                    }
                }
            }

            _eventLogger.Enqueue(EventLog.CreateConfigExposureLog(user, result.ConfigName, result.GroupName));
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
                throw new ArgumentNullException("user");
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
            
            if (eventName.Length > Constants.MAX_SCALAR_LENGTH)
            {
                eventName = eventName.Substring(0, Constants.MAX_SCALAR_LENGTH);
            }

            var eventLog = new EventLog
            {
                EventName = eventName,
                Value = value,
                User = user,
                Metadata = TrimMetadataAsNeeded(metadata),
            };

            _eventLogger.Enqueue(eventLog);
        }

        IReadOnlyDictionary<string, string> TrimMetadataAsNeeded(IReadOnlyDictionary<string, string> metadata = null)
        {
            if (metadata == null)
            {
                return null;
            }

            int totalLength = metadata.Sum((kv) => kv.Key.Length + (kv.Value == null ? 0 : kv.Value.Length));
            if (totalLength > Constants.MAX_METADATA_LENGTH)
            {
                Debug.WriteLine("Metadata in LogEvent is too big, dropping it.", "warning");
                return null;
            }

            return metadata;
        }

        #endregion
    }
}
