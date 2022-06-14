using Newtonsoft.Json.Linq;
using Statsig.Network;
using Statsig.Lib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;

namespace Statsig.Server
{
    class SpecStore
    {
        private readonly RequestDispatcher _requestDispatcher = null;

        private Task _syncIDListsTask;
        private Task _syncValuesTask;
        private readonly CancellationTokenSource _cts = null;

        internal long LastSyncTime { get; private set; }
        internal Dictionary<string, ConfigSpec> FeatureGates { get; private set; }
        internal Dictionary<string, ConfigSpec> DynamicConfigs { get; private set; }
        internal Dictionary<string, ConfigSpec> LayerConfigs { get; private set; }
        internal Dictionary<string, IReadOnlyCollection<string>> LayersMap { get; private set; }
        internal readonly ConcurrentDictionary<string, IDList> _idLists;
        private double _idListsSyncInterval;
        private double _rulesetsSyncInterval;
        private Func<IIDStore> _idStoreFactory;

        internal SpecStore(string serverSecret, StatsigOptions options)
        {
            _idStoreFactory = options.IDStoreFactory;
            _requestDispatcher = new RequestDispatcher(serverSecret, options.ApiUrlBase, options.AdditionalHeaders);
            _idListsSyncInterval = options.IDListsSyncInterval;
            _rulesetsSyncInterval = options.RulesetsSyncInterval;
            LastSyncTime = 0;
            FeatureGates = new Dictionary<string, ConfigSpec>();
            DynamicConfigs = new Dictionary<string, ConfigSpec>();
            LayerConfigs = new Dictionary<string, ConfigSpec>();
            LayersMap = new Dictionary<string, IReadOnlyCollection<string>>();
            _idLists = new ConcurrentDictionary<string, IDList>();

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
            if (_cts != null)
            {
                _cts.Cancel();
            }
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
            return _idLists.TryGetValue(listName, out var list) && list.Store.Contains(value);
        }

        private async Task BackgroundPeriodicSyncIDListsTask(CancellationToken cancellationToken)
        {
            var delayInterval = TimeSpan.FromSeconds(_idListsSyncInterval);
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
            var delayInterval = TimeSpan.FromSeconds(_rulesetsSyncInterval);
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

        private async Task DownloadIDList(IDList list)
        {
            try
            {
                var client = new HttpClient();
                using (var request = new HttpRequestMessage(HttpMethod.Get, list.URL))
                {
                    request.Headers.Add("Range", string.Format("bytes={0}-", list.Size));
                    var response = await client.SendAsync(request);
                    if (response == null)
                    {
                        return;
                    }
            
                    if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            await response.Content.CopyToAsync(memoryStream);
                            var contentLength = memoryStream.Length;
                            memoryStream.Seek(0, SeekOrigin.Begin);

                            using (var reader = new StreamReader(memoryStream))
                            {
                                var next = reader.Peek();
                                if (next < 0 || (((char)next) != '+' && ((char)next) != '-'))
                                {
                                    IDList removed;
                                    if (_idLists.TryRemove(list.Name, out removed))
                                    {
                                        removed.Dispose();
                                    }
                                    return;
                                }

                                string line;
                                while ((line = reader.ReadLine()) != null)
                                {
                                    if (string.IsNullOrEmpty(line))
                                    {
                                        continue;
                                    }
                                    var id = line.Substring(1);
                                    if (line[0] == '+')
                                    {
                                        list.Store.Add(id);
                                    }
                                    else if (line[0] == '-')
                                    {
                                        list.Store.Remove(id);
                                    }
                                }

                                list.Store.TrimExcess();
                                list.Size = list.Size + contentLength;
                            }
                        }
                    }
                }
            }
            catch
            {
                // unexpected error, will retry next time
            }
        }

        private async Task AddServerIDList(string listName, IDList serverList)
        {
            _idLists.TryGetValue(listName, out var localList);

            // reset local id list if it doesn't exist, or if server list is a newer file by creation time
            if (localList == null || (serverList.FileID != localList.FileID && serverList.CreationTime >= localList.CreationTime))
            {
                localList = new IDList
                {
                    Name = listName,
                    Size = 0,
                    URL = serverList.URL,
                    CreationTime = serverList.CreationTime,
                    FileID = serverList.FileID
                };
                if (_idStoreFactory != null)
                {
                    localList.Store = _idStoreFactory();
                }
                _idLists[listName] = localList;
            }

            // skip the list if url or fileID is invalid, or if the list was an old one
            if (string.IsNullOrEmpty(serverList.URL) ||
                string.IsNullOrEmpty(serverList.FileID) ||
                serverList.CreationTime < localList.CreationTime)
            {
                return;
            }

            // skip if server list is not bigger
            if (serverList.Size <= localList.Size)
            {
                return;
            }

            await DownloadIDList(localList);
        }

