using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Statsig.Server;

namespace Statsig
{
    public class DynamicConfig
    {
        [JsonProperty("name")] public string ConfigName { get; }

        [JsonProperty("value")] public IReadOnlyDictionary<string, JToken> Value { get; }

        [JsonProperty("explicit_parameters")] public List<string> ExplicitParameters { get; private set; }

        [JsonProperty("rule_id")] public string RuleID { get; }

        [JsonProperty("secondary_exposures")]
        public List<IReadOnlyDictionary<string, string>> SecondaryExposures { get; internal set; }

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
            List<IReadOnlyDictionary<string, string>>? secondaryExposures = null,
            List<string>? explicitParameters = null
        )
        {
            ConfigName = configName ?? "";
            Value = value ?? new Dictionary<string, JToken>();
            RuleID = ruleID ?? "";
            SecondaryExposures = secondaryExposures ?? new List<IReadOnlyDictionary<string, string>>();
            ExplicitParameters = explicitParameters ?? new List<string>();
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

            jobj.TryGetValue("rule_id", out JToken? ruleToken);
            jobj.TryGetValue("value", out JToken? valueToken);

            try
            {
                var value = valueToken == null ? null : valueToken.ToObject<Dictionary<string, JToken>>();
                var config = new DynamicConfig
                (
                    configName,
                    value,
                    ruleToken == null ? null : ruleToken.Value<string>(),
                    jobj.TryGetValue("secondary_exposures", out JToken? exposures)
                        ? exposures.ToObject<List<IReadOnlyDictionary<string, string>>>()
                        : new List<IReadOnlyDictionary<string, string>>(),
                    JsonHelpers.GetFromJSON(jobj, "explicit_parameters", new List<string>())
                );

                return config;
            }
            catch
            {
                // Failed to parse config.  TODO: Log this
                return null;
            }
        }
    }
}