using System;
using System.Collections.Generic;
using Statsig.Lib;
using Statsig.Server.Interfaces;

namespace Statsig
{
    /// <summary>
    /// Configuration options for the Statsig Server SDK
    /// </summary>
    public class StatsigServerOptions : StatsigOptions
    {
        /// <summary>
        /// Restricts the SDK to not issue any network requests and only respond with default values (or local overrides)
        /// </summary>
        public bool LocalMode;
        
        /// <summary>
        /// A class that extends IDataStore. Can be used to provide values from a
        /// common data store (like Redis) to initialize the Statsig SDK.
        /// </summary>
        public IDataStore DataStore;

        public StatsigServerOptions(string? apiUrlBase = null, StatsigEnvironment? environment = null) : base(
            apiUrlBase, environment)
        {
        }
    }

    public class StatsigOptions
    {
        public string ApiUrlBase { get; }
        public StatsigEnvironment StatsigEnvironment { get; }
        public string? PersistentStorageFolder { get; set; }
        public double RulesetsSyncInterval = Constants.SERVER_CONFIG_SPECS_SYNC_INTERVAL_IN_SEC;
        public double IDListsSyncInterval = Constants.SERVER_ID_LISTS_SYNC_INTERVAL_IN_SEC;
        public int ClientRequestTimeoutMs = 0;

        private Dictionary<string, string> _additionalHeaders;
        private Func<IIDStore>? _idStoreFactory = null;

        public StatsigOptions(string? apiUrlBase = null, StatsigEnvironment? environment = null)
        {
            ApiUrlBase = string.IsNullOrWhiteSpace(apiUrlBase) ? Constants.DEFAULT_API_URL_BASE : apiUrlBase!;
            StatsigEnvironment = environment ?? new StatsigEnvironment();
            _additionalHeaders = new Dictionary<string, string>();
        }

        internal IReadOnlyDictionary<string, string> AdditionalHeaders
        {
            get { return _additionalHeaders; }
        }

        public Func<IIDStore>? IDStoreFactory
        {
            get { return _idStoreFactory; }
            set { _idStoreFactory = value; }
        }

        public void AddRequestHeader(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Both Key and Value need to be non-empty");
            }

            _additionalHeaders.Add(key, value);
        }
    }
}