using System;
using System.Collections.Generic;
using System.Linq;

using Statsig.Client;
using Statsig.Server;
using WireMock.Server;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;
using Newtonsoft.Json.Linq;
using Statsig;
using System.Threading.Tasks;


namespace dotnet_statsig_tests
{

    [Collection("Statsig Singleton Tests")]
    public class StatsigTest : IAsyncLifetime
    {
        WireMockServer _server;

        private const String ExpectedSdkVersion = "1.27.3.0";

        Task IAsyncLifetime.InitializeAsync()
        {
            _server = WireMockServer.Start();
            return Task.CompletedTask;
        }

        Task IAsyncLifetime.DisposeAsync()
        {
            _server.Stop();
            return Task.CompletedTask;
        }

        [Fact]
        public async void TestClientInitialize()
        {
            _server.ResetLogEntries();
            _server.Given(
                Request.Create().WithPath("/v1/initialize").UsingPost()
            ).RespondWith(
                Response.Create().WithStatusCode(200).WithBodyAsJson(TestData.ClientInitializeResponse)
            );
            _server.Given(
                Request.Create().WithPath("/v1/log_event").UsingPost()
            ).RespondWith(
                Response.Create().WithStatusCode(200)
            );
            var user = new StatsigUser
            {
                UserID = "123",
                customIDs = new Dictionary<string, string> { { "random_id", "id123" } },
            };
            user.AddPrivateAttribute("value", "secret");
            user.AddCustomProperty("value", "public");
            await StatsigClient.Initialize
            (
                "client-fake-key",
                user,
                new StatsigOptions(_server.Urls[0] + "/v1")
            );
            var nowSeconds = Convert.ToInt32(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);

            Assert.Single(_server.LogEntries);

            var requestBody = _server.LogEntries.ElementAt(0).RequestMessage.Body;
            var requestHeaders = _server.LogEntries.ElementAt(0).RequestMessage.Headers;
            var requestDict = JObject.Parse(requestBody);

            JToken u, m;
            requestDict.TryGetValue("user", out u);
            requestDict.TryGetValue("statsigMetadata", out m);
            var requestUser = u.ToObject<StatsigUser>();
            var metadata = m.ToObject<Dictionary<string, string>>();

            Assert.True(requestUser.UserID == "123");
            Assert.True(requestUser.CustomProperties["value"].Equals("public"));
            Assert.Single(requestUser.CustomProperties);
            Assert.True(requestUser.CustomIDs["random_id"] == "id123");
            // Client SDK needs to send private attributes to server to evaluate gates and experiments
            Assert.True(requestUser.PrivateAttributes["value"].Equals("secret"));
            Assert.Single(requestUser.PrivateAttributes);

            Assert.True(requestHeaders["STATSIG-API-KEY"].ToString().Equals("client-fake-key"));
            Assert.True(requestHeaders["STATSIG-SDK-VERSION"].ToString().Equals(ExpectedSdkVersion));
            Assert.True(requestHeaders["STATSIG-SDK-TYPE"].ToString().Equals("dotnet-client"));

            Assert.True(metadata["sdkType"].Equals("dotnet-client"));
            Assert.True(metadata["sdkVersion"].Equals(ExpectedSdkVersion));

            Assert.True(StatsigClient.CheckGate("test_gate"));
            var exp = StatsigClient.GetExperiment("test_config");
            Assert.True(exp.Get("stringValue", "wrong").Equals("1"));
            Assert.True(exp.Get("stringValue1", "wrong").Equals("wrong"));
            Assert.True(exp.Get("numberValue", 0).Equals(1));
            Assert.True(exp.Get("numberValue1", 0).Equals(0));
            Assert.True(exp.Get("boolValue", false));
            Assert.False(exp.Get("boolValue1", false));

            StatsigClient.LogEvent("event_1");
            StatsigClient.LogEvent("event_2", 1);
            StatsigClient.LogEvent("event_3", "string");
            StatsigClient.LogEvent("event_4", null, new Dictionary<string, string> { { "key", "value" } });

            await StatsigClient.Shutdown();

            // Verify log event requets for exposures and custom logs
            requestBody = _server.LogEntries.ElementAt(1).RequestMessage.Body;
            requestHeaders = _server.LogEntries.ElementAt(1).RequestMessage.Headers;
            requestDict = JObject.Parse(requestBody);

            Assert.True(requestHeaders["STATSIG-API-KEY"].ToString().Equals("client-fake-key"));
            Assert.True(requestHeaders["STATSIG-SDK-VERSION"].ToString().Equals(ExpectedSdkVersion));
            Assert.True(requestHeaders["STATSIG-SDK-TYPE"].ToString().Equals("dotnet-client"));

            JToken e;
            requestDict.TryGetValue("events", out e);
            var events = e.ToObject<EventLog[]>();

            Assert.Equal(6, events.Count());
            var evt = events.ElementAt(0);
            Assert.True(evt.EventName == "statsig::gate_exposure");
            Assert.Null(evt.Value);
            Assert.True(evt.Metadata.GetValueOrDefault("gate", "fail").Equals("test_gate"));
            Assert.True(evt.Metadata.GetValueOrDefault("gateValue", "fail").Equals("true"));
            Assert.True(evt.Metadata.GetValueOrDefault("ruleID", "fail").Equals("ruleID"));
            Assert.True(evt.SecondaryExposures.Count() == 2);
            Assert.True(evt.SecondaryExposures.ElementAt(0).GetValueOrDefault("gate", "fail").Equals("dependent_gate_1"));
            Assert.True(evt.SecondaryExposures.ElementAt(0).GetValueOrDefault("gateValue", "fail").Equals("true"));
            Assert.True(evt.SecondaryExposures.ElementAt(0).GetValueOrDefault("ruleID", "fail").Equals("rule_1"));
            Assert.True(evt.User.UserID.Equals("123"));
            Assert.True(evt.User.CustomIDs["random_id"] == "id123");
            Assert.InRange(Convert.ToInt32(evt.Time / 1000), nowSeconds - 2, nowSeconds + 2);

            evt = events.ElementAt(1);
            Assert.True(evt.EventName == "statsig::config_exposure");
            Assert.Null(evt.Value);
            Assert.True(evt.Metadata.GetValueOrDefault("config", "fail").Equals("test_config"));
            Assert.True(evt.Metadata.GetValueOrDefault("ruleID", "fail").Equals("ruleID"));
            Assert.True(evt.SecondaryExposures.Count() == 2);
            Assert.True(evt.SecondaryExposures.ElementAt(0).GetValueOrDefault("gate", "fail").Equals("dependent_gate_1"));
            Assert.True(evt.SecondaryExposures.ElementAt(0).GetValueOrDefault("gateValue", "fail").Equals("true"));
            Assert.True(evt.SecondaryExposures.ElementAt(0).GetValueOrDefault("ruleID", "fail").Equals("rule_1"));
            Assert.True(evt.User.UserID.Equals("123"));
            Assert.True(evt.User.CustomIDs["random_id"] == "id123");
            Assert.InRange(Convert.ToInt32(evt.Time / 1000), nowSeconds - 2, nowSeconds + 2);

            evt = events.ElementAt(2);
            Assert.True(evt.EventName == "event_1");
            Assert.Null(evt.Value);
            Assert.Null(evt.Metadata);
            Assert.Null(evt.SecondaryExposures);
            Assert.True(evt.User.UserID.Equals("123"));
            Assert.True(evt.User.CustomIDs["random_id"] == "id123");
            Assert.InRange(Convert.ToInt32(evt.Time / 1000), nowSeconds - 2, nowSeconds + 2);

            evt = events.ElementAt(3);
            Assert.True(evt.EventName == "event_2");
            Assert.True(evt.Value.Equals(Convert.ToInt64(1)));
            Assert.Null(evt.Metadata);
            Assert.Null(evt.SecondaryExposures);
            Assert.True(evt.User.UserID.Equals("123"));
            Assert.InRange(Convert.ToInt32(evt.Time / 1000), nowSeconds - 2, nowSeconds + 2);

            evt = events.ElementAt(4);
            Assert.True(evt.EventName == "event_3");
            Assert.True((string)evt.Value == "string");
            Assert.Null(evt.Metadata);
            Assert.Null(evt.SecondaryExposures);
            Assert.True(evt.User.UserID.Equals("123"));
            Assert.InRange(Convert.ToInt32(evt.Time / 1000), nowSeconds - 2, nowSeconds + 2);

            evt = events.ElementAt(5);
            Assert.True(evt.EventName == "event_4");
            Assert.Null(evt.Value);
            Assert.True(evt.Metadata.GetValueOrDefault("key", "fail").Equals("value"));
            Assert.Null(evt.SecondaryExposures);
            Assert.True(evt.User.UserID.Equals("123"));
            Assert.InRange(Convert.ToInt32(evt.Time / 1000), nowSeconds - 2, nowSeconds + 2);


            // Start a new client and verify stableID is the same but sessionID changed
            var stableID = metadata["stableID"];
            var sessionID = metadata["sessionID"];

            var newClient = new ClientDriver("client-fake-key-2", new Statsig.StatsigOptions(_server.Urls[0] + "/v1"));

            await newClient.Initialize(null);
            requestBody = _server.LogEntries.ElementAt(2).RequestMessage.Body;
            requestDict = JObject.Parse(requestBody);
            requestDict.TryGetValue("statsigMetadata", out m);
            metadata = m.ToObject<Dictionary<string, string>>();
            Assert.True(metadata["stableID"].Equals(stableID));
            Assert.False(metadata["sessionID"].Equals(sessionID));
        }

