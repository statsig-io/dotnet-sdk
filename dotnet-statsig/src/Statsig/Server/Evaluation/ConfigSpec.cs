using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Statsig.Server
{
    class ConfigSpec
    {
        internal string Name { get; private set; }
        internal string Type { get; private set; }
        internal string Salt { get; private set; }
        internal bool Enabled { get; private set; }
        internal bool IsActive { get; private set; }
        internal string IDType { get; private set; }
        internal string Entity { get; private set; }
        internal List<ConfigRule> Rules { get; private set; }
        internal DynamicConfig DynamicConfigDefault { get; private set; }
        internal FeatureGate FeatureGateDefault { get; private set; }
        internal List<string> ExplicitParameters { get; private set; }

        ConfigSpec()
        {
        }

        void SetDefaultValue(JToken defaultValue)
        {
            DynamicConfigDefault = new DynamicConfig(Name);
            FeatureGateDefault = new FeatureGate(Name);
            if (Type.ToLower().Equals(Constants.DYNAMIC_CONFIG_SPEC_TYPE))
            {
                var configVal = defaultValue != null ? defaultValue.ToObject<Dictionary<string, JToken>>() : null;
                DynamicConfigDefault = new DynamicConfig(Name, configVal, Constants.DEFAULT_RULE_ID);
            }
            else
            {
                FeatureGateDefault = new FeatureGate(Name, false, Constants.DEFAULT_RULE_ID);
            }
        }

        internal static ConfigSpec FromJObject(JObject jobj)
        {
            JToken name, type, salt, entity, rules, enabled, 
                idType, explicitParameters, isActive;

            if (jobj == null ||
                !jobj.TryGetValue("name", out name) ||
                !jobj.TryGetValue("type", out type) ||
                !jobj.TryGetValue("salt", out salt) ||
                !jobj.TryGetValue("enabled", out enabled) ||
                !jobj.TryGetValue("entity", out entity))
            {
                return null;
            }

            var rulesList = new List<ConfigRule>();
            if (jobj.TryGetValue("rules", out rules))
            {
                foreach (JObject rule in rules.ToObject<JObject[]>())
                {
                    rulesList.Add(ConfigRule.FromJObject(rule));
                }
            }

            var spec = new ConfigSpec()
            {
                Name = name.Value<string>(),
                Type = type.Value<string>(),
                Salt = salt.Value<string>(),
                Entity = entity.Value<string>(),                
                Enabled = enabled.Value<bool>(),
                IsActive = jobj.TryGetValue("isActive", out isActive) ? isActive.Value<bool>() : false,
                Rules = rulesList,
                IDType = jobj.TryGetValue("idType", out idType) ? idType.Value<string>() : null,
                ExplicitParameters = jobj.TryGetValue("explicitParameters", out explicitParameters)
                    ? explicitParameters.Values<string>().ToList()
                    : new List<string>(),
            };

            JToken defaultValue = null;
            jobj.TryGetValue("defaultValue", out defaultValue);
            spec.SetDefaultValue(defaultValue);
            return spec;
        }
    }
}
