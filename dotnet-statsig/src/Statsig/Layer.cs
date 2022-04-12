using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Statsig
{
    public class Layer
    {
        [JsonProperty("name")]
        public string Name { get; }

        [JsonProperty("rule_id")]
        public string RuleID { get; }

        [JsonProperty("value")]
        internal IReadOnlyDictionary<string, JToken> Value { get; }

        private Action<Layer, string> OnExposure;

        static Layer _default;

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

        public Layer(string name = null, IReadOnlyDictionary<string, JToken> value = null, string ruleID = null, Action<Layer, string> onExposure = null)
        {
            Name = name ?? "";
            Value = value ?? new Dictionary<string, JToken>();
            RuleID = ruleID ?? "";
            OnExposure = onExposure ?? delegate { };
        }

        public T Get<T>(string key, T defaultValue = default(T))
        {
            JToken outVal;
            if (!this.Value.TryGetValue(key, out outVal))
            {
                return defaultValue;
            }

            try
            {
                var result = outVal.Value<T>();
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

        internal static Layer FromJObject(string configName, JObject jobj)
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
                var value = valueToken == null ? null : valueToken.ToObject<Dictionary<string, JToken>>();
                return new Layer
                (
                    configName,
                    value,
                    ruleToken == null ? null : ruleToken.Value<string>()
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
