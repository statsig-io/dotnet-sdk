using System.Collections.Generic;
using Xunit;
using Statsig;
using Statsig.Server;
using Statsig.Lib;
using System.Threading.Tasks;
using Statsig.Network;
using WireMock.ResponseProviders;
using WireMock;
using WireMock.Settings;
using WireMock.Server;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace dotnet_statsig_tests
{
    [Collection("Statsig Singleton Tests")]
    public class SpecStoreTest : IAsyncLifetime, IResponseProvider
    {
        WireMockServer _server;
        string baseURL;

        int getIDListCount = 0;
        int list1Count = 0;

        Task IAsyncLifetime.InitializeAsync()
        {
            _server = WireMockServer.Start();
            baseURL = _server.Urls[0];
            _server.ResetLogEntries();
            _server.Given(
                Request.Create().WithPath("/v1/download_config_specs").UsingPost()
            ).RespondWith(this);
            _server.Given(
                Request.Create().WithPath("/v1/get_id_lists").UsingPost()
            ).RespondWith(this);
            _server.Given(
                Request.Create().WithPath("/list_1").UsingAnyMethod()
            ).RespondWith(this);
            _server.Given(
                Request.Create().WithPath("/list_2").UsingAnyMethod()
            ).RespondWith(this);
            _server.Given(
                Request.Create().WithPath("/list_3").UsingAnyMethod()
            ).RespondWith(this);

            return Task.CompletedTask;
        }

        Task IAsyncLifetime.DisposeAsync()
        {
            _server.Stop();
            return Task.CompletedTask;
        }

        // IResponseProvider
        public async Task<(ResponseMessage Message, IMapping Mapping)> ProvideResponseAsync(
            RequestMessage requestMessage, IWireMockServerSettings settings)
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
                var body = SpecStoreResponseData.getIDListsResponse(baseURL, getIDListCount);
                getIDListCount++;
                return await Response.Create()
                    .WithStatusCode(200)
                    .WithBody(body)
                    .ProvideResponseAsync(requestMessage, settings);
            }

            if (requestMessage.AbsolutePath.Contains("/list_1"))
            {
                var body = SpecStoreResponseData.getIDList1Response(list1Count);
                list1Count++;
                return await Response.Create()
                    .WithStatusCode(200)
                    .WithBody(body)
                    .ProvideResponseAsync(requestMessage, settings);
            }

            if (requestMessage.AbsolutePath.Contains("/list_2"))
            {
                return await Response.Create()
                    .WithStatusCode(200)
                    .WithBody("+a\n")
                    .ProvideResponseAsync(requestMessage, settings);
            }

            if (requestMessage.AbsolutePath.Contains("/list_3"))
            {
                return await Response.Create()
                    .WithStatusCode(200)
                    .WithBody("+0\n")
                    .ProvideResponseAsync(requestMessage, settings);
            }

            return await Response.Create()
                .WithStatusCode(404)
                .ProvideResponseAsync(requestMessage, settings);
        }

        [Fact]
        public async void TestStore()
        {
            var opts = new StatsigOptions(_server.Urls[0] + "/v1") { IDListsSyncInterval = 1 };
            var dispatcher = new RequestDispatcher("secret-123", opts, SDKDetails.GetServerSDKDetails(), "my-session");
            var store = new SpecStore(opts, dispatcher, "secret-123", new ErrorBoundary("secret-123", SDKDetails.GetServerSDKDetails()));
            await store.Initialize();
            var expectedIDLists = SpecStoreResponseData.getIDListExpectedResults(_server.Urls[0]);
            TestStoreHelper(store, expectedIDLists, 0);
            await Task.Delay(1100);
            TestStoreHelper(store, expectedIDLists, 1);
            await Task.Delay(1100);
            TestStoreHelper(store, expectedIDLists, 2);
            await Task.Delay(1100);
            TestStoreHelper(store, expectedIDLists, 3);
            await Task.Delay(1100);
            TestStoreHelper(store, expectedIDLists, 4);
            await Task.Delay(1100);
            TestStoreHelper(store, expectedIDLists, 5);
        }

        private void TestStoreHelper(SpecStore store, Dictionary<string, IDList[]> expectedLists, int index)
        {
            IDList list1, list2, list3 = null;
            store._idLists.TryGetValue("list_1", out list1);
            store._idLists.TryGetValue("list_2", out list2);
            store._idLists.TryGetValue("list_3", out list3);
            Assert.Equal(index + 1, getIDListCount);
            Assert.Equal(expectedLists["list_1"][index], list1);
            Assert.Equal(expectedLists["list_2"][index], list2);
            Assert.Equal(expectedLists["list_3"][index], list3);
        }
    }
}