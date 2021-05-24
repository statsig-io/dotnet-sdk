using System;
namespace Statsig
{
    internal static class Constants
    {
        public static string DEFAULT_API_URL_BASE = "https://api.statsig.com/v1";
        public static int MAX_SCALAR_LENGTH = 64;
        public static int MAX_METADATA_LENGTH = 1024;

        public static int SERVER_MAX_LOGGER_QUEUE_LENGTH = 1000;
        public static int SERVER_MAX_LOGGER_WAIT_TIME_IN_SEC = 60;
        public static int SERVER_CONFIG_SPECS_SYNC_INTERVAL_IN_SEC = 10;
        public static string DYNAMIC_CONFIG_SPEC_TYPE = "dynamic_config";
        public static string DEFAULT_RULE_ID = "default";

        public static int CLIENT_MAX_LOGGER_QUEUE_LENGTH = 10;
        public static int CLIENT_MAX_LOGGER_WAIT_TIME_IN_SEC = 10;

        public static string GATE_EXPOSURE_EVENT = "statsig::gate_exposure";
        public static string CONFIG_EXPOSURE_EVENT = "statsig::config_exposure";
    }
}
