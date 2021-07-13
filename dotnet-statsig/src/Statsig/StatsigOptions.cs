using System;

namespace Statsig
{
    public class StatsigOptions
    {
        public string ApiUrlBase { get; }
        public StatsigEnvironment StatsigEnvironment { get; }

        public StatsigOptions()
        {
            ApiUrlBase = Constants.DEFAULT_API_URL_BASE;
            StatsigEnvironment = null;
        }

        public StatsigOptions(StatsigEnvironment environment = null)
        {
            ApiUrlBase = Constants.DEFAULT_API_URL_BASE;
            StatsigEnvironment = environment;
        }

        public StatsigOptions(string apiUrlBase = null, StatsigEnvironment environment = null)
        {
            ApiUrlBase = String.IsNullOrWhiteSpace(apiUrlBase) ?
                Constants.DEFAULT_API_URL_BASE : apiUrlBase;
            StatsigEnvironment = environment;
        }
    }
}
