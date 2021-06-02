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
    }
}
