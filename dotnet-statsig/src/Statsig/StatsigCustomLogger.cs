using System;
using Microsoft.Extensions.Logging;

namespace Statsig
{
    internal class StatsigCustomLogger
    {
        ILogger _logger;
        internal StatsigCustomLogger(ILogger logger)
        {
            _logger = logger;
            // TODO: also log to Statsig telemetry
        }

        internal void LogError(Exception e, string message)
        {
            if (_logger != null)
            {
                _logger.LogError(e, message);
            }
        }

        internal void LogWarning(Exception e, string message)
        {
            if (_logger != null)
            {
                _logger.LogWarning(e, message);
            }
        }

        internal void LogDebug(Exception e, string message)
        {
            if (_logger != null)
            {
                _logger.LogDebug(e, message);
            }
        }
    }
}
