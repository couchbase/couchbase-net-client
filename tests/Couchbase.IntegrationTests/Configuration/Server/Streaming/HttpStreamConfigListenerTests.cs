using System;
using System.Threading;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Configuration.Server.Streaming;
using Couchbase.Core.DI;
using Couchbase.Core.IO.HTTP;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Test.Common.Fixtures;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.IntegrationTests.Configuration.Server.Streaming
{
    public class HttpStreamConfigListenerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public HttpStreamConfigListenerTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact(Skip = "Test is broken")]
        public void When_Config_Published_Subscriber_Receives_Config()
        {
            using var autoResetEvent = new AutoResetEvent(false);

            using var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromSeconds(10));

            using var context = new ClusterContext(tokenSource, _fixture.ClusterOptions);
            var httpClientFactory = context.ServiceProvider.GetRequiredService<ICouchbaseHttpClientFactory>();
            _fixture.Log("START");
            var handler = new Mock<IConfigHandler>();
            handler
                .Setup(m => m.Publish(It.IsAny<BucketConfig>()))
                .Callback((BucketConfig config) =>
                {
                    // ReSharper disable once AccessToDisposedClosure
                    _fixture.Log("RESET");
                    autoResetEvent.Set();
                });

            var mockBucket = new Mock<IConfigUpdateEventSink>();
            mockBucket.Setup(m => m.Name).Returns("default");

            using var listener = new HttpStreamingConfigListener(mockBucket.Object, _fixture.ClusterOptions, httpClientFactory, handler.Object,
                new Mock<ILogger<HttpStreamingConfigListener>>().Object);

            listener.StartListening();
            _fixture.Log("LISTENING");
            Assert.True(autoResetEvent.WaitOne(TimeSpan.FromSeconds(5)));
        }
    }
}
