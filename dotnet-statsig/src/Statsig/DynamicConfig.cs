using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Statsig
{
    public class DynamicConfig
    {
        [JsonProperty("name")]
        public string ConfigName { get; }

        [JsonProperty("value")]
        public IReadOnlyDictionary<string, JToken> Value { get; }

        [JsonProperty("rule_id")]
        public string RuleID { get; }

        static DynamicConfig _defaultConfig;

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

        public DynamicConfig(string configName = null, IReadOnlyDictionary<string, JToken> value = null, string ruleID = null)
        {
            if (configName == null)
            {
                configName = "";
            }
            if (value == null)
            {
                value = new Dictionary<string, JToken>();
            }
            if (ruleID == null)
            {
                ruleID = "";
            }

            ConfigName = configName;
            Value = value;
            RuleID = ruleID;
        }


        public T Get<T>(string key, T defaultValue = default(T))
        {
            JToken outVal = null;
            if (!this.Value.TryGetValue(key, out outVal))
            {
                return defaultValue;
            }

            try
            { 
                return outVal.Value<T>();
            }
            catch
            {
                // There are a bunch of different types of exceptions that could
                // be thrown at this point - missing converters, format exception
                // type cast exception, etc.
                return defaultValue;
            }
        }

        internal static DynamicConfig FromJObject(string configName, JObject jobj)
        {
            if (jobj == null)
            {
                return null;
            }

            JToken ruleToken;
            jobj.TryGetValue("rule_id", out ruleToken);
            
            JToken valueToken;
            jobj.TryGetValue("value", out valueToken);
            
            try
            {
                var value = valueToken == null ? null: valueToken.ToObject<Dictionary<string, JToken>>();
                return new DynamicConfig(
                    configName,
                    value,
                    ruleToken == null ? null : ruleToken.Value<string>());
            }
            catch
            {
                // Failed to parse config.  TODO: Log this
                return null;
            }
        }
    }
}
