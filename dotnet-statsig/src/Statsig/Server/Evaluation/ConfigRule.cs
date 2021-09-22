using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Statsig.Server
{
    class ConfigRule
    {
        internal string Name { get; }
        internal double PassPercentage { get; }
        internal string ID { get; }
        internal string Salt { get; }
        internal List<ConfigCondition> Conditions { get; }
        internal DynamicConfig DynamicConfigValue { get; }
        internal FeatureGate FeatureGateValue { get; }

        internal ConfigRule(string name, double passPercentage, JToken returnValue, string id, string salt, List<ConfigCondition> conditions)
        {
            Name = name;
            PassPercentage = passPercentage;
            Conditions = conditions;
            ID = id;
            Salt = salt;

            FeatureGateValue = new FeatureGate(name, true, id);
            DynamicConfigValue = new DynamicConfig(name, null, id);
            try
            {
                DynamicConfigValue =
                    new DynamicConfig(name, returnValue.ToObject<Dictionary<string, JToken>>(), id);
            }
            catch {}
        }

        internal static ConfigRule FromJObject(JObject jobj)
        {
            JToken name, passPercentage, returnValue, conditions, id, salt;

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
            foreach (JObject cond in conditions.ToObject<JObject[]>())
            {
                conditionsList.Add(ConfigCondition.FromJObject(cond));
            }

            return new ConfigRule(
                name.Value<string>(),
                passPercentage.Value<double>(),
                returnValue,
                id.Value<string>(),
                jobj.TryGetValue("salt", out salt) ? salt.Value<string>() : null,
                conditionsList);
        }
    }
}
