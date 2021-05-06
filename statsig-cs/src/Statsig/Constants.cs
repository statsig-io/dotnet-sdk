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

        public static int CLIENT_MAX_LOGGER_QUEUE_LENGTH = 10;
        public static int CLIENT_MAX_LOGGER_WAIT_TIME_IN_SEC = 10;

        public static string GATE_EXPOSURE_EVENT = "statsig::gate_exposure";
        public static string CONFIG_EXPOSURE_EVENT = "statsig::config_exposure";
    }
}
