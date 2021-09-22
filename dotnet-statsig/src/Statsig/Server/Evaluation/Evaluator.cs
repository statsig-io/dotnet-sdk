using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using static Statsig.Server.Evaluation.Helpers;

namespace Statsig.Server.Evaluation
{
    class Evaluator
    {
        SpecStore _store;
        bool _initialized;

        internal Evaluator(string serverSecret, StatsigOptions options)
        {
            _store = new SpecStore(serverSecret, options);
            _initialized = false;
        }

        internal async Task Initialize()
        {
            await _store.Initialize();
            _initialized = true;
        }

        internal void Shutdown()
        {
            _store.Shutdown();
        }

        internal ConfigEvaluation CheckGate(StatsigUser user, string gateName)
        {
            gateName = gateName.ToLowerInvariant();
            if (!_initialized || string.IsNullOrWhiteSpace(gateName) || !_store.FeatureGates.ContainsKey(gateName))
            {
                return null;
            }
            return Evaluate(user, _store.FeatureGates[gateName]);
        }

        internal ConfigEvaluation GetConfig(StatsigUser user, string configName)
        {
            configName = configName.ToLowerInvariant();
            if (!_initialized || string.IsNullOrWhiteSpace(configName) || !_store.DynamicConfigs.ContainsKey(configName))
            {
                return null;
            }
            return Evaluate(user, _store.DynamicConfigs[configName]);
        }

        private ConfigEvaluation Evaluate(StatsigUser user, ConfigSpec spec)
        {
            var secondaryExposures = new List<IReadOnlyDictionary<string, string>>();
            var defaultRuleID = "default";
            if (spec.Enabled)
            {
                foreach (ConfigRule rule in spec.Rules)
                {
                    var result = EvaluateRule(user, rule, out List<IReadOnlyDictionary<string, string>> ruleExposures);
                    secondaryExposures.AddRange(ruleExposures);
                    switch (result)
                    {
                        case EvaluationResult.FetchFromServer:
                            return new ConfigEvaluation(EvaluationResult.FetchFromServer);
                        case EvaluationResult.Pass:
                            // return the value of the first rule that the user passes.
                            var passPercentage = EvaluatePassPercentage(user, rule, spec);
                            var gateV = new FeatureGate
                            (
                                spec.Name,
                                passPercentage ? rule.FeatureGateValue.Value : spec.FeatureGateDefault.Value,
                                rule.ID,
                                secondaryExposures
                            );
                            var configV = new DynamicConfig
                            (
                                spec.Name,
                                passPercentage ? rule.DynamicConfigValue.Value : spec.DynamicConfigDefault.Value,
                                rule.ID,
                                secondaryExposures
                            );
                            return new ConfigEvaluation(passPercentage ? EvaluationResult.Pass : EvaluationResult.Fail, gateV, configV);
                        case EvaluationResult.Fail:
                        default:
                            break;
                    }
                }
            }
            else
            {
                defaultRuleID = "disabled";
            }
            return new ConfigEvaluation
            (
                EvaluationResult.Fail,
                new FeatureGate(spec.Name, spec.FeatureGateDefault.Value, defaultRuleID, secondaryExposures),
                new DynamicConfig(spec.Name, spec.DynamicConfigDefault.Value, defaultRuleID, secondaryExposures)
            );
        }

