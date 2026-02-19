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

            var clusterNodeCollection = new ClusterNodeCollection();
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
            clusterNodeCollection.Add(clusterNode.Object);
            mockBucket.Object.Nodes.Add(clusterNode.Object);

            using var configListener = new HttpStreamingConfigListener(mockBucket.Object,
                clusterOptions, httpClientFactory, configHandler, mockLogger);
            configListener.StartListening();

            // Wait for the listener to start (use async polling instead of SpinWait to avoid starving the background task)
            var listenerStarted = await AsyncTestHelper.WaitForConditionAsync(
                () => messageHandler.CallCount > 0,
                timeout: TimeSpan.FromSeconds(30));
            if (!listenerStarted)
            {
                throw new TimeoutException($"{nameof(HttpStreamingConfigListener)} didn't start in time.");
            }

            // Wait for multiple calls to occur
            var multipleCalls = await AsyncTestHelper.WaitForConditionAsync(
                () => messageHandler.CallCount > 2,
                timeout: TimeSpan.FromSeconds(10));
            Assert.True(multipleCalls, "Expected more than 2 calls before dispose");

            configListener.Dispose();

            // Wait for the call count to stabilize after dispose (instead of fixed delays)
            var stableCallCount = await AsyncTestHelper.WaitForStableValueAsync(
                () => messageHandler.CallCount,
                stableDuration: TimeSpan.FromMilliseconds(300),
                timeout: TimeSpan.FromSeconds(5));

            // Verify no more calls are made after stabilization
            var finalCallCount = messageHandler.CallCount;
            Assert.Equal(stableCallCount, finalCallCount);

            await configListener.DisposeAsync();
        }

        class ThrowsEveryTimeMessageHandler : HttpMessageHandler
        {
            public int CallCount { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CallCount++;
                throw new NotImplementedException();
            }
        }
    }
}
