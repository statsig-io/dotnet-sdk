using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Statsig;
using Xunit;

namespace dotnet_statsig_tests
{
    public class UserEventDedupeKeyTest : IAsyncLifetime
    {
        private readonly StatsigUser _constantUserWithId = new() { UserID = "user-id" };
        private StatsigUser _dynamicUserWithId;

        private readonly StatsigUser _constantUserWithCustomIds =
            new() { customIDs = { { "custom-id", "custom-id-value" } } };

        private StatsigUser _dynamicUserWithCustomIds;

        public Task InitializeAsync()
        {
            _dynamicUserWithId = new StatsigUser { UserID = "user-id" };
            _dynamicUserWithCustomIds = new StatsigUser
                { customIDs = { { "custom-id", "custom-id-value" } } };
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        [Fact]
        public void TestSameUserSameKey()
        {
            Assert.Equal(_constantUserWithId.GetDedupeKey(), _dynamicUserWithId.GetDedupeKey());
            Assert.Equal(_constantUserWithCustomIds.GetDedupeKey(), _dynamicUserWithCustomIds.GetDedupeKey());
        }

        [Fact]
        public void TestSameGateExposureEventSameKey()
        {
            var eventA = EventLog.CreateGateExposureLog(_constantUserWithId, "a_gate", true, "a-rule-id",
                new List<IReadOnlyDictionary<string, string>>());
            var eventB = EventLog.CreateGateExposureLog(_dynamicUserWithId, "a_gate", true, "a-rule-id",
                new List<IReadOnlyDictionary<string, string>>());

            Assert.Equal(eventA.GetDedupeKey(), eventB.GetDedupeKey());
        }

        [Fact]
        public void TestSameConfigExposureEventSameKey()
        {
            var eventA = EventLog.CreateGateExposureLog(_constantUserWithId, "a_gate", true, "a-rule-id",
                new List<IReadOnlyDictionary<string, string>>());
            var eventB = EventLog.CreateGateExposureLog(_dynamicUserWithId, "a_gate", true, "a-rule-id",
                new List<IReadOnlyDictionary<string, string>>());

            Assert.Equal(eventA.GetDedupeKey(), eventB.GetDedupeKey());
        }

        [Fact]
        public void TestSameLayerExposureEventSameKey()
        {
            var eventA = EventLog.CreateGateExposureLog(_constantUserWithId, "a_gate", true, "a-rule-id",
                new List<IReadOnlyDictionary<string, string>>());
            var eventB = EventLog.CreateGateExposureLog(_dynamicUserWithId, "a_gate", true, "a-rule-id",
                new List<IReadOnlyDictionary<string, string>>());

            Assert.Equal(eventA.GetDedupeKey(), eventB.GetDedupeKey());
        }

        [Fact]
        public void TestSameCustomEventSameKey()
        {
            var eventA = new EventLog
            {
                EventName = "event-name",
                Value = 1,
                User = _constantUserWithId
            };

            var eventB = new EventLog
            {
                EventName = "event-name",
                Value = 1,
                User = _dynamicUserWithId
            };

            Assert.Equal(eventA.GetDedupeKey(), eventB.GetDedupeKey());
        }
    }
}