        [Fact]
        public async void TestServerInitialize()
        {
            _server.ResetLogEntries();
            _server.Given(
                Request.Create().WithPath("/v1/download_config_specs").UsingPost()
            ).RespondWith(
                Response.Create().WithStatusCode(200).WithBodyAsJson(
                    new Dictionary<string, object>
                    {
                        { "has_updates", true},
                        {
                            "feature_gates", new List<Dictionary<string, object>>
                            {
                                new Dictionary<string, object>
                                {
                                    { "name", "test_gate" },
                                    { "type", "feature_gate" },
                                    { "entity", "feature_gate" },
                                    { "salt", "na" },
                                    { "defaultValue", false },
                                    { "enabled", true },
                                    {
                                        "rules", new List<Dictionary<string, object>>
                                        {
                                            new Dictionary<string, object>
                                            {
                                                { "name", "rule_1" },
                                                { "passPercentage", 100 },
                                                { "returnValue", true },
                                                { "id", "rule_id_1" },
                                                { "salt", "na" },
                                                {
                                                    "conditions", new List<Dictionary<string, object>>
                                                    {
                                                        new Dictionary<string, object>
                                                        {
                                                            { "type", "public" },
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    },
                                }
                            }
                        },
                        {
                            "dynamic_configs", new List<Dictionary<string, object>>
                            {
                                new Dictionary<string, object>
                                {
                                    { "name", "test_config" },
                                    { "type", "dynamic_config" },
                                    { "entity", "dynamic_config" },
                                    { "salt", "na" },
                                    { "defaultValue", new Dictionary<string, object> {} },
                                    { "enabled", true },
                                    {
                                        "rules", new List<Dictionary<string, object>>
                                        {
                                            new Dictionary<string, object>
                                            {
                                                { "name", "rule_1" },
                                                { "passPercentage", 100 },
                                                { "returnValue", new Dictionary<string, object> { { "stringValue", "1" }, { "numberValue", 1 }, { "boolValue", true } } },
                                                { "id", "rule_id_2" },
                                                { "salt", "na" },
                                                {
                                                    "conditions", new List<Dictionary<string, object>>
                                                    {
                                                        new Dictionary<string, object>
                                                        {
                                                            { "type", "pass_gate" },
                                                            { "targetValue", "test_gate" },
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    },
                                }
                            }
                        },
                    }
                )
            );
            _server.Given(
                Request.Create().WithPath("/v1/log_event").UsingPost()
            ).RespondWith(
                Response.Create().WithStatusCode(200)
            );
            var user = new StatsigUser
            {
                UserID = "123",
                customIDs = new Dictionary<string, string> { { "random_id", "id123" }, { "another_random_id", "id456" } },
            };
            user.AddPrivateAttribute("value", "secret");
            user.AddCustomProperty("value", "public");
            await StatsigServer.Initialize
            (
                "secret-fake-key",
                new Statsig.StatsigOptions(_server.Urls[0] + "/v1")
            );
            var nowSeconds = Convert.ToInt32(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);

            Assert.Equal(2, _server.LogEntries.Count());

            var requestBody = _server.LogEntries.ElementAt(0).RequestMessage.Body;
            var requestHeaders = _server.LogEntries.ElementAt(0).RequestMessage.Headers;
            var requestDict = JObject.Parse(requestBody);

            JToken m;
            requestDict.TryGetValue("statsigMetadata", out m);
            var metadata = m.ToObject<Dictionary<string, string>>();

            Assert.True(requestHeaders["STATSIG-API-KEY"].ToString().Equals("secret-fake-key"));
            Assert.True(requestHeaders["STATSIG-SDK-VERSION"].ToString().Equals(ExpectedSdkVersion));
            Assert.True(requestHeaders["STATSIG-SDK-TYPE"].ToString().Equals("dotnet-server"));
            
            Assert.True(metadata["sdkType"].Equals("dotnet-server"));
            Assert.True(metadata["sdkVersion"].Equals(ExpectedSdkVersion));

            var gate = await StatsigServer.CheckGate(user, "test_gate");
            Assert.True(gate);
            var exp = await StatsigServer.GetExperiment(user, "test_config");
            Assert.True(exp.Get("stringValue", "wrong").Equals("1"));
            Assert.True(exp.Get("stringValue1", "wrong").Equals("wrong"));
            Assert.True(exp.Get("numberValue", 0).Equals(1));
            Assert.True(exp.Get("numberValue1", 0).Equals(0));
            Assert.True(exp.Get("boolValue", false));
            Assert.False(exp.Get("boolValue1", false));

            StatsigServer.LogEvent(user, "event_1");
            StatsigServer.LogEvent(user, "event_2", 1);
            StatsigServer.LogEvent(user, "event_3", "string");
            StatsigServer.LogEvent(user, "event_4", null, new Dictionary<string, string> { { "key", "value" } });

            await StatsigServer.Shutdown();

            // Verify log event requets for exposures and custom logs
            requestBody = _server.LogEntries.ElementAt(2).RequestMessage.Body;
            requestHeaders = _server.LogEntries.ElementAt(2).RequestMessage.Headers;
            requestDict = JObject.Parse(requestBody);

            Assert.True(requestHeaders["STATSIG-API-KEY"].ToString().Equals("secret-fake-key"));

            JToken e;
            requestDict.TryGetValue("events", out e);
            var events = e.ToObject<EventLog[]>();

            Assert.Equal(6, events.Count());
            var evt = events.ElementAt(0);
            Assert.True(evt.EventName == "statsig::gate_exposure");
            Assert.Null(evt.Value);
            Assert.True(evt.Metadata.GetValueOrDefault("gate", "fail").Equals("test_gate"));
            Assert.True(evt.Metadata.GetValueOrDefault("gateValue", "fail").Equals("true"));
            Assert.True(evt.Metadata.GetValueOrDefault("ruleID", "fail").Equals("rule_id_1"));
            Assert.True(evt.SecondaryExposures.Count() == 0);
            Assert.True(evt.User.UserID.Equals("123"));
            Assert.True(evt.User.CustomIDs["random_id"] == "id123");
            Assert.True(evt.User.CustomIDs["another_random_id"] == "id456");
            Assert.InRange(Convert.ToInt32(evt.Time / 1000), nowSeconds - 2, nowSeconds + 2);

            evt = events.ElementAt(1);
            Assert.True(evt.EventName == "statsig::config_exposure");
            Assert.Null(evt.Value);
            Assert.True(evt.Metadata.GetValueOrDefault("config", "fail").Equals("test_config"));
            Assert.True(evt.Metadata.GetValueOrDefault("ruleID", "fail").Equals("rule_id_2"));
            Assert.True(evt.SecondaryExposures.Count() == 1);
            Assert.True(evt.SecondaryExposures.ElementAt(0).GetValueOrDefault("gate", "fail").Equals("test_gate"));
            Assert.True(evt.SecondaryExposures.ElementAt(0).GetValueOrDefault("gateValue", "fail").Equals("true"));
            Assert.True(evt.SecondaryExposures.ElementAt(0).GetValueOrDefault("ruleID", "fail").Equals("rule_id_1"));
            Assert.True(evt.User.UserID.Equals("123"));
            Assert.True(evt.User.CustomIDs["another_random_id"] == "id456");
            Assert.InRange(Convert.ToInt32(evt.Time / 1000), nowSeconds - 2, nowSeconds + 2);

            evt = events.ElementAt(2);
            Assert.True(evt.EventName == "event_1");
            Assert.Null(evt.Value);
            Assert.Null(evt.Metadata);
            Assert.Null(evt.SecondaryExposures);
            Assert.True(evt.User.UserID.Equals("123"));
            Assert.True(evt.User.CustomIDs["another_random_id"] == "id456");
            Assert.InRange(Convert.ToInt32(evt.Time / 1000), nowSeconds - 2, nowSeconds + 2);

            evt = events.ElementAt(3);
            Assert.True(evt.EventName == "event_2");
            Assert.True(evt.Value.Equals(Convert.ToInt64(1)));
            Assert.Null(evt.Metadata);
            Assert.Null(evt.SecondaryExposures);
            Assert.True(evt.User.UserID.Equals("123"));
            Assert.InRange(Convert.ToInt32(evt.Time / 1000), nowSeconds - 2, nowSeconds + 2);

            evt = events.ElementAt(4);
            Assert.True(evt.EventName == "event_3");
            Assert.True((string)evt.Value == "string");
            Assert.Null(evt.Metadata);
            Assert.Null(evt.SecondaryExposures);
            Assert.True(evt.User.UserID.Equals("123"));
            Assert.InRange(Convert.ToInt32(evt.Time / 1000), nowSeconds - 2, nowSeconds + 2);

            evt = events.ElementAt(5);
            Assert.True(evt.EventName == "event_4");
            Assert.Null(evt.Value);
            Assert.True(evt.Metadata.GetValueOrDefault("key", "fail").Equals("value"));
            Assert.Null(evt.SecondaryExposures);
            Assert.True(evt.User.UserID.Equals("123"));
            Assert.InRange(Convert.ToInt32(evt.Time / 1000), nowSeconds - 2, nowSeconds + 2);
        }
    }
}