        private async Task SyncIDLists(IReadOnlyDictionary<string, JToken> idListMap)
        {
            var tasks = new List<Task>();
            foreach (var entry in idListMap)
            {
                try
                {
                    var serverList = entry.Value.ToObject<IDList>();
                    if (serverList != null)
                    {
                        if (_idStoreFactory != null)
                        {
                            serverList.Store = _idStoreFactory();
                        }
                        tasks.Add(this.AddServerIDList(entry.Key, serverList));
                    }
                }
                catch
                {
                    // Ignore malformed lists
                }
            }
            await Task.WhenAll(tasks);

            var deletedLists = new List<string>();
            foreach (var listName in _idLists.Keys)
            {
                if (!idListMap.ContainsKey(listName))
                {
                    deletedLists.Add(listName);
                }
            }
            foreach (var listName in deletedLists)
            {
                IDList removed;
                if (_idLists.TryRemove(listName, out removed))
                {
                    removed.Dispose();
                }
            }
        }

        private async Task SyncIDLists()
        {
            var response = await _requestDispatcher.Fetch("get_id_lists", new Dictionary<string, object>
            {
                ["statsigMetadata"] = SDKDetails.GetServerSDKDetails().StatsigMetadata
            });
            if (response == null || response.Count == 0)
            {
                return;
            }

            await SyncIDLists(response);
        }

        private async Task SyncValues(bool initialRequest)
        {
            var response = await _requestDispatcher.Fetch(
                "download_config_specs",
                new Dictionary<string, object>
                {
                    ["sinceTime"] = LastSyncTime,
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
            LastSyncTime = response.TryGetValue("time", out time) ? time.Value<long>() : LastSyncTime;

            if (!initialRequest)
            {
                if (!response.TryGetValue("has_updates", out var hasUpdates) || !hasUpdates.Value<bool>())
                {
                    return;
                }
            }

            var newGates = new Dictionary<string, ConfigSpec>();
            var newConfigs = new Dictionary<string, ConfigSpec>();
            var newLayerConfigs = new Dictionary<string, ConfigSpec>();
            var newLayersMap = new Dictionary<string, IReadOnlyCollection<string>>();
            JToken objVal;
            if (response.TryGetValue("feature_gates", out objVal))
            {
                var gates = objVal.ToObject<JObject[]>();
                foreach (JObject gate in gates)
                {
                    try
                    {
                        var gateSpec = ConfigSpec.FromJObject(gate);
                        newGates[gateSpec.Name.ToLowerInvariant()] = gateSpec;
                    }
                    catch
                    {
                        // Skip
                    }
                }
            }
            
            if (response.TryGetValue("dynamic_configs", out objVal))
            {
                var configs = objVal.ToObject<JObject[]>();
                foreach (JObject config in configs)
                {
                    try
                    {
                        var configSpec = ConfigSpec.FromJObject(config);
                        newConfigs[configSpec.Name.ToLowerInvariant()] = configSpec;
                    }
                    catch
                    {
                        // Skip
                    }
                }
            }
            
            if (response.TryGetValue("layer_configs", out objVal))
            {
                var configs = objVal.ToObject<JObject[]>();
                foreach (JObject config in configs)
                {
                    try
                    {
                        var configSpec = ConfigSpec.FromJObject(config);
                        newLayerConfigs[configSpec.Name.ToLowerInvariant()] = configSpec;
                    }
                    catch
                    {
                        // Skip
                    }
                }
            }
            
            if (response.TryGetValue("layers", out objVal) && objVal.Type == JTokenType.Object)
            {
                try
                {
                    var jobj = objVal.Value<JObject>();
                    foreach (var prop in jobj.Properties())
                    {
                        if (prop.Value.Type == JTokenType.Array)
                        {
                            var array = prop.Value.Value<JArray>();
                            var list = new List<string>();
                            foreach (var item in array)
                            {
                                list.Add(item.Value<string>());
                            }
                            newLayersMap.Add(prop.Name, list);
                        }
                    }
                }
                catch
                {
                    // Skip all layers
                }
            }
            
            FeatureGates = newGates;
            DynamicConfigs = newConfigs;
            LayerConfigs = newLayerConfigs;
            LayersMap = newLayersMap;
        }
    }
}
