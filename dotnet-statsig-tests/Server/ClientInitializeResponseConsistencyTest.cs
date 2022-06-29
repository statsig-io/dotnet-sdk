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

    public class ClientInitializeResponseConsistencyTest : IAsyncLifetime
    {
        StatsigUser _user = new()
        {
            UserID = "123",
            email: 'test@statsig.com',
            country: 'US',
            custom: {
            test: '123',
            },
            customIDs: {
                stableID: '12345',
            },
        };
        private class InitializeResponse
        {
            public Dictionary<string, FeatureGate> feature_gates { get; set; }
            public Dictionary<string, DynamicConfig> dynamic_configs { get; set; }
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
            await TestConsistency("https://statsigapi.net/v1");
        }

        private async Task<InitializeResponse> FetchTestData(string apiURLBase)
        {
            using (HttpClient client = new HttpClient())
            {
                 var body = new Dictionary<string, string>();
                 body.Add("user", _user.GetCopyForLogging());
                 var bodyJson = JsonConvert.SerializeObject(body);
                 var httpRequestMessage = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri(apiURLBase + "/initialize"),
                    Headers = {
                        { "STATSIG-API-KEY", secret },
                        { "STATSIG-CLIENT-TIME", (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds.ToString() }
                    },
                    Content = new StringContent("")
                };

                var response = client.SendAsync(httpRequestMessage).Result;
                string result = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<InitializeResponse>(result);
            }
        }

        private async Task TestConsistency(string apiURLBase)
        {
            var driver = new ServerDriver(secret, new StatsigOptions(apiURLBase));
            await driver.Initialize();
            var initializeResponse = await FetchTestData(apiURLBase);
            var sdkResponse = await driver.GenerateInitializeResponse(_user);
            foreach (var gate in initializeResponse.feature_gates)
            {
                sdkResult = sdkResponse.Get("feature_gates").get(gate.Name);
                Assert.True(sdkResult.Value == gate.Value, string.Format("Values are different for gate {0}. Expected {1} but got {2}", gate.Name, serverResult.Value, sdkGateResult.Value));
                
            }
            await driver.Shutdown();
        }
    }
}