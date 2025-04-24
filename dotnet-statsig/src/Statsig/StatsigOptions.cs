using System;
using System.Collections.Generic;
using System.Net.Http;
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

        /// <summary>
        /// The maximum number of events to batch before flushing logs to the server
        /// default: 1000
        /// </summary>
        public int LoggingBufferMaxSize = Constants.SERVER_MAX_LOGGER_QUEUE_LENGTH;

        /// <summary>
        /// How often to flush logs to Statsig
        /// default: 60
        /// </summary>
        public int LoggingIntervalSeconds = Constants.SERVER_MAX_LOGGER_WAIT_TIME_IN_SEC;

        /// <summary>
        /// A class that extends IUserPersistentStorage. Can be used to save and load values for users
        /// to ensure they get consistent experiment assignments across sessions.
        /// </summary>
        public IUserPersistentStorage UserPersistentStorage;

        /// <summary>
        /// Allows setting environment to a custom value. Will take precedence over the StatsigEnvironment value.
        /// </summary>
        public string? CustomEnvironment;

        public StatsigServerOptions(string? apiUrlBase = null, StatsigEnvironment? environment = null) : base(
            apiUrlBase, environment)
        {
        }
    }

    /// <summary>
    /// Configuration options for the Statsig Client SDK
    /// </summary>
    public class StatsigClientOptions : StatsigOptions
    {
        /// <summary>
        /// The maximum number of events to batch before flushing logs to the server
        /// default: 100
        /// </summary>
        public int LoggingBufferMaxSize = Constants.CLIENT_MAX_LOGGER_QUEUE_LENGTH;

        /// <summary>
        /// How often to flush logs to Statsig
        /// default: 10
        /// </summary>
        public int LoggingIntervalSeconds = Constants.CLIENT_MAX_LOGGER_WAIT_TIME_IN_SEC;

        public StatsigClientOptions(string? apiUrlBase = null, StatsigEnvironment? environment = null) : base(
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

        public HttpClient? HttpClient { get; set; } = null;

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