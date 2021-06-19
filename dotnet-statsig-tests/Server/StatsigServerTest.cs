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
                new StatsigOptions(new StatsigEnvironment(EnvironmentTier.Development)));
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
    }
}
