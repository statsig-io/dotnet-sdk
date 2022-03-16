using Newtonsoft.Json.Linq;
using Statsig.Network;
using System;
using System.Collections.Generic;
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
        internal Dictionary<string, IDList> IDLists { get; private set; }

        internal SpecStore(string serverSecret, StatsigOptions options)
        {
            _requestDispatcher = new RequestDispatcher(serverSecret, options.ApiUrlBase);
            _lastSyncTime = 0;
            FeatureGates = new Dictionary<string, ConfigSpec>();
            DynamicConfigs = new Dictionary<string, ConfigSpec>();
            IDLists = new Dictionary<string, IDList>();

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
            var tasks = new List<Task<IReadOnlyDictionary<string, JToken>>>();
            foreach (KeyValuePair<string, IDList> entry in IDLists)
            {
                var list = entry.Value;
                tasks.Add(
                    _requestDispatcher.Fetch("download_id_list", new Dictionary<string, object>
                    {
                        ["listName"] = entry.Key,
                        ["sinceTime"] = list.Time,
                        ["statsigMetadata"] = SDKDetails.GetServerSDKDetails().StatsigMetadata
                    }));
            }
            await Task.WhenAll(tasks);
            foreach (Task<IReadOnlyDictionary<string, JToken>> task in tasks)
            {
                try
                {
                    if (task == null || task.Result == null)
                    {
                        continue;
                    }
                    var response = task.Result;
                    JToken name, time, addIDsToken, removeIDsToken;
                    var listName = response.TryGetValue("list_name", out name) ? name.Value<string>() : "";
                    if (IDLists.ContainsKey(listName))
                    {
                        var list = IDLists[listName];
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
                catch
                {
                    // TODO: log this
                }
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
                    foreach (string listName in IDLists.Keys)
                    {
                        if (!lists.ContainsKey(listName))
                        {
                            IDLists.Remove(listName);
                        }
                    }
                    foreach (KeyValuePair<string, bool> entry in lists)
                    {
                        if (!IDLists.ContainsKey(entry.Key))
                        {
                            IDLists.Add(entry.Key, new IDList(entry.Key));
                        }
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
