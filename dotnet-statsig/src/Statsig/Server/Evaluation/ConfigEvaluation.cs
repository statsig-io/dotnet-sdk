using System;
using System.Collections.Generic;

namespace Statsig.Server.Evaluation
{
    enum EvaluationResult
    {
        Pass,
        Fail,
        Unsupported
    }

    public class EvaluationReason
    {
        public static string Network = "Network";
        public static string LocalOverride = "LocalOverride";
        public static string Unrecognized = "Unrecognized";
        public static string Uninitialized = "Uninitialized";
        public static string Bootstrap = "Bootstrap";
        public static string DataAdapter = "DataAdapter";
        public static string Unsupported = "Unsupported";
        public static string Error = "Error";
        public static string Persisted = "Persisted";

    }

    class ConfigEvaluation
    {
        internal EvaluationResult Result { get; set; }
        internal FeatureGate GateValue { get; set; }
        internal DynamicConfig ConfigValue { get; set; }
        internal List<IReadOnlyDictionary<string, string>> UndelegatedSecondaryExposures { get; set; }
        internal List<string> ExplicitParameters { get; set; }
        internal string? ConfigDelegate { get; set; }
        internal string Reason { get; set; }

        internal ConfigEvaluation(
            EvaluationResult result,
            string reason,
            FeatureGate? gate = null,
            DynamicConfig? config = null)
        {
            Result = result;
            GateValue = gate ?? new FeatureGate();
            ConfigValue = config ?? new DynamicConfig();
            UndelegatedSecondaryExposures = ConfigValue.SecondaryExposures;
            ExplicitParameters = new List<string>();
            Reason = reason;
        }

        internal StickyValue ToStickyValue(double configSyncTime)
        {
            return new StickyValue
            {
                Value = Result == EvaluationResult.Pass,
                JsonValue = ConfigValue.Value,
                RuleID = ConfigValue.RuleID,
                GroupName = ConfigValue.GroupName,
                SecondaryExposures = ConfigValue.SecondaryExposures,
                UndelegatedSecondaryExposures = UndelegatedSecondaryExposures,
                ConfigDelegate = ConfigDelegate,
                ExplicitParameters = ExplicitParameters.Count > 0 ? ExplicitParameters : null,
                Time = configSyncTime
            };
        }

        internal static ConfigEvaluation fromStickyValues(StickyValue stickyValue)
        {
            return new ConfigEvaluation(
                stickyValue.Value ? EvaluationResult.Pass : EvaluationResult.Fail,
                EvaluationReason.Persisted,
                new FeatureGate(),
                new DynamicConfig(
                    value: stickyValue.JsonValue,
                    ruleID: stickyValue.RuleID,
                    groupName: stickyValue.GroupName,
                    secondaryExposures: stickyValue.SecondaryExposures,
                    explicitParameters: stickyValue.ExplicitParameters
                )
            );
        }
    }
}
