using System;
using System.Collections.Generic;

namespace Statsig.Server.Evaluation
{
    enum EvaluationResult
    {
        Pass,
        Fail,
        FetchFromServer
    }

    class ConfigEvaluation
    {
        internal EvaluationResult Result { get; set; }
        internal FeatureGate GateValue { get; set; }
        internal DynamicConfig ConfigValue { get; set; }
        internal List<IReadOnlyDictionary<string, string>> UndelegatedSecondaryExposures { get; set; }
        internal List<string> ExplicitParameters { get; set; }
        internal string ConfigDelegate { get; set; }

        internal ConfigEvaluation(
            EvaluationResult result,
            FeatureGate gate = null,
            DynamicConfig config = null)
        {
            Result = result;
            GateValue = gate ?? new FeatureGate();
            ConfigValue = config ?? new DynamicConfig();
        }
    }
}
