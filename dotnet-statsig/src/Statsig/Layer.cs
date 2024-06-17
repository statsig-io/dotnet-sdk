using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Statsig.Lib;
using Statsig.Server;
using Statsig.Server.Evaluation;

namespace Statsig
{
    public class Layer
    {
        [JsonProperty("name")] public string Name { get; }

        [JsonProperty("rule_id")] public string RuleID { get; }

        [JsonProperty("explicit_parameters")] public List<string> ExplicitParameters { get; private set; }

        [JsonProperty("value")] internal IReadOnlyDictionary<string, JToken> Value { get; }

        [JsonProperty("secondary_exposures")] internal List<IReadOnlyDictionary<string, string>> SecondaryExposures;

        [JsonProperty("undelegated_secondary_exposures")]
        internal List<IReadOnlyDictionary<string, string>> UndelegatedSecondaryExposures;

        [JsonProperty("allocated_experiment_name")]
        public string AllocatedExperimentName { get; }

        [JsonProperty("group_name")] public string? GroupName { get; }

        internal Action<Layer, string> OnExposure;

        static Layer? _default;

        public EvaluationDetails? EvaluationDetails { get; }

        public static Layer Default
        {
            get
            {
                if (_default == null)
                {
                    _default = new Layer();
                }

                return _default;
            }
        }

        public Layer(string? name = null,
            IReadOnlyDictionary<string, JToken>? value = null,
            string? ruleID = null,
            string? allocatedExperimentName = null,
            List<string>? explicitParameters = null,
            Action<Layer, string>? onExposure = null,
            string? groupName = null,
            EvaluationDetails? details = null)
        {
            Name = name ?? "";
            Value = value ?? new Dictionary<string, JToken>();
            RuleID = ruleID ?? "";
            OnExposure = onExposure ?? delegate { };
            SecondaryExposures = new List<IReadOnlyDictionary<string, string>>();
            UndelegatedSecondaryExposures = new List<IReadOnlyDictionary<string, string>>();
            ExplicitParameters = explicitParameters ?? new List<string>();
            AllocatedExperimentName = allocatedExperimentName ?? "";
            GroupName = groupName;
            EvaluationDetails = details;
        }

        public T? Get<T>(string key, T? defaultValue = default(T))
        {
            JToken? outVal;
            if (!this.Value.TryGetValue(key, out outVal))
            {
                return defaultValue;
            }

            try
            {
                var result = outVal.ToObject<T>();
                OnExposure(this, key);
                return result;
            }
            catch
            {
                // There are a bunch of different types of exceptions that could
                // be thrown at this point - missing converters, format exception
                // type cast exception, etc.
                return defaultValue;
            }
        }

        internal static Layer? FromJObject(string configName, JObject? jobj)
        {
            if (jobj == null)
            {
                return null;
            }

            try
            {
                var layer = new Layer
                (
                    configName,
                    JsonHelpers.GetFromJSON<Dictionary<string, JToken>>(jobj, "value",
                        new Dictionary<string, JToken>()),
                    JsonHelpers.GetFromJSON<string>(jobj, "rule_id", ""),
                    JsonHelpers.GetFromJSON(jobj, "allocated_experiment_name", ""),
                    JsonHelpers.GetFromJSON(jobj, "explicit_parameters", new List<string>()),
                    null,
                    jobj.GetOrDefault<string?>("group_name", null)
                );

                layer.SecondaryExposures = JsonHelpers.GetFromJSON(jobj, "secondary_exposures",
                    new List<IReadOnlyDictionary<string, string>>());
                layer.UndelegatedSecondaryExposures = JsonHelpers.GetFromJSON(jobj, "undelegated_secondary_exposures",
                    new List<IReadOnlyDictionary<string, string>>());

                return layer;
            }
            catch
            {
                // Failed to parse config.  TODO: Log this
                return null;
            }
        }
    }
}