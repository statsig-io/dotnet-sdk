using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Statsig.Network
{
    public class EventLogger
    {
        private int _dedupeInterval = 60 * 1000;

        private readonly int _maxQueueLength;
        private readonly SDKDetails _sdkDetails;
        private readonly RequestDispatcher _dispatcher;
        private readonly Dictionary<string, string> _statsigMetadata;

        private readonly Task _backgroundPeriodicFlushTask;
        private readonly CancellationTokenSource _shutdownCTS;
        private DateTime _lastFlushTime = DateTime.UtcNow;

        internal List<EventLog> _eventLogQueue;
        private readonly HashSet<string> _errorsLogged;
        private readonly HashSet<int> _eventDedupeSet;
        private readonly object _queueLock;
        private ConcurrentDictionary<Task, bool> _tasks;

        private DateTime _dedupeStartTime;

        public EventLogger(RequestDispatcher dispatcher, SDKDetails sdkDetails, int maxQueueLength,
            int maxThresholdSecs, int dedupeInterval = 60 * 1000)
        {
            _sdkDetails = sdkDetails;
            _maxQueueLength = maxQueueLength;
            _dispatcher = dispatcher;
            _statsigMetadata = new Dictionary<string, string>
            {
                ["sdkType"] = _sdkDetails.SDKType,
                ["sdkVersion"] = _sdkDetails.SDKVersion,
            };

            _eventLogQueue = new List<EventLog>();
            _errorsLogged = new HashSet<string>();
            _eventDedupeSet = new HashSet<int>();
            _tasks = new ConcurrentDictionary<Task, bool>();
            _queueLock = new object();

            _dedupeInterval = dedupeInterval;
            ResetDedupeSet();

            _shutdownCTS = new CancellationTokenSource();
            _backgroundPeriodicFlushTask = BackgroundPeriodicFlushTask(maxThresholdSecs, _shutdownCTS.Token);
        }

        public void Enqueue(EventLog entry)
        {
            bool flushNeeded = false;

            lock (_queueLock)
            {
                // If this is an error event, check to see if we already have a queued event for this
                // error, and if so then don't bother adding the event to the queue.
                if (entry.IsErrorLog)
                {
                    if (!_errorsLogged.Add(entry.ErrorKey))
                    {
                        return;
                    }
                }

                if (ShouldAddEventAfterDeduping(entry))
                {
                    _eventLogQueue.Add(entry);

                    // Determine if a flush is needed
                    flushNeeded = _eventLogQueue.Count >= _maxQueueLength;
                }
            }

            if (!flushNeeded)
            {
                return;
            }

            // If a flush is needed, then fire-and-forget a call to FlushEvents()
            try
            {
                var task = FlushEvents();
                _tasks[task] = true;
                task.ContinueWith(t => { _tasks.TryRemove(task, out _); });
            }
            catch
            {
                // TODO: Log this
            }
        }

        private async Task BackgroundPeriodicFlushTask(int maxThresholdSecs, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Figure out how long we need to wait until the next periodic event flush
                    // should trigger, and then delay for that long.
                    var timeUntilNextFlush = _lastFlushTime.AddSeconds(maxThresholdSecs) - DateTime.UtcNow;
                    if (timeUntilNextFlush > TimeSpan.Zero)
                    {
                        await Task.Delay(timeUntilNextFlush, cancellationToken).ConfigureAwait(false);
                    }

                    // While waiting, a flush may have been triggered because the queue filled up,
                    // so check once more to make sure that enough time has elapsed since the last
                    // event flush, and if so, then trigger a flush.
                    if (_lastFlushTime.AddSeconds(maxThresholdSecs) <= DateTime.UtcNow)
                    {
                        await FlushEvents().ConfigureAwait(false);
                    }
                }
                catch (TaskCanceledException)
                {
                    // This is expected to occur when the cancellationToken is signaled during the Task.Delay()
                    break;
                }
                catch
                {
                    // TODO: Log this
                }
            }

            // Do one final flush before exiting
            await FlushEvents().ConfigureAwait(false);
        }

        internal async Task FlushEvents()
        {
            _lastFlushTime = DateTime.UtcNow;

            // Grab a snapshot of the events that are currently queued and then clear the queue
            List<EventLog> snapshot;
            lock (_queueLock)
            {
                if (_eventLogQueue.Count == 0)
                {
                    return;
                }

                snapshot = _eventLogQueue;
                _eventLogQueue = new List<EventLog>();
                _errorsLogged.Clear();
            }

            // Generate the log event request and dispatch it
            var body = new Dictionary<string, object>
            {
                ["statsigMetadata"] = _statsigMetadata,
                ["events"] = snapshot
            };

            await _dispatcher.Fetch("log_event", body, 5, 1);
        }

        public async Task Shutdown()
        {
            // Signal that the periodic flush task should exit, and then wait for it finish
            _shutdownCTS.Cancel();
            await Task.WhenAll(
                _backgroundPeriodicFlushTask,
                FlushEvents(),
                Task.WhenAll(_tasks.Keys)
            ).ConfigureAwait(false);
        }

        private bool ShouldAddEventAfterDeduping(EventLog entry)
        {
            if (!entry.IsExposureLog)
            {
                return true;
            }

            if ((DateTime.Now - _dedupeStartTime).TotalMilliseconds > _dedupeInterval)
            {
                ResetDedupeSet();
                return true;
            }

            var hash = entry.GetDedupeKey();
            if (_eventDedupeSet.Contains(hash))
            {
                return false;
            }

            _eventDedupeSet.Add(hash);
            return true;
        }

        private void ResetDedupeSet()
        {
            _dedupeStartTime = DateTime.Now;
            _eventDedupeSet.Clear();
        }
    }
}