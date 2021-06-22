using System;
using Xunit;

using Statsig;
using Statsig.Server;
using System.Threading.Tasks;

namespace dotnet_statsig_tests
{
    public class StatsigServerTest : IAsyncLifetime
    {
        public async Task InitializeAsync()
        {
            await StatsigServer.Initialize("secret-9IWfdzNwExEYHEW4YfOQcFZ4xreZyFkbOXHaNbPsMwW",
                new StatsigOptions("https://latest.api.statsig.com/v1", new StatsigEnvironment(EnvironmentTier.Development)));
        }

        public async Task DisposeAsync()
        {
        }

        [Fact]
        public async void TestPublicGate()
        {
            var publicGate = await StatsigServer.CheckGate(new StatsigUser { UserID = "123" }, "test_public");
            Assert.True(publicGate);
        }

        [Fact]
        public async void TestEmailGate()
        {
            var passEmailGate = await StatsigServer.CheckGate(new StatsigUser { UserID = "123", Email = "jkw@statsig.com" }, "test_email");
            var failEmailGate = await StatsigServer.CheckGate(new StatsigUser { UserID = "123", Email = "jkw@gmail.com" }, "test_email");
            Assert.True(passEmailGate);
            Assert.False(failEmailGate);
        }

        [Fact]
        public async void TestEnvTierGate_Pass()
        {
            var passGate = await StatsigServer.CheckGate(new StatsigUser { UserID = "123", Email = "jkw@statsig.com" }, "test_environment_tier");
            Assert.True(passGate);
        }

        [Fact]
        public async void TestClientVersion()
        {
            var passGate1 = await StatsigServer.CheckGate(new StatsigUser { UserID = "123", AppVersion = "1.0" }, "test_version");
            Assert.True(passGate1);

            var passGate2 = await StatsigServer.CheckGate(new StatsigUser { UserID = "123", AppVersion = "1.2" }, "test_version");
            Assert.True(passGate2);

            var passGate3 = await StatsigServer.CheckGate(new StatsigUser { UserID = "123", AppVersion = "1.2.3" }, "test_version");
            Assert.True(passGate3);

            var passGate4 = await StatsigServer.CheckGate(new StatsigUser { UserID = "123", AppVersion = "1.2.3.3" }, "test_version");
            Assert.True(passGate4);

            var passGate5 = await StatsigServer.CheckGate(new StatsigUser { UserID = "123", AppVersion = "1.2.3.3-beta" }, "test_version");
            Assert.True(passGate5);

            var passGate6 = await StatsigServer.CheckGate(new StatsigUser { UserID = "123", AppVersion = "1" }, "test_version");
            Assert.True(passGate6);



            var failGate1 = await StatsigServer.CheckGate(new StatsigUser { UserID = "123", AppVersion = "2.0" }, "test_version");
            Assert.False(failGate1);

            var failGate2 = await StatsigServer.CheckGate(new StatsigUser { UserID = "123", AppVersion = "1.3" }, "test_version");
            Assert.False(failGate2);

            var failGate3 = await StatsigServer.CheckGate(new StatsigUser { UserID = "123", AppVersion = "1.2.4" }, "test_version");
            Assert.False(failGate3);

            var failGate4 = await StatsigServer.CheckGate(new StatsigUser { UserID = "123", AppVersion = "1.2.3.5" }, "test_version");
            Assert.False(failGate4);

            var failGate5 = await StatsigServer.CheckGate(new StatsigUser { UserID = "123", AppVersion = "1.2.3.5-beta" }, "test_version");
            Assert.False(failGate5);

            var failGate6 = await StatsigServer.CheckGate(new StatsigUser { UserID = "123", AppVersion = "1.2.3.39" }, "test_version");
            Assert.False(failGate6);

            var failGate7 = await StatsigServer.CheckGate(new StatsigUser { UserID = "123", AppVersion = "1.2.3.4.1" }, "test_version");
            Assert.False(failGate7);

            var failGate8 = await StatsigServer.CheckGate(new StatsigUser { UserID = "123", AppVersion = "1.2.3.4" }, "test_version");
            Assert.False(failGate8);

            var failGate9 = await StatsigServer.CheckGate(new StatsigUser { UserID = "123", AppVersion = "2" }, "test_version");
            Assert.False(failGate9);
        }
    }
}
