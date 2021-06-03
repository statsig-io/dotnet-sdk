using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json.Linq;
using Statsig.Network;

namespace Statsig.src.Statsig.Server
{
    class SpecStore
    {
        double _lastSyncTime;
        RequestDispatcher _requestDispatcher;
        Timer _syncTimer;

        internal Dictionary<string, ConfigSpec> FeatureGates { get; set; }
        internal Dictionary<string, ConfigSpec> DynamicConfigs { get; set; }

        internal SpecStore(string serverSecret, ConnectionOptions options)
        {
            _requestDispatcher = new RequestDispatcher(serverSecret, options.ApiUrlBase);
            _lastSyncTime = 0;
            _syncTimer = new Timer();
            FeatureGates = new Dictionary<string, ConfigSpec>();
            DynamicConfigs = new Dictionary<string, ConfigSpec>();
        }

        internal async Task Initialize()
        {
            await SyncValues();
        }

        internal void Shutdown()
        {
            _syncTimer.Stop();
            _syncTimer.Dispose();
        }

        private async Task SyncValues()
        {
            var response = await _requestDispatcher.Fetch(
                "download_config_specs",
                new Dictionary<string, object>
                {
                    ["sinceTime"] = _lastSyncTime,
                    ["statsigMetadata"] = ServerUtils.GetStatsigMetadata(),
                }
            );

            if (response != null)
            {
                ParseResponse(response);
            }

            _syncTimer.Interval = Constants.SERVER_CONFIG_SPECS_SYNC_INTERVAL_IN_SEC * 1000;
            _syncTimer.Enabled = true;
            _syncTimer.AutoReset = false;
            _syncTimer.Elapsed += async (sender, e) => await SyncValues();
        }

        private void ParseResponse(IReadOnlyDictionary<string, JToken> response)
        {
            JToken time;
            _lastSyncTime = response.TryGetValue("time", out time) ? time.Value<double>() : _lastSyncTime;

            if (!response.TryGetValue("has_updates", out var hasUpdates) || !hasUpdates.Value<bool>())
            {
                return;
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
        }
    }
}
