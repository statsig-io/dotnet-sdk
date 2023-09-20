using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Statsig.Lib;

namespace Statsig.Server
{
    class ConfigSpec
    {
        internal string Name { get; private set; }
        internal string Type { get; private set; }
        internal string Salt { get; private set; }
        internal bool Enabled { get; private set; }
        internal bool IsActive { get; private set; }
        internal string? IDType { get; private set; }
        internal string Entity { get; private set; }
        internal List<ConfigRule> Rules { get; private set; }
        internal DynamicConfig DynamicConfigDefault { get; private set; }
        internal FeatureGate FeatureGateDefault { get; private set; }
        internal List<string> ExplicitParameters { get; private set; }
        internal bool HasSharedParams { get; private set; }
        internal string[]? TargetAppIDs { get; private set; }

#pragma warning disable CS8618 // FromJObject below takes care of properties init
        ConfigSpec()
        {
        }
#pragma warning restore CS8618

        void SetDefaultValue(JToken? defaultValue)
        {
            DynamicConfigDefault = new DynamicConfig(Name);
            FeatureGateDefault = new FeatureGate(Name);
            if (Type.ToLower().Equals(Constants.DYNAMIC_CONFIG_SPEC_TYPE))
            {
                var configVal = defaultValue != null ? defaultValue.ToObject<Dictionary<string, JToken>>() : null;
                DynamicConfigDefault = new DynamicConfig(Name, configVal, Constants.DEFAULT_RULE_ID, null, null,
                    ExplicitParameters, HasSharedParams);
            }
            else
            {
                FeatureGateDefault = new FeatureGate(Name, false, Constants.DEFAULT_RULE_ID);
            }
        }

        internal static ConfigSpec? FromJObject(JObject jobj)
        {
            JToken? name,
                type,
                salt,
                entity,
                rules,
                enabled,
                idType,
                explicitParameters,
                isActive,
                targetAppIDs;

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
                foreach (JObject rule in rules.ToObject<JObject[]>() ?? Enumerable.Empty<JObject>())
                {
                    var configRule = ConfigRule.FromJObject(rule);
                    if (configRule != null)
                    {
                        rulesList.Add(configRule);
                    }
                }
            }

            var explicitParamsList = new List<string>();
            if (jobj.TryGetValue("explicitParameters", out explicitParameters))
            {
                var nonNullable = explicitParameters.Values<string>().Where(s => s != null).Select(s => s!);
                explicitParamsList = new List<string>(nonNullable);
            }

            JToken? hasSharedParams;
            jobj.TryGetValue("hasSharedParams", out hasSharedParams);
            var spec = new ConfigSpec()
            {
                Name = name.Value<string>() ?? "",
                Type = type.Value<string>() ?? "",
                Salt = salt.Value<string>() ?? "",
                Entity = entity.Value<string>() ?? "",
                Enabled = enabled.Value<bool>(),
                IsActive = jobj.TryGetValue("isActive", out isActive) ? isActive.Value<bool>() : false,
                Rules = rulesList,
                IDType = jobj.TryGetValue("idType", out idType) ? idType.Value<string>() : null,
                ExplicitParameters = explicitParamsList,
                HasSharedParams = hasSharedParams?.Value<bool>() ?? false,
                TargetAppIDs = jobj.GetOrDefault<string[]?>("targetAppIDs", null),
            };

            jobj.TryGetValue("defaultValue", out JToken? defaultValue);
            spec.SetDefaultValue(defaultValue);
            return spec;
        }
    }
}