using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Statsig.Server
{
    class ConfigSpec
    {
        internal string Name { get; }
        internal string Type { get; }
        internal string Salt { get; }
        internal bool Enabled { get; }
        internal string IDType { get; }
        internal string Entity { get; }
        internal List<ConfigRule> Rules { get; }
        internal DynamicConfig DynamicConfigDefault { get; }
        internal FeatureGate FeatureGateDefault { get; }
        internal List<string> ExplicitParameters { get; }

        internal ConfigSpec(string name, string type, string salt, string entity, JToken defaultValue, bool enabled, List<ConfigRule> rules, string idType, List<string> explicitParameters)
        {
            Name = name;
            Type = type;
            Salt = salt;
            Entity = entity;
            Enabled = enabled;
            Rules = rules;
            IDType = idType;
            DynamicConfigDefault = new DynamicConfig(name);
            FeatureGateDefault = new FeatureGate(name);
            ExplicitParameters = explicitParameters;

            if (Type.ToLower().Equals(Constants.DYNAMIC_CONFIG_SPEC_TYPE))
            {
                var configVal = defaultValue.ToObject<Dictionary<string, JToken>>();
                DynamicConfigDefault = new DynamicConfig(name, configVal, Constants.DEFAULT_RULE_ID);
            }
            else
            {
                FeatureGateDefault = new FeatureGate(name, false, Constants.DEFAULT_RULE_ID);
            }
        }

        internal static ConfigSpec FromJObject(JObject jobj)
        {
            JToken name, type, salt, entity, defaultValue, rules, enabled, idType, explicitParameters;

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

            return new ConfigSpec(
                name.Value<string>(),
                type.Value<string>(),
                salt.Value<string>(),
                entity.Value<string>(),
                jobj.TryGetValue("defaultValue", out defaultValue) ? defaultValue : null,
                enabled.Value<bool>(),
                rulesList,
                jobj.TryGetValue("idType", out idType) ? idType.Value<string>() : null,
                jobj.TryGetValue("explicitParameters", out explicitParameters)
                    ? explicitParameters.Values<string>().ToList()
                    : new List<string>());
        }
    }
}
