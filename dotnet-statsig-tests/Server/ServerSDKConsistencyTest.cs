﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Xunit;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Statsig;
using Statsig.Server;
using System.Threading.Tasks;

namespace dotnet_statsig_tests
{
    internal class LayerWithExposures : Layer
    {
        [JsonProperty("secondary_exposures")]
        public List<IReadOnlyDictionary<string, string>> SecondaryExposures { get; }

        [JsonProperty("undelegated_secondary_exposures")]
        public List<IReadOnlyDictionary<string, string>> UndelegatedSecondaryExposures { get; }

        public LayerWithExposures(
            string name = null,
            IReadOnlyDictionary<string, JToken> value = null,
            string ruleID = null, Action<Layer, string> onExposure = null,
            List<IReadOnlyDictionary<string, string>> secondaryExposures = null,
            List<IReadOnlyDictionary<string, string>> undelegatedSecondaryExposures = null) : base(name, value, ruleID, onExposure)
        {
            SecondaryExposures = secondaryExposures ?? new List<IReadOnlyDictionary<string, string>>();
            UndelegatedSecondaryExposures = undelegatedSecondaryExposures ?? new List<IReadOnlyDictionary<string, string>>();
        }
    }

    public class ServerSDKConsistencyTest : IAsyncLifetime
    {
        private class TestData
        {
            public StatsigUser user { get; set; }
            public Dictionary<string, FeatureGate> feature_gates_v2 { get; set; }
            public Dictionary<string, DynamicConfig> dynamic_configs { get; set; }
            public Dictionary<string, LayerWithExposures> layer_configs { get; set; }
        }

        string secret;

        public Task InitializeAsync()
        {
            try
            {
                secret = Environment.GetEnvironmentVariable("test_api_key");
            }
            catch
            {
                throw new InvalidOperationException("THIS TEST IS EXPECTED TO FAIL FOR NON-STATSIG EMPLOYEES! If this is the only test failing, please proceed to submit a pull request. If you are a Statsig employee, chat with jkw.");
            }

            return Task.CompletedTask;
        }

        public Task DisposeAsync() { return Task.CompletedTask; }

        [Fact]
        public async void TestProd()
        {
            await TestConsistency("https://statsigapi.net/v1");
        }

        [Fact]
        public async void TestStaging()
        {
            await TestConsistency("https://staging.statsigapi.net/v1");
        }


        private async Task<TestData[]> FetchTestData(string apiURLBase)
        {
            using (HttpClient client = new HttpClient())
            {
                var httpRequestMessage = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri(apiURLBase + "/rulesets_e2e_test"),
                    Headers = {
                        { "STATSIG-API-KEY", secret },
                        { "STATSIG-CLIENT-TIME", (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds.ToString() }
                    },
                    Content = new StringContent("")
                };

                var response = client.SendAsync(httpRequestMessage).Result;
                string result = await response.Content.ReadAsStringAsync();
                var testData = JsonConvert.DeserializeObject<Dictionary<string, TestData[]>>(result);
                return testData["data"];
            }
        }

