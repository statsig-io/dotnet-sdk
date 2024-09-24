using System.Collections.Generic;

namespace Statsig.Server
{
    public class StatsigGetLayerOptions
    {
        public Dictionary<string, StickyValue>? UserPersistedValues { get; set; }

        public StatsigGetLayerOptions(Dictionary<string, StickyValue>? userPersistedValues)
        {
            UserPersistedValues = userPersistedValues;
        }
    }
}