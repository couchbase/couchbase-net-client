using System;
using System.Collections.Generic;
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
            var messageHandler = new ThrowsEveryTimeMessageHandler();

            // await using, not using: the sync Dispose only signals the background loop to stop, while
            // DisposeAsync waits for it to actually finish. This is the failure path — the explicit
            // DisposeAsync below is what the test asserts around.
            await using var configListener =
                CreateListener(nameof(Should_Continue_After_Failures), messageHandler);

            // Skip the backoff rather than serve it: this test is about the listener carrying on after
            // a failure, and waiting out the real backoff would cost a second and make it depend on
            // the very timing NCBC-4298 changed. Hold at the third attempt so the loop is not still
            // running, at full speed, while the assertion reads what it did.
            var held = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            configListener.Delay = (_, _) => messageHandler.Calls.Count < 3 ? Task.CompletedTask : held.Task;
            configListener.StartListening();

            // Every request throws, and the listener has to keep going regardless. A third attempt is
            // therefore proof that it retried after failing, and is awaited rather than polled for: a
            // listener which gives up hangs this test, which says what went wrong, instead of failing
            // a deadline that only says the runner was busy (NCBC-4293).
            await messageHandler.Calls.WaitForAsync(3);

            // Disposing has to stop it. DisposeAsync awaits the background loop, so its returning is
            // itself the proof that the loop has exited and can make no further calls — there is no
            // need to watch the call count and decide it has gone quiet enough.
            configListener.Dispose();
            held.TrySetResult(true);
            await configListener.DisposeAsync();

            // Exactly three, not merely at least three: held at the third, the count is settled, so
            // this says the listener retried twice after failing rather than only that it got going.
            Assert.Equal(3, messageHandler.Calls.Count);
        }

        [Fact]
        public async Task Every_Node_Failing_Backs_Off_Between_Rounds()
        {
            var messageHandler = new ThrowsEveryTimeMessageHandler();
            await using var configListener =
                CreateListener(nameof(Every_Node_Failing_Backs_Off_Between_Rounds), messageHandler);

            // Record what the listener asks to wait for between rounds, and hold it at the fourth
            // rather than let it run free. A zero-length wait completes without reaching a clock, so
            // recording the request is the only way to see that it did not wait at all.
            const int rounds = 4;
            var backoffs = new List<TimeSpan>();
            var rounded = new AsyncCounter();
            var held = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            configListener.Delay = (duration, _) =>
            {
                lock (backoffs)
                {
                    backoffs.Add(duration);
                }

                rounded.Increment();
                return rounded.Count < rounds ? Task.CompletedTask : held.Task;
            };

            configListener.StartListening();

            // The one node fails every time, so every round ends in a wait. Four is enough to show
            // whether the wait grows, and to reach the cap. Waiting for the fourth is waiting for the
            // listener, not for a clock: nothing here has a deadline to lose on a slow machine.
            await rounded.WaitForAsync(rounds);

            List<TimeSpan> waited;
            lock (backoffs)
            {
                waited = new List<TimeSpan>(backoffs);
            }

            // Held at the fourth wait, so no further round can run while the assertion reads this.
            configListener.Dispose();
            held.TrySetResult(true);
            await configListener.DisposeAsync();

            // Ten times longer each round, capped at MaxDelayMs: while every management endpoint is
            // failing the listener must not reattempt as fast as the failures come back, which is a
            // hot loop and the log failstorm the backoff exists to prevent.
            Assert.Equal(
                new[]
                {
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(10)
                },
                waited);
        }

        [Fact]
        public async Task Disposing_During_A_Backoff_Does_Not_Wait_It_Out()
        {
            var messageHandler = new ThrowsEveryTimeMessageHandler();
            await using var configListener =
                CreateListener(nameof(Disposing_During_A_Backoff_Does_Not_Wait_It_Out), messageHandler);

            // Stand in for a backoff which has only just begun: it ends when, and only when, the
            // listener's own token is cancelled. A backoff of up to ten seconds which ignored that
            // token would hold up DisposeAsync, and with it closing a bucket, for its full length.
            var backingOff = new AsyncCounter();
            var backoffToken = CancellationToken.None;
            configListener.Delay = (_, token) =>
            {
                backoffToken = token;
                backingOff.Increment();

                // Only simulate the wait when the token can actually end it. Given a token which
                // cannot be cancelled — the defect this guards — an unending wait would hang the test
                // in DisposeAsync rather than let the assertion below report what is wrong.
                return token.CanBeCanceled ? Task.Delay(Timeout.InfiniteTimeSpan, token) : Task.CompletedTask;
            };

            configListener.StartListening();
            await backingOff.WaitForAsync(1);

            // Fails here rather than hanging if the listener passes no token at all.
            Assert.True(backoffToken.CanBeCanceled, "The backoff was given a token which can never be cancelled.");

            // Returns only once the loop has finished, which it can only do by abandoning the wait.
            await configListener.DisposeAsync();

            Assert.True(backoffToken.IsCancellationRequested);
        }

        private static HttpStreamingConfigListener CreateListener(string testName,
            ThrowsEveryTimeMessageHandler messageHandler)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var clusterOptions = new ClusterOptions()
                .WithConnectionString($"couchbases://NOSUCHHOST{testName}")
                .WithCredentials("UnitTestUser", "PasswordDoesn'tMatter");
#pragma warning restore CS0618 // Type or member is obsolete

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
            clusterNode.Setup(x => x.ManagementUri).Returns(new Uri($"http://NOSUCHHOST{testName}:8091"));
            clusterNode.Setup(x => x.EndPoint).Returns(new HostEndpointWithPort($"NOSUCHHOST{testName}", 11210));
            nodeList.Add(clusterNode.Object);
            mockBucket.Object.Nodes.Add(clusterNode.Object);

            return new HttpStreamingConfigListener(mockBucket.Object,
                clusterOptions, httpClientFactory, configHandler, mockLogger);
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
