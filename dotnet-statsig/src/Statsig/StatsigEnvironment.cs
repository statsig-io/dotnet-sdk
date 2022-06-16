using System.Collections.Generic;

namespace Statsig
{
    public enum EnvironmentTier
    {
        Production,
        Development,
        Staging,
    }

    public class StatsigEnvironment
    {
        public Dictionary<string, string> Values { get; }

        public StatsigEnvironment(EnvironmentTier? tier = null, IReadOnlyDictionary<string, string>? additionalParams = null)
        {
            Values = new Dictionary<string, string>();
            if (tier != null)
            {
                Values["tier"] = tier.ToString()!.ToLowerInvariant();
            };

            if (additionalParams != null)
            {
                foreach (var pair in additionalParams)
                {
                    Values[pair.Key.ToLowerInvariant()] = pair.Value;
                }
            }
        }
    }
}
