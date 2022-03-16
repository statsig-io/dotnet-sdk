﻿using System;
using Microsoft.Extensions.Logging;

namespace Statsig
{
    public class StatsigOptions
    {
        public string ApiUrlBase { get; }
        public StatsigEnvironment StatsigEnvironment { get; }
        public string PersistentStorageFolder { get; set; }
        internal StatsigCustomLogger logger;

        public ILogger CustomLogger
        {
            set
            {
                logger = new StatsigCustomLogger(value);
            }
        }

        public StatsigOptions()
        {
            ApiUrlBase = Constants.DEFAULT_API_URL_BASE;
            StatsigEnvironment = new StatsigEnvironment();
        }

        public StatsigOptions(StatsigEnvironment environment = null)
        {
            ApiUrlBase = Constants.DEFAULT_API_URL_BASE;
            StatsigEnvironment = environment ?? new StatsigEnvironment();
        }

        public StatsigOptions(string apiUrlBase = null, StatsigEnvironment environment = null)
        {
            ApiUrlBase = string.IsNullOrWhiteSpace(apiUrlBase) ?
                Constants.DEFAULT_API_URL_BASE : apiUrlBase;
            StatsigEnvironment = environment ?? new StatsigEnvironment();
        }
    }
}
