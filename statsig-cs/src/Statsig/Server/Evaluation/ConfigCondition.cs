using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Statsig.src.Statsig.Server
{
    class ConfigCondition
    {
        // The type of the condition, e.g. "public", "ip-based", etc.
        internal string Type { get; }
        // The operator to be used for the condition, e.g. "any", "gt" (greater than), etc.
        internal string Operator { get; }
        // The targeting value (right-hand operand) for this condition's operation, e.g. ["iOS", "Android"] if the condition is "os_name is any of"
        internal JToken TargetValue { get; }
        // The name of the field to be used to retrieve user's value (left-hand operand) to be used for this condition's evaluation.
        // E.g. "os_name" if the condition is "os_name is any of ['iOS', 'Android']", so we know to get "os_name" from StatsigUser to check.
        internal string Field { get; }

        internal ConfigCondition(string type, JToken targetValue, string op, string field)
        {
            Type = type;
            TargetValue = targetValue;
            Operator = op;
            Field = field;
        }

        internal static ConfigCondition FromJObject(JObject jobj)
        {
            JToken type, targetValue, op, field;
            if (jobj == null || !jobj.TryGetValue("type", out type))
            {
                return null;
            }
            return new ConfigCondition(
                type.Value<string>(),
                jobj.TryGetValue("targetValue", out targetValue) ? targetValue : null,
                jobj.TryGetValue("operator", out op) ? op.Value<string>() : null,
                jobj.TryGetValue("field", out field) ? field.Value<string>() : null
            );
        }
    }
}
