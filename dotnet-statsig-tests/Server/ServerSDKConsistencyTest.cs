using System;
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
    public class ServerSDKConsistencyTest : IAsyncLifetime
    {
        private class TestData
        {
            public StatsigUser user { get; set; }
            public Dictionary<string, bool> feature_gates { get; set; }
            public Dictionary<string, DynamicConfig> dynamic_configs { get; set; }
        }

        string secret;
        TestData[] prodTestData;
        TestData[] stagingTestData;

        public async Task InitializeAsync()
        {
            try
            {
                secret = Environment.GetEnvironmentVariable("test_api_key");
                if (string.IsNullOrEmpty(secret))
                {
                    secret = File.ReadAllText("../../../../../ops/secrets/prod_keys/statsig-rulesets-eval-consistency-test-secret.key");   
                }
                prodTestData = await FetchTestData("https://api.statsig.com/v1/rulesets_e2e_test");
                stagingTestData = await FetchTestData("https://latest.api.statsig.com/v1/rulesets_e2e_test");

                await StatsigServer.Initialize(secret);
            }
            catch
            {
                throw new InvalidOperationException("THIS TEST IS EXPECTED TO FAIL FOR NON-STATSIG EMPLOYEES! If this is the only test failing, please proceed to submit a pull request. If you are a Statsig employee, chat with jkw.");
            }
        }

        public async Task DisposeAsync() {}

        [Fact]
        public async void TestProdConsistency()
        {
            await TestConsistency(prodTestData);
        }

        [Fact]
        public async void TestStagingConsistency()
        {
            await TestConsistency(stagingTestData);
        }

        private async Task<TestData[]> FetchTestData(string url)
        {
            using (HttpClient client = new HttpClient())
            {
                var httpRequestMessage = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri(url),
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

        private async Task TestConsistency(TestData[] testData)
        {
            foreach (var data in testData)
            {
                foreach (var gate in data.feature_gates)
                {
                    var sdkValue = await StatsigServer.CheckGate(data.user, gate.Key);
                    Assert.True(sdkValue == gate.Value, gate.Key + "  expected " + gate.Value + " got " + sdkValue + "for " + gate.Key);
                }
                foreach (var config in data.dynamic_configs)
                {
                    var sdkValue = await StatsigServer.GetConfig(data.user, config.Key);
                    foreach (var entry in sdkValue.Value)
                    {
                        Assert.True(JToken.DeepEquals(entry.Value, config.Value.Value[entry.Key]));
                    }
                }
            }
        }
    }
}