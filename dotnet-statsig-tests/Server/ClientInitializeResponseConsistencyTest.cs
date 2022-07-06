using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
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
        private readonly StatsigUser _user = JsonConvert.DeserializeObject<StatsigUser>(@"{
            'userID': '123',
            'email': 'test@statsig.com',
            'country': 'US',
            'custom': {'test': '123'},
            'customIDs': {'stableID': '12345'}
        }");

        private readonly string _serverKey = Environment.GetEnvironmentVariable("test_api_key");
        private readonly string _clientKey = Environment.GetEnvironmentVariable("test_client_key");

        public Task InitializeAsync()
        {
            if (string.IsNullOrEmpty(_serverKey) || string.IsNullOrEmpty(_clientKey))
            {
                throw new InvalidOperationException(
                    "THIS TEST IS EXPECTED TO FAIL FOR NON-STATSIG EMPLOYEES! If this is the only test failing, please proceed to submit a pull request. If you are a Statsig employee, chat with jkw.");
            }

            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

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

        private async Task<string> FetchTestData(string apiUrlBase)
        {
            using var client = new HttpClient();
            var body = new Dictionary<string, object>
            {
                { "user", _user.GetCopyForLogging() },
                {
                    "statsigMetadata",
                    new Dictionary<string, object> { { "sdkType", "consistency-test" }, { "sessionID", "x123" } }
                }
            };

            var httpRequestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(apiUrlBase + "/initialize"),
                Headers =
                {
                    { "STATSIG-API-KEY", _clientKey },
                    { "STATSIG-CLIENT-TIME", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() }
                },
                Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json")
            };

            var response = client.SendAsync(httpRequestMessage).Result;
            return await response.Content.ReadAsStringAsync();
        }

        private async Task TestConsistency(string apiUrlBase)
        {
            var driver = new ServerDriver(_serverKey, new StatsigOptions(apiUrlBase));
            await driver.Initialize();
            var serverResponse = await FetchTestData(apiUrlBase);
            var sdkResponse = JsonConvert.SerializeObject(driver.GenerateInitializeResponse(_user));

            SanitizeResponse(ref serverResponse);
            SanitizeResponse(ref sdkResponse);

            var serverJToken = JToken.Parse(serverResponse);
            var sdkJToken = JToken.Parse(sdkResponse);

            Assert.True(JToken.DeepEquals(serverJToken, sdkJToken));

            await driver.Shutdown();
        }

        private static void SanitizeResponse(ref string response)
        {
            RemoveGateExposureFields(ref response);
            RemoveGeneratorField(ref response);
        }

        private static void RemoveGateExposureFields(ref string input)
        {
            input = Regex.Replace(input, "\"gate\":\".+?\",*", "");
        }

        private static void RemoveGeneratorField(ref string input)
        {
            input = Regex.Replace(input, "\"generator\":\".+?\",*", "");
        }
    }
}