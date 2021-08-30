using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;

namespace Statsig
{
    public class EventLog
    {
        private StatsigUser _user;

        [JsonProperty("eventName")]
        public string EventName { get; set; }
        [JsonProperty("user")]
        public StatsigUser User
        {
            get => _user;
            set
            {
                // C# pass by reference so we need to make a copy of user that does NOT have private attributes
                _user = value.GetCopyForLogging();
            }
        }
        [JsonProperty("metadata")]
        public IReadOnlyDictionary<string, string> Metadata { get; set; }
        [JsonProperty("value")]
        public object Value { get; set; }

        [JsonIgnore]
        internal bool IsErrorLog { get; set; }
        [JsonIgnore]
        internal string ErrorKey { get; set; }

        public EventLog()
        {
        }

        internal static EventLog CreateGateExposureLog(
            StatsigUser user,
            string gateName,
            string gateValue,
            String ruleID)
        {
            return new EventLog
            {
                User = user,
                EventName = Constants.GATE_EXPOSURE_EVENT,
                Metadata = new Dictionary<string, string>
                {
                    ["gate"] = gateName,
                    ["gateValue"] = gateValue,
                    ["ruleID"] = ruleID
                }
            };
        }

        internal static EventLog CreateConfigExposureLog(
            StatsigUser user,
            string configName,
            string ruleID)
        {
            return new EventLog
            {
                User = user,
                EventName = Constants.CONFIG_EXPOSURE_EVENT,
                Metadata = new Dictionary<string, string>
                {
                    ["config"] = configName,
                    ["ruleID"] = ruleID,
                }
            };
        }

        internal static EventLog CreateErrorLog(string eventName, string errorMessage = null)
        {
            if (errorMessage == null)
            {
                errorMessage = eventName;
            }

            return new EventLog
            {
                EventName = eventName,
                Metadata = new Dictionary<string, string>
                {
                    ["error"] = errorMessage
                },
                IsErrorLog = true,
                ErrorKey = errorMessage,
            };
        }

        internal static IReadOnlyDictionary<string, string> TrimMetadataAsNeeded(IReadOnlyDictionary<string, string> metadata = null)
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
    }
}
