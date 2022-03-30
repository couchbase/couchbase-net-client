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
            var clusterOptions = new ClusterOptions()
                .WithConnectionString($"couchbases://NOSUCHHOST{nameof(Should_Continue_After_Failures)}")
                .WithCredentials("UnitTestUser", "PasswordDoesn'tMatter");

            var messageHandler = new ThrowsEveryTimeMessageHandler();
            var httpClientFactory = new MockHttpClientFactory(() => new HttpClient(messageHandler, false));
            var configHandler = new Mock<IConfigHandler>(MockBehavior.Loose).Object;
            var mockLogger = new Mock<ILogger<HttpStreamingConfigListener>>(MockBehavior.Loose).Object;
            var mockBucket = new Mock<BucketBase>();

            var clusterNodeCollection = new ClusterNodeCollection();
            var clusterNode = new Mock<IClusterNode>();
            clusterNode.Setup(x => x.KeyEndPoints).Returns(new ReadOnlyObservableCollection<HostEndpointWithPort>(new ObservableCollection<HostEndpointWithPort>()));
            clusterNode.Setup(x => x.ManagementUri).Returns(new Uri($"http://NOSUCHHOST{nameof(Should_Continue_After_Failures)}:8091"));
            clusterNode.Setup(x => x.EndPoint).Returns(new HostEndpointWithPort($"NOSUCHHOST{nameof(Should_Continue_After_Failures)}", 11210));
            clusterNodeCollection.Add(clusterNode.Object);
            mockBucket.Object.Nodes.Add(clusterNode.Object);

            using var configListener = new HttpStreamingConfigListener(mockBucket.Object,
                clusterOptions, httpClientFactory, configHandler, mockLogger);
            configListener.StartListening();
            var exitedSpinBeforeTimeout = SpinWait.SpinUntil(() => messageHandler.CallCount > 0, TimeSpan.FromSeconds(10));
            if (!exitedSpinBeforeTimeout)
            {
                throw new TimeoutException($"{nameof(HttpStreamingConfigListener)} didn't start in time.");
            }

            await Task.Delay(500);
            Assert.NotInRange(messageHandler.CallCount, 0, 2);
            configListener.Dispose();

            // give it a little time to finish up.
            await Task.Delay(500);
            var callCountAfterDispose = messageHandler.CallCount;

            // a little more time to generate more calls if it was still running.
            await Task.Delay(500);
            Assert.Equal(callCountAfterDispose, messageHandler.CallCount);

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
