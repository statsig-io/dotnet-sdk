﻿using System;
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
        public IReadOnlyDictionary<string, string>? Metadata { get; set; }
        [JsonProperty("value")]
        public object? Value { get; set; }
        [JsonProperty("time")]
        public double Time { get; } = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
        [JsonProperty("secondaryExposures")]
        public List<IReadOnlyDictionary<string, string>> SecondaryExposures { get; set; }

        [JsonIgnore]
        internal bool IsErrorLog { get; set; }
        [JsonIgnore]
        internal string ErrorKey { get; set; }

#pragma warning disable CS8618 // Creation helpers below take care of properties init
        internal EventLog()
        {            
        }
#pragma warning restore CS8618

        internal static EventLog CreateGateExposureLog(
            StatsigUser user,
            string gateName,
            bool gateValue,
            string ruleID,
            List<IReadOnlyDictionary<string, string>> secondaryExposures)
        {
            return new EventLog
            {
                User = user,
                EventName = Constants.GATE_EXPOSURE_EVENT,
                Metadata = new Dictionary<string, string>
                {
                    ["gate"] = gateName,
                    ["gateValue"] = gateValue ? "true" : "false",
                    ["ruleID"] = ruleID
                },
                SecondaryExposures = secondaryExposures,
            };
        }

        internal static EventLog CreateConfigExposureLog(
            StatsigUser user,
            string configName,
            string ruleID,
            List<IReadOnlyDictionary<string, string>> secondaryExposures)
        {
            return new EventLog
            {
                User = user,
                EventName = Constants.CONFIG_EXPOSURE_EVENT,
                Metadata = new Dictionary<string, string>
                {
                    ["config"] = configName,
                    ["ruleID"] = ruleID,
                },
                SecondaryExposures = secondaryExposures,
            };
        }

        internal static EventLog CreateLayerExposureLog(
            StatsigUser user,
            string layerName,
            string ruleID,
            string allocatedExperiment,
            string parameterName,
            bool isExplicitParameter,
            List<IReadOnlyDictionary<string, string>> exposures)
        {
            return new EventLog
            {
                User = user,
                EventName = Constants.LAYER_EXPOSURE_EVENT,
                Metadata = new Dictionary<string, string>
                {
                    ["config"] = layerName,
                    ["ruleID"] = ruleID,
                    ["allocatedExperiment"] = allocatedExperiment,
                    ["parameterName"] = parameterName,
                    ["isExplicitParameter"] = isExplicitParameter ? "true" : "false",
                },
                SecondaryExposures = exposures,
            };
        }

        internal static EventLog CreateErrorLog(string eventName, string? errorMessage = null)
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

        internal static IReadOnlyDictionary<string, string>? TrimMetadataAsNeeded(IReadOnlyDictionary<string, string>? metadata = null)
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

        readonly List<string> ignoredMetadataKeys = new List<string> {
            "serverTime", "configSyncTime", "initTime", "reason"
        };
        public int GetDedupeKey()
        {
            return string.Join(
                ":",
                User.GetDedupeKey(),
                EventName,
                Metadata == null ? "" : string.Join(":", Metadata?.Where(kv => !ignoredMetadataKeys.Contains(kv.Key)).Select(kv => kv.Value))
            ).GetHashCode();
        }
    }
}
