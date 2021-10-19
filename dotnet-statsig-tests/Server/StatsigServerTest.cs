using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Statsig.Server;
using WireMock.Server;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using Statsig;

namespace dotnet_statsig_tests.Server
{
    public class StatsigServerTest
    {
        [Fact]
        public async void TestInitialize()
        {
            var server = WireMockServer.Start(9999);
            server.Given(
                Request.Create().WithPath("/v1/download_config_specs").UsingPost()
            ).RespondWith(
                Response.Create().WithStatusCode(200).WithBodyAsJson(
                    new Dictionary<string, object>
                    {
                        {
                            "feature_gates", new List<Dictionary<string, object>>
                            {
                                new Dictionary<string, object>
                                {
                                    { "name", "test_gate" },
                                    { "type", "feature_gate" },
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
            server.Given(
                Request.Create().WithPath("/v1/log_event").UsingPost()
            ).RespondWith(
                Response.Create().WithStatusCode(200)
            );
            var user = new StatsigUser
            {
                UserID = "123",
            };
            user.AddPrivateAttribute("value", "secret");
            user.AddCustomProperty("value", "public");
            await StatsigServer.Initialize
            (
                "secret-fake-key",
                new StatsigOptions("http://localhost:9999/v1")
            );

            Assert.Single(server.LogEntries);

            var requestBody = server.LogEntries.ElementAt(0).RequestMessage.Body;
            var requestHeaders = server.LogEntries.ElementAt(0).RequestMessage.Headers;
            var requestDict = JObject.Parse(requestBody);

            JToken m;
            requestDict.TryGetValue("statsigMetadata", out m);
            var metadata = m.ToObject<Dictionary<string, string>>();

            Assert.True(requestHeaders["STATSIG-API-KEY"].ToString().Equals("secret-fake-key"));

            Assert.True(metadata["sdkType"].Equals("dotnet-server"));
            Assert.True(metadata["sdkVersion"].Equals("1.4.2.0"));

            var gate = await StatsigServer.CheckGate(user, "test_gate");
            Assert.True(gate);
            var exp = await StatsigServer.GetExperiment(user, "test_config");
            Assert.True(exp.Get("stringValue", "wrong").Equals("1"));
            Assert.True(exp.Get("stringValue1", "wrong").Equals("wrong"));
            Assert.True(exp.Get("numberValue", 0).Equals(1));
            Assert.True(exp.Get("numberValue1", 0).Equals(0));
            Assert.True(exp.Get("boolValue", false));
            Assert.False(exp.Get("boolValue1", false));

            var config = await StatsigServer.GetConfig(user, "test_config");
            Assert.True(config.Get("stringValue", "wrong").Equals("1"));
            Assert.True(config.Get("numberValue", 0).Equals(1));
            Assert.True(config.Get("boolValue", false));

            StatsigServer.LogEvent(user, "event_1");
            StatsigServer.LogEvent(user, "event_2", 1);
            StatsigServer.LogEvent(user, "event_3", "string");
            StatsigServer.LogEvent(user, "event_4", null, new Dictionary<string, string> { { "key", "value" } });

            StatsigServer.Shutdown();

            // Verify log event requets for exposures and custom logs
            requestBody = server.LogEntries.ElementAt(1).RequestMessage.Body;
            requestHeaders = server.LogEntries.ElementAt(1).RequestMessage.Headers;
            requestDict = JObject.Parse(requestBody);

            Assert.True(requestHeaders["STATSIG-API-KEY"].ToString().Equals("secret-fake-key"));

            JToken e;
            requestDict.TryGetValue("events", out e);
            var events = e.ToObject<EventLog[]>();

            Assert.True(events.Count() == 7);
            var evt = events.ElementAt(0);
            Assert.True(evt.EventName == "statsig::gate_exposure");
            Assert.Null(evt.Value);
            Assert.True(evt.Metadata.GetValueOrDefault("gate", "fail").Equals("test_gate"));
            Assert.True(evt.Metadata.GetValueOrDefault("gateValue", "fail").Equals("true"));
            Assert.True(evt.Metadata.GetValueOrDefault("ruleID", "fail").Equals("rule_id_1"));
            Assert.True(evt.SecondaryExposures.Count() == 0);
            Assert.True(evt.User.UserID.Equals("123"));

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

            evt = events.ElementAt(2);
            Assert.True(evt.EventName == "statsig::config_exposure");
            Assert.Null(evt.Value);
            Assert.True(evt.Metadata.GetValueOrDefault("config", "fail").Equals("test_config"));
            Assert.True(evt.Metadata.GetValueOrDefault("ruleID", "fail").Equals("rule_id_2"));
            Assert.True(evt.SecondaryExposures.Count() == 1);
            Assert.True(evt.SecondaryExposures.ElementAt(0).GetValueOrDefault("gate", "fail").Equals("test_gate"));
            Assert.True(evt.SecondaryExposures.ElementAt(0).GetValueOrDefault("gateValue", "fail").Equals("true"));
            Assert.True(evt.SecondaryExposures.ElementAt(0).GetValueOrDefault("ruleID", "fail").Equals("rule_id_1"));
            Assert.True(evt.User.UserID.Equals("123"));

            evt = events.ElementAt(3);
            Assert.True(evt.EventName == "event_1");
            Assert.Null(evt.Value);
            Assert.Null(evt.Metadata);
            Assert.Null(evt.SecondaryExposures);
            Assert.True(evt.User.UserID.Equals("123"));

            evt = events.ElementAt(4);
            Assert.True(evt.EventName == "event_2");
            Assert.True(evt.Value.Equals(Convert.ToInt64(1)));
            Assert.Null(evt.Metadata);
            Assert.Null(evt.SecondaryExposures);
            Assert.True(evt.User.UserID.Equals("123"));

            evt = events.ElementAt(5);
            Assert.True(evt.EventName == "event_3");
            Assert.True((string)evt.Value == "string");
            Assert.Null(evt.Metadata);
            Assert.Null(evt.SecondaryExposures);
            Assert.True(evt.User.UserID.Equals("123"));

            evt = events.ElementAt(6);
            Assert.True(evt.EventName == "event_4");
            Assert.Null(evt.Value);
            Assert.True(evt.Metadata.GetValueOrDefault("key", "fail").Equals("value"));
            Assert.Null(evt.SecondaryExposures);
            Assert.True(evt.User.UserID.Equals("123"));

            server.Stop();
        }
    }
}