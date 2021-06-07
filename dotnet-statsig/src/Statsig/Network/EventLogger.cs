using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Statsig.Network
{
    public class EventLogger
    {
        int _maxQueueLength, _maxThresholdSecs;
        SDKDetails _sdkDetails;
        Timer _threadTimer;
        List<EventLog> _eventLogQueue;
        RequestDispatcher _dispatcher;
        HashSet<string> _errorsLogged;


        public EventLogger(RequestDispatcher dispatcher, SDKDetails sdkDetails, int maxQueueLength = 100, int maxThresholdSecs = 60)
        {
            _sdkDetails = sdkDetails;
            _maxQueueLength = maxQueueLength;
            _maxThresholdSecs = maxThresholdSecs;

            _eventLogQueue = new List<EventLog>();
            _errorsLogged = new HashSet<string>();

            _threadTimer = new Timer(TimerCallback);
            _dispatcher = dispatcher;
        }

        public void Enqueue(EventLog entry)
        {
            if (entry.IsErrorLog) { 
                if (_errorsLogged.Contains(entry.ErrorKey))
                {
                    return;
                }
                else
                {
                    _errorsLogged.Add(entry.ErrorKey);
                }
            }

            _eventLogQueue.Add(entry);
            if (_eventLogQueue.Count == 1)
            {
                // Only triggered when the list was empty at start
                _threadTimer.Change(_maxThresholdSecs * 1000, Timeout.Infinite);
            }
            else if (_eventLogQueue.Count >= _maxQueueLength)
            {
                _threadTimer.Change(0, Timeout.Infinite);
            }
        }

        internal void ForceFlush()
        {
            var task = FlushEvents();
            task.Wait();
        }

        async Task FlushEvents()
        {
            var snapshot = _eventLogQueue;
            _eventLogQueue = new List<EventLog>();
            _errorsLogged.Clear();

            var body = new Dictionary<string, object>
            {
                ["statsigMetadata"] = GetStatsigMetadata(),
                ["events"] = snapshot
            };

            await _dispatcher.Fetch("log_event", body);
        }

        IReadOnlyDictionary<string, string> GetStatsigMetadata()
        {
            return new Dictionary<string, string>
            {
                ["sdkType"] = _sdkDetails.SDKType,
                ["sdkVersion"] = _sdkDetails.SDKVersion,
            };
        }

        async void TimerCallback(object state)
        {
            await FlushEvents();
        }
    }
}
