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

    enum EvaluationReason
    {
        Network,
        LocalOverride,
        Unrecognized,
        Uninitialized,
        Bootstrap,
        DataAdapter,
        Unsupported

    }

    class ConfigEvaluation
    {
        internal EvaluationResult Result { get; set; }
        internal FeatureGate GateValue { get; set; }
        internal DynamicConfig ConfigValue { get; set; }
        internal List<IReadOnlyDictionary<string, string>> UndelegatedSecondaryExposures { get; set; }
        internal List<string> ExplicitParameters { get; set; }
        internal string? ConfigDelegate { get; set; }
        internal EvaluationReason Reason { get; set; }

        internal ConfigEvaluation(
            EvaluationResult result,
            EvaluationReason reason,
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
    }
}
