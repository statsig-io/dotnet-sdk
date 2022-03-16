﻿using System.Timers;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Statsig.Network
{
    public class EventLogger
    {
        int _maxQueueLength;
        SDKDetails _sdkDetails;
        Timer _flushTimer;
        ConcurrentBag<EventLog> _eventLogQueue;
        RequestDispatcher _dispatcher;

        public EventLogger(RequestDispatcher dispatcher, SDKDetails sdkDetails, int maxQueueLength = 100, int maxThresholdSecs = 60)
        {
            _sdkDetails = sdkDetails;
            _maxQueueLength = maxQueueLength;
            _dispatcher = dispatcher;

            _eventLogQueue = new ConcurrentBag<EventLog>();

            _flushTimer = new Timer
            {
                Interval = maxThresholdSecs * 1000,
                Enabled = true,
                AutoReset = true,
            };
            _flushTimer.Elapsed += async (sender, e) => await FlushEvents();
        }

        public void Enqueue(EventLog entry)
        {
            _eventLogQueue.Add(entry);
            if (_eventLogQueue.Count >= _maxQueueLength)
            {
                ForceFlush();
            }
        }

        internal void ForceFlush()
        {
            var task = FlushEvents();
            task.Wait();
        }

        async Task FlushEvents()
        {
            if (_eventLogQueue.Count == 0)
            {
                return;
            }
            var snapshot = _eventLogQueue;
            _eventLogQueue = new ConcurrentBag<EventLog>();

            var body = new Dictionary<string, object>
            {
                ["statsigMetadata"] = GetStatsigMetadata(),
                ["events"] = snapshot
            };

            await _dispatcher.Fetch("log_event", body, 5, 1);
        }

        IReadOnlyDictionary<string, string> GetStatsigMetadata()
        {
            return new Dictionary<string, string>
            {
                ["sdkType"] = _sdkDetails.SDKType,
                ["sdkVersion"] = _sdkDetails.SDKVersion,
            };
        }

        public void Shutdown()
        {
            _flushTimer.Stop();
            _flushTimer.Dispose();
            ForceFlush();
        }
    }
}
