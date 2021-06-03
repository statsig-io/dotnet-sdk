using System;
using System.Linq;
using System.Numerics;
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

        internal Evaluator(string serverSecret, ConnectionOptions options)
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
            if (spec.Enabled)
            {
                foreach (ConfigRule rule in spec.Rules)
                {
                    var result = EvaluateRule(user, rule);
                    switch (result)
                    {
                        case EvaluationResult.FetchFromServer:
                            return new ConfigEvaluation(EvaluationResult.FetchFromServer);
                        case EvaluationResult.Pass:
                            // return the value of the first rule that the user passes.
                            var passPercentage = EvaluatePassPercentage(user, rule, spec.Salt);
                            if (passPercentage)
                            {
                                return new ConfigEvaluation(EvaluationResult.Pass, rule.FeatureGateValue, rule.DynamicConfigValue);
                            }
                            else
                            {
                                // when the user passes conditions but fail due to pass percentage, we return the default value with the current rule ID.
                                var gateValue = new FeatureGate(spec.FeatureGateDefault.Name, spec.FeatureGateDefault.Value, rule.ID);
                                var configValue = new DynamicConfig(spec.DynamicConfigDefault.ConfigName, spec.DynamicConfigDefault.Value, rule.ID);
                                return new ConfigEvaluation(EvaluationResult.Fail, gateValue, configValue);
                            }
                        case EvaluationResult.Fail:
                        default:
                            break;
                    }
                }
            }
            return new ConfigEvaluation(EvaluationResult.Fail, spec.FeatureGateDefault, spec.DynamicConfigDefault);
        }

        private bool EvaluatePassPercentage(StatsigUser user, ConfigRule rule, string salt)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.ASCII.GetBytes(string.Format("{0}.{1}.{2}", salt, rule.Name, user.UserID ?? "")));
                var result = new BigInteger(bytes);
                var mod = new BigInteger(10000);
                return (result % mod) < new BigInteger(rule.PassPercentage * 100);
            }
        }

        private EvaluationResult EvaluateRule(StatsigUser user, ConfigRule rule)
        {
            foreach (ConfigCondition condition in rule.Conditions)
            {
                var result = EvaluateCondition(user, condition);
                switch (result)
                {
                    case EvaluationResult.FetchFromServer:
                        return result;
                    case EvaluationResult.Fail:
                        return result;
                    case EvaluationResult.Pass:
                    default:
                        break;
                }
            }
            return EvaluationResult.Pass;
        }

        private EvaluationResult EvaluateCondition(StatsigUser user, ConfigCondition condition)
        {
            var type = condition.Type.ToLowerInvariant();
            var op = condition.Operator?.ToLowerInvariant();
            var target = condition.TargetValue.Value<object>();
            var field = condition.Field;
            bool fetchFromServer = false;
            object value;
            switch (type)
            {
                case "public":
                    return EvaluationResult.Pass;
                case "fail_gate":
                    var otherGateResult = CheckGate(user, target.ToString().ToLowerInvariant());
                    switch (otherGateResult.Result)
                    {
                        case EvaluationResult.FetchFromServer:
                            return EvaluationResult.FetchFromServer;
                        case EvaluationResult.Fail:
                            return EvaluationResult.Pass;
                        case EvaluationResult.Pass:
                        default:
                            return EvaluationResult.Fail;
                    }
                case "pass_gate":
                    return CheckGate(user, target.ToString().ToLowerInvariant()).Result;
                case "ip_based":
                    value = GetFromUser(user, field) ??
                        GetFromIP(user.IPAddress, field, out fetchFromServer);
                    if (fetchFromServer)
                    {
                        return EvaluationResult.FetchFromServer;
                    }
                    break;
                case "ua_based":
                    value = GetFromUser(user, field) ?? GetFromUserAgent(user.UserAgent, field);
                    if (fetchFromServer)
                    {
                        return EvaluationResult.FetchFromServer;
                    }
                    break;
                case "user_field":
                    value = GetFromUser(user, field);
                    break;
                case "current_time":
                    value = DateTime.Now;
                    break;
                default:
                    return EvaluationResult.FetchFromServer;
            }
            if (value == null)
            {
                return EvaluationResult.Fail;
            }

            bool result = false;
            object[] targetArray = (condition.TargetValue as JArray)?.Cast<object>().ToArray();

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
                    result = ArrayContains(targetArray, value);
                    break;
                case "none":
                    result = !ArrayContains(targetArray, value);
                    break;

                // string
                case "str_starts_with_any":
                    result = MatchStringCaseInsensitiveInArray(targetArray, value.ToString(),
                        (string s1, string s2) => (s1.StartsWith(s2)));
                    break;
                case "str_ends_with_any":
                    result = MatchStringCaseInsensitiveInArray(targetArray, value.ToString(),
                        (string s1, string s2) => (s1.EndsWith(s2)));
                    break;
                case "str_contains_any":
                    result = MatchStringCaseInsensitiveInArray(targetArray, value.ToString(),
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
                    // TODO:
                case "after":
                    // TODO:
                case "on":
                    // TODO:
                default:
                    return EvaluationResult.FetchFromServer;
            }

            return result ? EvaluationResult.Pass : EvaluationResult.Fail;
        }
    }
}
