using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Statsig.Server.Evaluation;

namespace Statsig
{
    public class FeatureGate
    {
        [JsonProperty("name")]
        public string Name { get; }
        [JsonProperty("value")]
        public bool Value { get; }
        [JsonProperty("rule_id")]
        public string RuleID { get; }
        [JsonProperty("secondary_exposures")]
        public List<IReadOnlyDictionary<string, string>> SecondaryExposures { get; }
        public string? Reason { get; }
        public EvaluationDetails? EvaluationDetails { get; }

        static FeatureGate? _defaultConfig;

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

        public FeatureGate(string? name = null, bool value = false, string? ruleID = null, List<IReadOnlyDictionary<string, string>>? secondaryExposures = null, string? reason = null, EvaluationDetails? details = null)
        {
            Name = name ?? "";
            Value = value;
            RuleID = ruleID ?? "";
            SecondaryExposures = secondaryExposures ?? new List<IReadOnlyDictionary<string, string>>();
            Reason = reason ?? EvaluationReason.Uninitialized;
            EvaluationDetails = details;
        }

        internal static FeatureGate? FromJObject(string name, JObject? jobj)
        {
            if (jobj == null)
            {
                return null;
            }

            JToken? ruleToken;
            if (!jobj.TryGetValue("rule_id", out ruleToken))
            {
                return null;
            }

            JToken? valueToken;
            if (!jobj.TryGetValue("value", out valueToken))
            {
                return null;
            }

            JToken? nameToken;
            if (!jobj.TryGetValue("name", out nameToken))
            {
                return null;
            }

            try
            {
                return new FeatureGate
                (
                    nameToken.Value<string>(),
                    valueToken.Value<bool>(),
                    ruleToken.Value<string>(),
                    jobj.TryGetValue("secondary_exposures", out JToken? exposures)
                        ? exposures.ToObject<List<IReadOnlyDictionary<string, string>>>()
                        : new List<IReadOnlyDictionary<string, string>>()
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
