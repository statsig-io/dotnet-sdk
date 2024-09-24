

using System.Collections.Generic;

namespace Statsig.Server
{
    public class StatsigGetExperimentOptions
    {
        public Dictionary<string, StickyValue>? UserPersistedValues { get; set; }

        public StatsigGetExperimentOptions(Dictionary<string, StickyValue>? userPersistedValues)
        {
            UserPersistedValues = userPersistedValues;
        }
    }
}