using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Statsig.Lib;
using Statsig.Server;
using Statsig.Server.Evaluation;

namespace Statsig
{
    public class DynamicConfig
    {
        [JsonProperty("name")] public string ConfigName { get; }

        [JsonProperty("value")] public IReadOnlyDictionary<string, JToken> Value { get; }

        [JsonProperty("explicit_parameters")] public List<string> ExplicitParameters { get; private set; }

        [JsonProperty("rule_id")] public string RuleID { get; }
        [JsonProperty("group_name")] public string? GroupName { get; private set; }

        [JsonProperty("secondary_exposures")]
        public List<IReadOnlyDictionary<string, string>> SecondaryExposures { get; internal set; }

        [JsonProperty("is_in_layer")] public bool IsInLayer { get; private set; }

        [JsonProperty("is_user_in_experiment")]
        public bool IsUserInExperiment { get; private set; }

        public EvaluationDetails? EvaluationDetails { get; }


        static DynamicConfig? _defaultConfig;

        public static DynamicConfig Default
        {
            get
            {
                if (_defaultConfig == null)
                {
                    _defaultConfig = new DynamicConfig();
                }

                return _defaultConfig;
            }
        }

        public DynamicConfig(
            string? configName = null,
            IReadOnlyDictionary<string, JToken>? value = null,
            string? ruleID = null,
            string? groupName = null,
            List<IReadOnlyDictionary<string, string>>? secondaryExposures = null,
            List<string>? explicitParameters = null,
            bool isInLayer = false,
            bool isUserInExperiment = false,
            EvaluationDetails? details = null
        )
        {
            ConfigName = configName ?? "";
            Value = value ?? new Dictionary<string, JToken>();
            RuleID = ruleID ?? "";
            GroupName = groupName ?? null;
            SecondaryExposures = secondaryExposures ?? new List<IReadOnlyDictionary<string, string>>();
            ExplicitParameters = explicitParameters ?? new List<string>();
            IsInLayer = isInLayer;
            IsUserInExperiment = isUserInExperiment;
            EvaluationDetails = details;
        }

        public T? Get<T>(string key, T? defaultValue = default(T))
        {
            if (!this.Value.TryGetValue(key, out var outVal))
            {
                return defaultValue;
            }

            try
            {
                return outVal.ToObject<T>();
            }
            catch
            {
                // There are a bunch of different types of exceptions that could
                // be thrown at this point - missing converters, format exception
                // type cast exception, etc.
                return defaultValue;
            }
        }

        internal static DynamicConfig? FromJObject(string configName, JObject? jobj)
        {
            if (jobj == null)
            {
                return null;
            }

            try
            {
                return new DynamicConfig
                (
                    configName,
                    jobj.GetOrDefault<Dictionary<string, JToken>>("value"),
                    jobj.GetOrDefault("rule_id", ""),
                    jobj.GetOrDefault<string?>("group_name", null),
                    jobj.GetOrDefault<List<IReadOnlyDictionary<string, string>>>("secondary_exposures"),
                    jobj.GetOrDefault<List<string>>("explicit_parameters"),
                    jobj.GetOrDefault("is_in_layer", false),
                    jobj.GetOrDefault("is_user_in_experiment", false)
                );
            }
            catch
            {
                // Failed to parse config.  TODO: Log this
                return null;
            }
        }
    }
}