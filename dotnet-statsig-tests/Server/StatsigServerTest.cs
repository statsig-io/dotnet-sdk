using System;
using Xunit;

using Statsig;
using Statsig.Server;
using System.Threading.Tasks;

namespace dotnet_statsig_tests
{
    public class StatsigServerTest
    {
        [Fact]
        public async void TestPublicGate()
        {
            await StatsigServer.Initialize("secret-9IWfdzNwExEYHEW4YfOQcFZ4xreZyFkbOXHaNbPsMwW");
            var publicGate = await StatsigServer.CheckGate(new StatsigUser(), "test_public");
            Assert.True(publicGate);
        }

        [Fact]
        public async void TestEmailGate()
        {
            await StatsigServer.Initialize("secret-9IWfdzNwExEYHEW4YfOQcFZ4xreZyFkbOXHaNbPsMwW");
            var passEmailGate = await StatsigServer.CheckGate(new StatsigUser { Email = "jkw@statsig.com" }, "test_email");
            var failEmailGate = await StatsigServer.CheckGate(new StatsigUser { Email = "jkw@gmail.com" }, "test_email");
            Assert.True(passEmailGate);
            Assert.False(failEmailGate);
        }
    }
}
