using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Statsig.Lib;

namespace Statsig.Server
{
    class ConfigRule
    {
        internal string Name { get; private set; }
        internal double PassPercentage { get; private set; }
        internal string ID { get; private set; }
        internal string? GroupName { get; private set; }
        internal string? Salt { get; private set; }
        internal string? IDType { get; private set; }
        internal bool IsExperimentGroup { get; private set; }
        internal List<ConfigCondition> Conditions { get; private set; }
        internal DynamicConfig DynamicConfigValue { get; private set; }
        internal FeatureGate FeatureGateValue { get; private set; }
        internal string? ConfigDelegate { get; private set; }

#pragma warning disable CS8618 // FromJObject below takes care of properties init
        ConfigRule()
        {
        }
#pragma warning restore CS8618

        void SetConfigValue(JToken returnValue)
        {
            FeatureGateValue = new FeatureGate(Name, true, ID);
            DynamicConfigValue = new DynamicConfig(Name, null, ID);

            try
            {
                DynamicConfigValue =
                    new DynamicConfig(Name, returnValue.ToObject<Dictionary<string, JToken>>(), ID);
            }
            catch
            {
            }
        }

        internal static ConfigRule? FromJObject(JObject jobj)
        {
            JToken? name,
                passPercentage,
                returnValue,
                conditions,
                id,
                salt,
                idType,
                configDelegate,
                isExperimentGroup;

            if (jobj == null ||
                !jobj.TryGetValue("name", out name) ||
                !jobj.TryGetValue("passPercentage", out passPercentage) ||
                !jobj.TryGetValue("returnValue", out returnValue) ||
                !jobj.TryGetValue("conditions", out conditions) ||
                !jobj.TryGetValue("id", out id))
            {
                return null;
            }

            var conditionsList = new List<ConfigCondition>();
            foreach (var cond in (conditions.ToObject<JObject[]>() ?? Enumerable.Empty<JObject>()))
            {
                var condition = ConfigCondition.FromJObject(cond);
                if (condition != null)
                {
                    conditionsList.Add(condition);
                }
            }

            var rule = new ConfigRule()
            {
                Name = name.Value<string>() ?? "",
                PassPercentage = passPercentage.Value<double>(),
                ID = id.Value<string>() ?? "",
                Salt = jobj.TryGetValue("salt", out salt) ? salt.Value<string>() : null,
                Conditions = conditionsList,
                IsExperimentGroup = jobj.TryGetValue("isExperimentGroup", out isExperimentGroup)
                    ? isExperimentGroup.Value<bool>()
                    : false,
                IDType = jobj.TryGetValue("idType", out idType) ? idType.Value<string>() : null,
                ConfigDelegate = jobj.TryGetValue("configDelegate", out configDelegate)
                    ? configDelegate.Value<string>()
                    : null,
                GroupName = jobj.GetOrDefault<string?>("groupName", null)
            };

            rule.SetConfigValue(returnValue);
            return rule;
        }
    }
}