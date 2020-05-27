using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Configuration.Server.Streaming;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Views;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

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
            var httpClient = new HttpClient(messageHandler);
            var configHandler = new Mock<IConfigHandler>(MockBehavior.Loose).Object;
            var mockLogger = new Mock<ILogger<HttpStreamingConfigListener>>(MockBehavior.Loose).Object;
            using var configListener = new HttpStreamingConfigListener(nameof(Should_Continue_After_Failures),
                clusterOptions, httpClient, configHandler, mockLogger);
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
