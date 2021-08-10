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

        public async Task InitializeAsync()
        {
            try
            {
                secret = Environment.GetEnvironmentVariable("test_api_key");
                if (string.IsNullOrEmpty(secret))
                {
                    secret = File.ReadAllText("../../../../../ops/secrets/prod_keys/statsig-rulesets-eval-consistency-test-secret.key");   
                }
            }
            catch
            {
                throw new InvalidOperationException("THIS TEST IS EXPECTED TO FAIL FOR NON-STATSIG EMPLOYEES! If this is the only test failing, please proceed to submit a pull request. If you are a Statsig employee, chat with jkw.");
            }
        }

        public async Task DisposeAsync() {}

        [Fact]
        public async void TestProd()
        {
            await TestConsistency("https://api.statsig.com/v1");
        }

        [Fact]
        public async void TestStaging()
        {
            await TestConsistency("https://latest.api.statsig.com/v1");
        }

        [Fact]
        public async void TestAPSouth()
        {
            await TestConsistency("https://ap-south-1.api.statsig.com/v1");
        }

        [Fact]
        public async void TestUSWest()
        {
            await TestConsistency("https://us-west-2.api.statsig.com/v1");
        }

        [Fact]
        public async void TestUSEast()
        {
            await TestConsistency("https://us-east-2.api.statsig.com/v1");
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
                foreach (var gate in data.feature_gates)
                {
                    var sdkValue = await driver.CheckGate(data.user, gate.Key);
                    Assert.True(sdkValue == gate.Value, gate.Key + "  expected " + gate.Value + " got " + sdkValue + "for " + gate.Key);
                }
                foreach (var config in data.dynamic_configs)
                {
                    var sdkValue = await driver.GetConfig(data.user, config.Key);
                    foreach (var entry in sdkValue.Value)
                    {
                        Assert.True(JToken.DeepEquals(entry.Value, config.Value.Value[entry.Key]));
                    }
                }
            }
            driver.Shutdown();
        }
    }
}