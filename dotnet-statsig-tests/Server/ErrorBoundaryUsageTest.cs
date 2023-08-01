using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Statsig;
using Statsig.Lib;
using Statsig.Server;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.ResponseProviders;
using WireMock.Server;
using WireMock.Settings;
using Xunit;

namespace dotnet_statsig_tests.Server
{
    public class ErrorBoundaryUsageTest : IAsyncLifetime, IResponseProvider
    {
        private WireMockServer _server;
        private ServerDriver _statsig;
        private List<RequestMessage> _requests;
        private CountdownEvent _onRequest;

        private readonly StatsigUser _user = new()
        {
            UserID = "a_user"
        };

        Task IAsyncLifetime.InitializeAsync()
        {
            _requests = new List<RequestMessage>();
            _server = WireMockServer.Start();
            _server.Given(Request.Create().WithPath("*").UsingAnyMethod()).RespondWith(this);
            ErrorBoundary.ExceptionEndpoint = $"{_server.Urls[0]}/v1/sdk_exception";

            _statsig = new ServerDriver("secret-key");

            return Task.CompletedTask;
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            _server.Stop();
            await _statsig.Shutdown();
        }

        public Task<(ResponseMessage Message, IMapping Mapping)> ProvideResponseAsync(
            RequestMessage request,
            IWireMockServerSettings settings
        )
        {
            _requests.Add(request);
            _onRequest?.Signal();
            return Response.Create()
                .WithStatusCode(200)
                .WithBody("{}")
                .ProvideResponseAsync(request, settings);
        }

        [Fact]
        public async void TestDoesNotCatchUninitializedExceptions()
        {
            Exception thrown = null;
            try
            {
                await _statsig.CheckGate(_user, "");
            }
            catch (StatsigUninitializedException e)
            {
                thrown = e;
            }

            Assert.NotNull(thrown);
            Assert.Empty(_requests);
        }

        [Fact]
        public async void TestDoesNotCatchStatsigInvalidOperationExceptions()
        {
            Exception thrown = null;
            try
            {
                await _statsig.Shutdown();
                await _statsig.CheckGate(_user, "");
            }
            catch (StatsigInvalidOperationException e)
            {
                thrown = e;
            }

            Assert.NotNull(thrown);
            Assert.Empty(_requests);
        }


        [Fact]
        public async void TestDoesNotCatchStatsigArgumentNullExceptions()
        {
            Exception thrown = null;
            try
            {
                await _statsig.Initialize();
                StatsigUser user = null;
                await _statsig.CheckGate(user, "");
            }
            catch (StatsigArgumentNullException e)
            {
                thrown = e;
            }

            Assert.NotNull(thrown);
            Assert.Empty(_requests);
        }

        [Fact]
        public void TestDoesNotCatchStatsigArgumentExceptions()
        {
            Exception thrown = null;
            try
            {
                var _ = new ServerDriver("");
            }
            catch (StatsigArgumentException e)
            {
                thrown = e;
            }

            Assert.NotNull(thrown);
            Assert.Empty(_requests);
        }


        [Fact]
        public async void TestHandlingErrorsInInitialize()
        {
            await PerformAndWaitForError(
                async () => await _statsig.Initialize());

            AssertSingleErrorBoundaryHit(
                "Initialize",
                "System.NullReferenceException"
            );
        }

        [Fact]
        public async void TestHandlingErrorsInShutdown()
        {
            await PerformAndWaitForError(
                async () => await _statsig.Shutdown());

            AssertSingleErrorBoundaryHit(
                "Shutdown",
                "System.NullReferenceException"
            );
        }

        [Fact]
        public async void TestHandlingErrorsInCheckGate()
        {
            await PerformAndWaitForError(
                async () => await _statsig.CheckGate(_user, "a_gate"));

            AssertSingleErrorBoundaryHit(
                "CheckGate",
                "System.NullReferenceException"
            );
        }

