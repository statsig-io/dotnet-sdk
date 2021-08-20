using Statsig;
using Xunit;

namespace dotnet_statsig_tests
{
    public class LoggingTest
    {
        [Fact]
        public void TestPrivateAttributesStrippedFromLogs()
        {
            var user = new StatsigUser
            {
                UserID = "123",
            };
            user.AddPrivateAttribute("secret_prop", "shhh");
            user.AddCustomProperty("share_this", "see");

            var evt = new EventLog { User = user, EventName = "my_event" };
            var privateCount = evt.User.PrivateAttributes.Count;
            Assert.True(privateCount == 0);

            var customCount = evt.User.CustomProperties.Count;
            Assert.True(customCount == 1);
        }
    }
}