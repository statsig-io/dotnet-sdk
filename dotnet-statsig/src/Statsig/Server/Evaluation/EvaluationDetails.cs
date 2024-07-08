
using System;
using System.Collections.Generic;

namespace Statsig.Server.Evaluation
{
    public class EvaluationDetails
    {
        public string Reason { get; }
        public long InitTime { get; }
        public long ServerTime { get; }
        public long ConfigSyncTime { get; }

        public EvaluationDetails(string reason, long initTime, long configSyncTime)
        {
            Reason = reason;
            this.InitTime = initTime;
            this.ServerTime = DateTime.Now.Millisecond;
            this.ConfigSyncTime = configSyncTime;
        }
    }
}