        private async Task TestConsistency(string apiURLBase)
        {
            var driver = new ServerDriver(secret, new StatsigOptions(apiURLBase));
            await driver.Initialize();
            var testData = await FetchTestData(apiURLBase);
            foreach (var data in testData)
            {
                foreach (var gate in data.feature_gates_v2)
                {
                    var sdkResult = driver.evaluator.CheckGate(data.user, gate.Key);
                    var sdkGateResult = sdkResult.GateValue;
                    var serverResult = gate.Value;
                    Assert.True(sdkGateResult.Value == serverResult.Value, string.Format("Values are different for gate {0}. Expected {1} but got {2}", gate.Key, serverResult.Value, sdkGateResult.Value));
                    Assert.True(sdkGateResult.RuleID == serverResult.RuleID, string.Format("Rule IDs are different for gate {0}. Expected {1} but got {2}", gate.Key, serverResult.RuleID, sdkGateResult.RuleID));
                    Assert.True(compareSecondaryExposures(sdkGateResult.SecondaryExposures, serverResult.SecondaryExposures),
                        string.Format("Secondary exposures are different for gate {0}. Expected {1} but got {2}", gate.Key, stringifyExposures(serverResult.SecondaryExposures), stringifyExposures(sdkGateResult.SecondaryExposures)));
                }

                foreach (var config in data.dynamic_configs)
                {
                    var sdkResult = driver.evaluator.GetConfig(data.user, config.Key);
                    var sdkConfigResult = sdkResult.ConfigValue;
                    var serverResult = config.Value;
                    foreach (var entry in sdkConfigResult.Value)
                    {
                        Assert.True(JToken.DeepEquals(entry.Value, serverResult.Value[entry.Key]),
                            string.Format("Values are different for config {0}.", config.Key));
                    }
                    Assert.True(sdkConfigResult.RuleID == serverResult.RuleID, string.Format("Rule IDs are different for config {0}. Expected {1} but got {2}", config.Key, serverResult.RuleID, sdkConfigResult.RuleID));
                    Assert.True(compareSecondaryExposures(sdkConfigResult.SecondaryExposures, serverResult.SecondaryExposures),
                        string.Format("Secondary exposures are different for config {0}. Expected {1} but got {2}", config.Key, stringifyExposures(serverResult.SecondaryExposures), stringifyExposures(sdkConfigResult.SecondaryExposures)));
                }

                foreach (var layer in data.layer_configs)
                {
                    var sdkResult = driver.evaluator.GetLayer(data.user, layer.Key);
                    var sdkConfigResult = sdkResult.ConfigValue;
                    var serverResult = layer.Value;
                    foreach (var entry in sdkConfigResult.Value)
                    {
                        Assert.True(JToken.DeepEquals(entry.Value, serverResult.Value[entry.Key]),
                            string.Format("Values are different for config {0}.", layer.Key));
                    }
                    Assert.True(sdkConfigResult.RuleID == serverResult.RuleID, string.Format("Rule IDs are different for config {0}. Expected {1} but got {2}", layer.Key, serverResult.RuleID, sdkConfigResult.RuleID));
                    Assert.True(compareSecondaryExposures(sdkConfigResult.SecondaryExposures, serverResult.SecondaryExposures),
                        string.Format("Secondary exposures are different for config {0}. Expected {1} but got {2}", layer.Key, stringifyExposures(serverResult.SecondaryExposures), stringifyExposures(sdkConfigResult.SecondaryExposures)));
                    Assert.True(compareSecondaryExposures(sdkResult.UndelegatedSecondaryExposures, serverResult.UndelegatedSecondaryExposures),
                        string.Format("Undelegated Secondary exposures are different for config {0}. Expected {1} but got {2}", layer.Key, stringifyExposures(serverResult.UndelegatedSecondaryExposures), stringifyExposures(sdkResult.UndelegatedSecondaryExposures)));
                }
            }
            await driver.Shutdown();
        }

        private bool compareSecondaryExposures(List<IReadOnlyDictionary<string, string>> exposures1, List<IReadOnlyDictionary<string, string>> exposures2)
        {
            if (exposures1 == null)
            {
                exposures1 = new List<IReadOnlyDictionary<string, string>>();
            }
            if (exposures2 == null)
            {
                exposures2 = new List<IReadOnlyDictionary<string, string>>();
            }

            if (exposures1.Count != exposures2.Count)
            {
                return false;
            }

            var exposures2Lookup = new Dictionary<string, IReadOnlyDictionary<string, string>>();
            foreach (var expo in exposures2)
            {
                exposures2Lookup[expo["gate"]] = expo;
            }

            foreach (var expo in exposures1)
            {
                if (exposures2Lookup.TryGetValue(expo["gate"], out IReadOnlyDictionary<string, string> expo2))
                {
                    if (expo["gateValue"] != expo2["gateValue"] || expo["ruleID"] != expo2["ruleID"])
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private string stringifyExposures(List<IReadOnlyDictionary<string, string>> exposures)
        {
            if (exposures == null || exposures.Count == 0)
            {
                return "";
            }
            var res = "[ \n";
            foreach (var expo in exposures)
            {
                res += string.Format("Name: {0} \n Value: {1} \n Rule ID: {2}", expo["gate"], expo["gateValue"], expo["ruleID"]);
            }
            res += "\n ] \n";
            return res;
        }
    }
}