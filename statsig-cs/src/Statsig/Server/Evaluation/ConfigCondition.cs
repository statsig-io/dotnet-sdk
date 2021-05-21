using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Statsig.src.Statsig.Server
{
    class ConfigCondition
    {
        internal string Type { get; }
        internal JToken TargetValue { get; }
        internal string Operator { get; }
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
