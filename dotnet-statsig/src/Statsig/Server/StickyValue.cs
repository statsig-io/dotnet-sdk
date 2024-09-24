
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Statsig.Server
{
    [JsonObject(ItemNullValueHandling = NullValueHandling.Include)]
    public class StickyValue
    {
        [JsonProperty("value")]
        public bool Value { get; set; }

        [JsonProperty("json_value")]
        public IReadOnlyDictionary<string, JToken> JsonValue { get; set; }

        [JsonProperty("rule_id")]
        public string RuleID { get; set; }

        [JsonProperty("group_name")]
        public string? GroupName { get; set; }

        [JsonProperty("secondary_exposures")]
        public List<IReadOnlyDictionary<string, string>> SecondaryExposures { get; set; }

        [JsonProperty("undelegated_secondary_exposures")]
        public List<IReadOnlyDictionary<string, string>>? UndelegatedSecondaryExposures { get; set; }

        [JsonProperty("config_delegate")]
        public string? ConfigDelegate { get; set; }

        [JsonProperty("explicit_parameters")]
        public List<string>? ExplicitParameters { get; set; }

        [JsonProperty("time")]
        public double Time { get; set; }
    }
}