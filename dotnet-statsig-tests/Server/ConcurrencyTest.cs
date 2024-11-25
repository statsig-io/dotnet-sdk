using System.Collections.Generic;
using Xunit;
using WireMock.Server;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using System.Threading.Tasks;
using WireMock;
using WireMock.Settings;
using Statsig;
using Statsig.Server;
using Newtonsoft.Json.Linq;
using WireMock.ResponseProviders;

namespace dotnet_statsig_tests
{
    [Collection("Statsig Singleton Tests")]
    public class ConcurrencyTest : IAsyncLifetime, IResponseProvider
    {
        WireMockServer _server;
        string _baseURL;
        int _flushedEventCount = 0;
        int getIDListCount = 0;
        int list1Count = 0;

        Task IAsyncLifetime.InitializeAsync()
        {
            _server = WireMockServer.Start();
            _baseURL = _server.Urls[0];
            _server.ResetLogEntries();
            _server.Given(
                Request.Create().WithPath("/v1/download_config_specs/secret-server-key.json").UsingGet()
            ).RespondWith(this);
            _server.Given(
                Request.Create().WithPath("/v1/log_event").UsingPost()
            ).RespondWith(this);
            _server.Given(
                Request.Create().WithPath("/v1/get_id_lists").UsingPost()
            ).RespondWith(this);
            _server.Given(
                Request.Create().WithPath("/list_1").UsingAnyMethod()
            ).RespondWith(this);

            return Task.CompletedTask;
        }

        Task IAsyncLifetime.DisposeAsync()
        {
            _server.Stop();
            return Task.CompletedTask;
        }

        // IResponseProvider
        public async Task<(ResponseMessage Message, IMapping Mapping)> ProvideResponseAsync(RequestMessage requestMessage, IWireMockServerSettings settings)
        {
            if (requestMessage.AbsolutePath.Contains("/v1/download_config_specs"))
            {
                return await Response.Create()
                    .WithStatusCode(200)
                    .WithBody(SpecStoreResponseData.downloadConfigSpecResponse)
                    .ProvideResponseAsync(requestMessage, settings);
            }

            if (requestMessage.AbsolutePath.Contains("/v1/get_id_lists"))
            {
                getIDListCount++;
                var url = _baseURL + "/list_1";
                var body = $@"{{
                    'list_1': {{
                        'name': 'list_1',
                        'size': {3 * getIDListCount},
                        'url': '{url}',
                        'creationTime': 1,
                        'fileID': 'file_id_1',
                    }},
                }}";

                return await Response.Create()
                    .WithStatusCode(200)
                    .WithBody(body)
                    .ProvideResponseAsync(requestMessage, settings);
            }

            if (requestMessage.AbsolutePath.Contains("/v1/log_event"))
            {
                var body = (requestMessage.BodyAsJson as JObject);
                _flushedEventCount += ((JArray)body["events"]).ToObject<List<JObject>>().Count;
                return await Response.Create()
                    .WithStatusCode(200)
                    .ProvideResponseAsync(requestMessage, settings);
            }

            if (requestMessage.AbsolutePath.Contains("/list_1"))
            {
                var body = "+7/rrkvF6\n";
                list1Count++;
                if (list1Count > 1)
                {
                    body = string.Format("+{0}\n-{0}\n", list1Count);
                }
                return await Response.Create()
                    .WithStatusCode(200)
                    .WithBody(body)
                    .ProvideResponseAsync(requestMessage, settings);
            }

            return await Response.Create()
                .WithStatusCode(404)
                .ProvideResponseAsync(requestMessage, settings);
        }

        [Fact]
        public async void TestCallingAPIsConcurrently()
        {
            await Start();
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(RunChecks(i, 10, 20));
            }
            await Task.WhenAll(tasks);

            await StatsigServer.Shutdown();
            // make sure we ultimately flushed exactly 1800 events (10 threads x 20 loops/thread x 9 events/loop)
            Assert.Equal(1800, _flushedEventCount);
        }

        private async Task RunChecks(int taskId, int delay, int times)
        {
            for (int i = 0; i < times; i++)
            {
                var user = new StatsigUser
                {
                    UserID = $"user_id_{i}_{taskId}",
                    Email = "testuser@statsig.com",
                };
                user.AddCustomProperty("key", "value");

                StatsigServer.LogEvent(user, "test_event", 1, new Dictionary<string, string>() { { "Key", "Value" } });

                Assert.True(await StatsigServer.CheckGate(user, "on_for_statsig_email"));

                Assert.True(await StatsigServer.CheckGate(user, "always_on_gate"));

                // check id list gate for a user that should be in the id list
                Assert.True(await StatsigServer.CheckGate(new StatsigUser { UserID = "regular_user_id", customIDs = { { "unique_id", $"{i}_{taskId}" } } }, "on_for_id_list"));

                StatsigServer.LogEvent(user, "test_event_2", 1, new Dictionary<string, string>() { { "Key", "Value" } });

                var expParam =
                    (await StatsigServer.GetExperiment(user, "sample_experiment")).Get("experiment_param",
                        "default");
                Assert.True(expParam == "test" || expParam == "control");

                StatsigServer.LogEvent(user, "test_event_3", 1, new Dictionary<string, string>() { { "Key", "Value" } });

                var config = await StatsigServer.GetConfig(user, "test_config");
                Assert.Equal(7, config.Get("number", 0));

                var layer = await StatsigServer.GetLayer(user, "a_layer");
                Assert.True(layer.Get("layer_param", false));

                await Task.Delay(delay);
            }
        }

        private async Task Start()
        {
            await StatsigServer.Initialize(
               "secret-server-key",
               new StatsigOptions(_server.Urls[0] + "/v1") { RulesetsSyncInterval = 0.01, IDListsSyncInterval = 0.01 }
           );
        }
    }
}
