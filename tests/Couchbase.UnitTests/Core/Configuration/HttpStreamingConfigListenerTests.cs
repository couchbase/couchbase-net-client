using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Configuration.Server.Streaming;
using Couchbase.UnitTests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.Configuration
{
    public class HttpStreamingConfigListenerTests
    {
        [Fact]
        public async Task Should_Continue_After_Failures()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var clusterOptions = new ClusterOptions()
                .WithConnectionString($"couchbases://NOSUCHHOST{nameof(Should_Continue_After_Failures)}")
                .WithCredentials("UnitTestUser", "PasswordDoesn'tMatter");
#pragma warning restore CS0618 // Type or member is obsolete

            var messageHandler = new ThrowsEveryTimeMessageHandler();
            var httpClientFactory = new MockHttpClientFactory(() => new HttpClient(messageHandler, false));
            var configHandler = new Mock<IConfigHandler>(MockBehavior.Loose).Object;
            var mockLogger = new Mock<ILogger<HttpStreamingConfigListener>>(MockBehavior.Loose).Object;
            var mockBucket = new Mock<BucketBase>();

            var nodeList = new BucketNodeList();
            var clusterNode = new Mock<IClusterNode>();
            clusterNode.Setup(x => x.NodesAdapter).Returns(new NodeAdapter
            {
                MgmtApi = 8091,
                MgmtApiSsl = 18091
            });

            clusterNode.Setup(x => x.HasManagement).Returns(true);
            clusterNode.Setup(x => x.KeyEndPoints).Returns(new ReadOnlyObservableCollection<HostEndpointWithPort>(new ObservableCollection<HostEndpointWithPort>()));
            clusterNode.Setup(x => x.ManagementUri).Returns(new Uri($"http://NOSUCHHOST{nameof(Should_Continue_After_Failures)}:8091"));
            clusterNode.Setup(x => x.EndPoint).Returns(new HostEndpointWithPort($"NOSUCHHOST{nameof(Should_Continue_After_Failures)}", 11210));
            nodeList.Add(clusterNode.Object);
            mockBucket.Object.Nodes.Add(clusterNode.Object);

            // await using, not using: the sync Dispose only signals the background loop to stop, while
            // DisposeAsync waits for it to actually finish. This is the failure path — the explicit
            // DisposeAsync below is what the test asserts around.
            await using var configListener = new HttpStreamingConfigListener(mockBucket.Object,
                clusterOptions, httpClientFactory, configHandler, mockLogger);
            configListener.StartListening();

            // Every request throws, and the listener has to keep going regardless. A third attempt is
            // therefore proof that it retried after failing, and is awaited rather than polled for: a
            // listener which gives up hangs this test, which says what went wrong, instead of failing
            // a deadline that only says the runner was busy (NCBC-4293).
            await messageHandler.Calls.WaitForAsync(3);

            // Disposing has to stop it. DisposeAsync awaits the background loop, so its returning is
            // itself the proof that the loop has exited and can make no further calls — there is no
            // need to watch the call count and decide it has gone quiet enough.
            await configListener.DisposeAsync();

            Assert.True(messageHandler.Calls.Count >= 3);
        }

        class ThrowsEveryTimeMessageHandler : HttpMessageHandler
        {
            /// <summary>
            /// Counted rather than a plain field: the listener calls this from its background loop
            /// while the test reads it.
            /// </summary>
            public AsyncCounter Calls { get; } = new();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls.Increment();
                throw new NotImplementedException();
            }
        }
    }
}