        private ulong ComputeUserHash(string userHash)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(userHash));
                ulong result = BitConverter.ToUInt64(bytes, 0);
                if (BitConverter.IsLittleEndian)
                {
                    // we use big endian in the backend so need to be consistent here.
                    result = SwapBytes(result);
                }
                return result;
            }
        }

        // Swap bytes to change from little endian to big endian
        // https://stackoverflow.com/a/19560621/1524355
        private ulong SwapBytes(ulong x)
        {
            // swap adjacent 32-bit blocks
            x = (x >> 32) | (x << 32);
            // swap adjacent 16-bit blocks
            x = ((x & 0xFFFF0000FFFF0000) >> 16) | ((x & 0x0000FFFF0000FFFF) << 16);
            // swap adjacent 8-bit blocks
            return ((x & 0xFF00FF00FF00FF00) >> 8) | ((x & 0x00FF00FF00FF00FF) << 8);
        }

        private bool EvaluatePassPercentage(StatsigUser user, ConfigRule rule, ConfigSpec spec)
        {
            var hash = ComputeUserHash(string.Format("{0}.{1}.{2}", spec.Salt, rule.Salt ?? rule.ID, user.UserID ?? ""));
            return (hash % 10000) < (rule.PassPercentage * 100);
        }

        private EvaluationResult EvaluateRule(StatsigUser user, ConfigRule rule, out List<IReadOnlyDictionary<string, string>> secondaryExposures)
        {
            var passResult = EvaluationResult.Pass;
            secondaryExposures = new List<IReadOnlyDictionary<string, string>>();
            foreach (ConfigCondition condition in rule.Conditions)
            {
                var result = EvaluateCondition(user, condition, out List<IReadOnlyDictionary<string, string>> conditionExposures);
                if (result == EvaluationResult.FetchFromServer)
                {
                    return result;
                }

                secondaryExposures.AddRange(conditionExposures);
                // If any condition fails, the whole rule fails
                if (result == EvaluationResult.Fail)
                {
                    passResult = EvaluationResult.Fail;
                }
            }
            return passResult;
        }

        private EvaluationResult EvaluateCondition(StatsigUser user, ConfigCondition condition, out List<IReadOnlyDictionary<string, string>> secondaryExposures)
        {
            secondaryExposures = new List<IReadOnlyDictionary<string, string>>();

            var type = condition.Type.ToLowerInvariant();
            var op = condition.Operator?.ToLowerInvariant();
            var target = condition.TargetValue.Value<object>();
            var field = condition.Field;
            object value;
            switch (type)
            {
                case "public":
                    return EvaluationResult.Pass;
                case "fail_gate":
                case "pass_gate":
                    var otherGateResult = CheckGate(user, target.ToString().ToLowerInvariant());
                    if (otherGateResult.Result == EvaluationResult.FetchFromServer)
                    {
                        return EvaluationResult.FetchFromServer;
                    }
                    var pass = otherGateResult.Result == EvaluationResult.Pass;
                    var newExposure = new Dictionary<string, string>
                    {
                        ["gate"] = target.ToString(),
                        ["gateValue"] = pass ? "true" : "false",
                        ["ruleID"] = otherGateResult.GateValue.RuleID
                    };
                    secondaryExposures = new List<IReadOnlyDictionary<string, string>>(otherGateResult.GateValue.SecondaryExposures);
                    secondaryExposures.Add(newExposure);
                    if ((type == "pass_gate" && pass) || (type == "fail_gate" && !pass))
                    {
                        return EvaluationResult.Pass;
                    }
                    else
                    {
                        return EvaluationResult.Fail;
                    }
                case "ip_based":
                    value = GetFromUser(user, field) ?? GetFromIP(user, field);
                    break;
                case "ua_based":
                    value = GetFromUser(user, field) ?? GetFromUserAgent(user, field);
                    break;
                case "user_field":
                    value = GetFromUser(user, field);
                    break;
                case "environment_field":
                    value = GetFromEnvironment(user, field);
                    break;
                case "current_time":
                    value = DateTime.Now;
                    break;
                case "user_bucket":
                    object salt;
                    if (condition.AdditionalValues.TryGetValue("salt", out salt))
                    {
                        var hash = ComputeUserHash(salt.ToString() + "." + user.UserID ?? "");
                        value = Convert.ToInt64(hash % 1000); // user bucket condition only has 1k segments as opposed to 10k for condition pass %
                    }
                    else
                    {
                        return EvaluationResult.Fail;
                    }
                    break;
                default:
                    return EvaluationResult.FetchFromServer;
            }

            bool result = false;
            object[] targetArray = (condition.TargetValue as JArray)?.ToObject<object[]>();

            switch (op)
            {
                // numerical
                case "gt":
                    result = CompareNumbers(value, target, (double x, double y) => (x > y));
                    break;
                case "gte":
                    result = CompareNumbers(value, target, (double x, double y) => (x >= y));
                    break;
                case "lt":
                    result = CompareNumbers(value, target, (double x, double y) => (x < y));
                    break;
                case "lte":
                    result = CompareNumbers(value, target, (double x, double y) => (x <= y));
                    break;

                // version
                case "version_gt":
                    result = CompareVersions(value, target, (Version v1, Version v2) => (v1 > v2));
                    break;
                case "version_gte":
                    result = CompareVersions(value, target, (Version v1, Version v2) => (v1 >= v2));
                    break;
                case "version_lt":
                    result = CompareVersions(value, target, (Version v1, Version v2) => (v1 < v2));
                    break;
                case "version_lte":
                    result = CompareVersions(value, target, (Version v1, Version v2) => (v1 <= v2));
                    break;
                case "version_eq":
                    result = CompareVersions(value, target, (Version v1, Version v2) => (v1 == v2));
                    break;
                case "version_neq":
                    result = CompareVersions(value, target, (Version v1, Version v2) => (v1 != v2));
                    break;

                // array
                case "any":
                    result = MatchStringInArray(targetArray, value, true, (string s1, string s2) => (s1.Equals(s2)));
                    break;
                case "none":
                    result = !MatchStringInArray(targetArray, value, true, (string s1, string s2) => (s1.Equals(s2)));
                    break;
                case "any_case_sensitive":
                    result = MatchStringInArray(targetArray, value, false, (string s1, string s2) => (s1.Equals(s2)));
                    break;
                case "none_case_sensitive":
                    result = !MatchStringInArray(targetArray, value, false, (string s1, string s2) => (s1.Equals(s2)));
                    break;

                // string
                case "str_starts_with_any":
                    result = MatchStringInArray(targetArray, value, true,
                        (string s1, string s2) => (s1.StartsWith(s2)));
                    break;
                case "str_ends_with_any":
                    result = MatchStringInArray(targetArray, value, true,
                        (string s1, string s2) => (s1.EndsWith(s2)));
                    break;
                case "str_contains_any":
                    result = MatchStringInArray(targetArray, value, true,
                        (string s1, string s2) => (s1.Contains(s2)));
                    break;
                case "str_contains_none":
                    result = !MatchStringInArray(targetArray, value, true,
                        (string s1, string s2) => (s1.Contains(s2)));
                    break;
                case "str_matches":
                    try
                    {
                        Regex r = new Regex(target.ToString(), RegexOptions.IgnoreCase);
                        Match m = r.Match(value.ToString());
                        result = !string.IsNullOrEmpty(m.Value);
                    }
                    catch
                    {
                        // If any of the input is invalid
                        result = false;
                    }
                    break;

                // strictly equals
                case "eq":
                    result = value == target;
                    break;
                case "neq":
                    result = value != target;
                    break;

                // dates
                case "before":
                    result = CompareTimes(value, target, (DateTimeOffset t1, DateTimeOffset t2) => (t1 < t2));
                    break;
                case "after":
                    result = CompareTimes(value, target, (DateTimeOffset t1, DateTimeOffset t2) => (t1 > t2));
                    break;
                case "on":
                    result = CompareTimes(value, target, (DateTimeOffset t1, DateTimeOffset t2) => (t1.Date == t2.Date));
                    break;
                default:
                    return EvaluationResult.FetchFromServer;
            }

            return result ? EvaluationResult.Pass : EvaluationResult.Fail;
        }
    }
}
