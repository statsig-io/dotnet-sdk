using Newtonsoft.Json.Linq;
using Statsig.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Statsig.Server
{

    class IDList
    {
        internal string Name { get; }
        internal HashSet<string> IDs { get; set; }
        internal double Time { get; set; }

        internal IDList(string name)
        {
            Name = name;
            Time = 0;
            IDs = new HashSet<string>();
        }
    }

    class SpecStore
    {
        private double _lastSyncTime;
        private readonly RequestDispatcher _requestDispatcher;

        private Task _syncIDListsTask;
        private Task _syncValuesTask;
        private readonly CancellationTokenSource _cts;

        internal Dictionary<string, ConfigSpec> FeatureGates { get; private set; }
        internal Dictionary<string, ConfigSpec> DynamicConfigs { get; private set; }
        private readonly Dictionary<string, IDList> _idLists;
        private readonly ReaderWriterLockSlim _idListLock;

        internal SpecStore(string serverSecret, StatsigOptions options)
        {
            _requestDispatcher = new RequestDispatcher(serverSecret, options.ApiUrlBase);
            _lastSyncTime = 0;
            FeatureGates = new Dictionary<string, ConfigSpec>();
            DynamicConfigs = new Dictionary<string, ConfigSpec>();
            _idLists = new Dictionary<string, IDList>();
            _idListLock = new ReaderWriterLockSlim();

            _syncIDListsTask = null;
            _syncValuesTask = null;
            _cts = new CancellationTokenSource();
        }

        internal async Task Initialize()
        {
            // Execute and wait for the initial syncs
            await SyncValues(true);
            await SyncIDLists();

            // Start background tasks to periodically refresh the store
            _syncValuesTask = BackgroundPeriodicSyncValuesTask(_cts.Token);
            _syncIDListsTask = BackgroundPeriodicSyncIDListsTask(_cts.Token);
        }

        internal async Task Shutdown()
        {
            // Signal that the periodic task should exit, and then wait for them to finish
            _cts.Cancel();
            if (_syncIDListsTask != null)
            {
                await _syncIDListsTask;
            }
            if (_syncValuesTask != null)
            {
                await _syncValuesTask;
            }
        }

        internal bool IDListContainsValue(string listName, string value)
        {
            _idListLock.EnterReadLock();
            try
            {
                return _idLists.TryGetValue(listName, out var list) && list.IDs.Contains(value);
            }
            finally
            {
                _idListLock.ExitReadLock();
            }
        }

        private async Task BackgroundPeriodicSyncIDListsTask(CancellationToken cancellationToken)
        {
            var delayInterval = TimeSpan.FromSeconds(Constants.SERVER_ID_LISTS_SYNC_INTERVAL_IN_SEC);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(delayInterval, cancellationToken);

                    await SyncIDLists();
                }
                catch (TaskCanceledException)
                {
                    // This is expected to occur when the cancellationToken is signaled during the Task.Delay()
                    break;
                }
                catch
                {
                    // TODO: log this
                }
            }
        }

        private async Task BackgroundPeriodicSyncValuesTask(CancellationToken cancellationToken)
        {
            var delayInterval = TimeSpan.FromSeconds(Constants.SERVER_CONFIG_SPECS_SYNC_INTERVAL_IN_SEC);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(delayInterval, cancellationToken);

                    await SyncValues(initialRequest: false);
                }
                catch (TaskCanceledException)
                {
                    // This is expected to occur when the cancellationToken is signaled during the Task.Delay()
                    break;
                }
                catch
                {
                    // TODO: log this
                }
            }
        }

        private async Task SyncIDLists()
        {
            // Build a list of requests from the current state of the _idLists
            List<Dictionary<string, object>> requests;
            _idListLock.EnterReadLock();
            try
            {
                requests = new List<Dictionary<string, object>>(_idLists.Count);
                foreach (var entry in _idLists)
                {
                    var list = entry.Value;
                    requests.Add(new Dictionary<string, object>
                    {
                        ["listName"] = entry.Key,
                        ["sinceTime"] = list.Time,
                        ["statsigMetadata"] = SDKDetails.GetServerSDKDetails().StatsigMetadata
                    });
                }
            }
            finally
            {
                _idListLock.ExitReadLock();
            }

            // Dispatch the requests in parallel
            var tasks = new List<Task<IReadOnlyDictionary<string, JToken>>>(requests.Count);
            foreach (var request in requests)
            {
                tasks.Add(_requestDispatcher.Fetch("download_id_list", request));
            }

            // Wait for all the requests to complete
            await Task.WhenAll(tasks);

            // Process the result of each request
            _idListLock.EnterWriteLock();
            try
            {
                foreach (Task<IReadOnlyDictionary<string, JToken>> task in tasks)
                {
                    // RequestDispatcher.Fetch() can return null if it fails to execute the request.
                    // Gracefully handle that error here.
                    if (task == null || task.Result == null)
                    {
                        continue;
                    }
                    var response = task.Result;
                    JToken name, time, addIDsToken, removeIDsToken;
                    var listName = response.TryGetValue("list_name", out name) ? name.Value<string>() : "";
                    if (_idLists.ContainsKey(listName))
                    {
                        var list = _idLists[listName];
                        var addIDs = response.TryGetValue("add_ids", out addIDsToken) ? addIDsToken.ToObject<string[]>() : new string[] { };
                        var removeIDs = response.TryGetValue("remove_ids", out removeIDsToken) ? removeIDsToken.ToObject<string[]>() : new string[] { };
                        foreach (string id in addIDs)
                        {
                            list.IDs.Add(id);
                        }
                        foreach (string id in removeIDs)
                        {
                            list.IDs.Remove(id);
                        }
                        var newTime = response.TryGetValue("time", out time) ? time.Value<double>() : 0;
                        list.Time = Math.Max(list.Time, newTime);
                    }
                }
            }
            finally
            {
                _idListLock.ExitWriteLock();
            }
        }

        private async Task SyncValues(bool initialRequest)
        {
            var response = await _requestDispatcher.Fetch(
                "download_config_specs",
                new Dictionary<string, object>
                {
                    ["sinceTime"] = _lastSyncTime,
                    ["statsigMetadata"] = SDKDetails.GetServerSDKDetails().StatsigMetadata
                }
            );

            if (response != null)
            {
                ParseResponse(response, initialRequest);
            }
        }

        private void ParseResponse(IReadOnlyDictionary<string, JToken> response, bool initialRequest)
        {
            JToken time;
            _lastSyncTime = response.TryGetValue("time", out time) ? time.Value<double>() : _lastSyncTime;

            if (!initialRequest)
            {
                if (!response.TryGetValue("has_updates", out var hasUpdates) || !hasUpdates.Value<bool>())
                {
                    return;
                }
            }

            var newGates = new Dictionary<string, ConfigSpec>();
            var newConfigs = new Dictionary<string, ConfigSpec>();
            try
            {
                JToken objVal;
                if (response.TryGetValue("feature_gates", out objVal))
                {
                    var gates = objVal.ToObject<JObject[]>();
                    foreach (JObject gate in gates)
                    {
                        var gateSpec = ConfigSpec.FromJObject(gate);
                        newGates.Add(gateSpec.Name.ToLowerInvariant(), ConfigSpec.FromJObject(gate));
                    }
                }
            }
            catch
            {
                // TODO: Log this
                return;
            }

            try
            {
                JToken objVal;
                if (response.TryGetValue("dynamic_configs", out objVal))
                {
                    var configs = objVal.ToObject<JObject[]>();
                    foreach (JObject config in configs)
                    {
                        var configSpec = ConfigSpec.FromJObject(config);
                        newConfigs.Add(configSpec.Name.ToLowerInvariant(), ConfigSpec.FromJObject(config));
                    }
                }
            }
            catch
            {
                // TODO: Log this
                return;
            }

            FeatureGates = newGates;
            DynamicConfigs = newConfigs;

            try
            {
                JToken objVal;
                if (response.TryGetValue("id_lists", out objVal))
                {
                    var lists = objVal.ToObject<Dictionary<string, bool>>();
                    _idListLock.EnterWriteLock();
                    try
                    {
                        foreach (string listName in _idLists.Keys)
                        {
                            if (!lists.ContainsKey(listName))
                            {
                                _idLists.Remove(listName);
                            }
                        }
                        foreach (KeyValuePair<string, bool> entry in lists)
                        {
                            if (!_idLists.ContainsKey(entry.Key))
                            {
                                _idLists.Add(entry.Key, new IDList(entry.Key));
                            }
                        }
                    }
                    finally
                    {
                        _idListLock.ExitWriteLock();
                    }
                }
            }
            catch
            {
                // TODO: Log this
                return;
            }
        }
    }
}
