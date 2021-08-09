using Xunit;
using System.Threading.Tasks;
using Microsoft.QualityTools.Testing.Fakes;
using Microsoft.QualityTools.Testing.Fakes.Shims;
using System.Net;

namespace dotnet_statsig_tests.Server
{
    public class NetworkTest
    {
        public NetworkTest()
        {
        }

        [Fact]
        public async Task InitializeShimHttpWebResponse()
        {
            using (ShimsContext.Create())
            {
                System.Fakes.ShimWebRequest.AllInstances.GetResponseAsync = (req) =>
                {
                    var res = new HttpWebResponse()
                    return Task.FromResult((WebResponse)res);
                };
                //ShimWebRequest.AllInstances.GetResponseAsync = (x) =>
                //{
                //    /* you can replace the var with WebResponse if you aren't going to set any behavior */
                //    var res = new ShimHttpWebResponse();
                //    return Task.FromResult((WebResponse)res);
                //};

                //ShimWebRequest.CreateString = uri =>
                //{
                //    WebRequest req = new ShimFileWebRequest();
                //    return req;
                //};

                //var request = WebRequest.Create("");
                //var response = await request.GetResponseAsync() as HttpWebResponse;

                //Assert.IsNotNull(response);
            }
        }
    }
}
