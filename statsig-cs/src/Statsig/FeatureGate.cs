using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Statsig
{
    public class FeatureGate
    {
        public string Name { get; }
        public bool Value { get; }
        public string RuleID { get; }

        static FeatureGate _defaultConfig;

        public static FeatureGate Default
        {
            get
            {
                if (_defaultConfig == null)
                {
                    _defaultConfig = new FeatureGate();
                }
                return _defaultConfig;
            }
        }

        public FeatureGate(string name = null, bool value = false, string ruleID = null)
        {
            if (name == null)
            {
                name = "";
            }
            if (ruleID == null)
            {
                ruleID = "";
            }

            Name = name;
            Value = value;
            RuleID = groupName;
        }

        internal static FeatureGate FromJObject(string name, JObject jobj)
        {
            if (jobj == null)
            {
                return null;
            }

            JToken ruleToken;
            if (!jobj.TryGetValue("rule", out ruleToken))
            {
                return null;
            }

            JToken valueToken;
            if (!jobj.TryGetValue("value", out valueToken))
            {
                return null;
            }

            JToken nameToken;
            if (!jobj.TryGetValue("name", out nameToken))
            {
                return null;
            }

            try
            {
                var value = valueToken.ToObject<Dictionary<string, JToken>>();
                return new FeatureGate(nameToken.Value<string>(), valueToken.Value<bool>(), ruleToken.Value<string>());
            }
            catch
            {
                // Failed to parse config.  TODO: Log this
                return null;
            }
        }
    }
}
