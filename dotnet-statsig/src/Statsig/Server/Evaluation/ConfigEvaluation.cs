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
        internal List<IReadOnlyDictionary<string, string>> SecondaryExposures { get; set; }

        internal ConfigEvaluation(
            EvaluationResult result,
            FeatureGate gate = null,
            DynamicConfig config = null,
            List<IReadOnlyDictionary<string, string>> secondaryExposures = null)
        {
            Result = result;
            GateValue = gate;
            ConfigValue = config;
            SecondaryExposures = secondaryExposures ?? new List<IReadOnlyDictionary<string, string>>();
        }
    }
}