        [Fact]
        public async void TestHandlingErrorsInGetConfig()
        {
            await PerformAndWaitForError(
                async () => await _statsig.GetConfig(_user, "a_config"));

            AssertSingleErrorBoundaryHit(
                "GetConfig",
                "System.NullReferenceException"
            );
        }

        [Fact]
        public async void TestHandlingErrorsInGetExperiment()
        {
            await PerformAndWaitForError(
                async () => await _statsig.GetExperiment(_user, "an_experiment"));

            AssertSingleErrorBoundaryHit(
                "GetExperiment",
                "System.NullReferenceException"
            );
        }

        [Fact]
        public async void TestHandlingErrorsInGetLayer()
        {
            await PerformAndWaitForError(
                async () => await _statsig.GetLayer(_user, "a_layer"));

            AssertSingleErrorBoundaryHit(
                "GetLayer",
                "System.NullReferenceException"
            );
        }

        [Fact]
        public async void TestHandlingErrorsInGenerateInitializeResponse()
        {
            await PerformAndWaitForError(
                () => _statsig.GenerateInitializeResponse(_user));

            AssertSingleErrorBoundaryHit(
                "GenerateInitializeResponse",
                "System.NullReferenceException"
            );
        }

        [Fact]
        public async void TestHandlingErrorsInLogEventString()
        {
            await PerformAndWaitForError(
                () => _statsig.LogEvent(_user, "a_string_event", "a_value"));

            AssertSingleErrorBoundaryHit(
                "LogEvent:String",
                "System.NullReferenceException",
                "LogEvent"
            );
        }

        [Fact]
        public async void TestHandlingErrorsInLogEventInt()
        {
            await PerformAndWaitForError(
                () => _statsig.LogEvent(_user, "an_int_event", 1));

            AssertSingleErrorBoundaryHit(
                "LogEvent:Int",
                "System.NullReferenceException",
                "LogEvent"
            );
        }

        [Fact]
        public async void TestHandlingErrorsInLogEventDouble()
        {
            await PerformAndWaitForError(
                () => _statsig.LogEvent(_user, "a_double_event", 1.2));

            AssertSingleErrorBoundaryHit(
                "LogEvent:Double",
                "System.NullReferenceException",
                "LogEvent"
            );
        }

        #region Private Methods

        private async Task PerformAndWaitForError(Action task)
        {
            await PerformAndWaitForError(() =>
            {
                task();
                return Task.CompletedTask;
            });
        }

        private async Task PerformAndWaitForError(Func<Task> task)
        {
            await _statsig.Initialize();
            _statsig._eventLogger = null;
            _statsig.evaluator = null;

            await task();

            WaitForNextRequest();
        }

        private void WaitForNextRequest()
        {
            _onRequest = new CountdownEvent(1);
            _onRequest.Wait(TimeSpan.FromMilliseconds(1000));
        }

        private void AssertSingleErrorBoundaryHit(string tag, string exception, string infoRegex = null)
        {
            Assert.Single(_requests);

            var request = _requests[0];
            Assert.EndsWith("/v1/sdk_exception", request.Url);

            var sdkDetails = SDKDetails.GetServerSDKDetails();
            var body = JsonConvert.DeserializeObject<Dictionary<string, object>>(request.Body);
            Assert.Equal(exception, body?["exception"]);
            Assert.Equal(tag, body?["tag"]);

            var statsigMetadata = (body?["statsigMetadata"] as JObject)?.ToObject<Dictionary<string, string>>();
            Assert.Equal(sdkDetails.StatsigMetadata, statsigMetadata);

            var info = body?["info"] as string ?? "";
            Assert.Matches($"at Statsig.Server.ServerDriver\\..*<{infoRegex ?? tag}>.*in", info);

            var headers = request.Headers;
            Assert.Equal(sdkDetails.SDKType, headers["STATSIG-SDK-TYPE"].ToString());
            Assert.Equal(sdkDetails.SDKVersion, headers["STATSIG-SDK-VERSION"].ToString());
        }

        #endregion
    }
}