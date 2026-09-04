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

            // await using, not using: the sync Dispose only signals the loop to stop, DisposeAsync
            // waits for it to finish.
            await using var configListener =
                CreateListener(nameof(Should_Continue_After_Failures), messageHandler);

            // Skip the backoff rather than serve it — this test is about carrying on after a failure,
            // not about timing — and hold at the third attempt so the count settles.
            var held = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            configListener.Delay = (_, _) => messageHandler.Calls.Count < 3 ? Task.CompletedTask : held.Task;
            configListener.StartListening();

            // Every request throws, so a third attempt is proof it retried. Awaited, not polled: a
            // listener which gives up hangs the test rather than missing a deadline (NCBC-4293).
            await messageHandler.Calls.WaitForAsync(3);

            // DisposeAsync awaits the loop, so its returning is itself proof the loop has exited.
            configListener.Dispose();
            held.TrySetResult(true);
            await configListener.DisposeAsync();

            // Exactly three, not at least three: it retried twice after failing.
            Assert.Equal(3, messageHandler.Calls.Count);
        }

        [Fact]
        public async Task Every_Node_Failing_Backs_Off_Between_Rounds()
        {
            var messageHandler = new ThrowsEveryTimeMessageHandler();
            await using var configListener =
                CreateListener(nameof(Every_Node_Failing_Backs_Off_Between_Rounds), messageHandler);

            // Record what the listener asks to wait for, and hold at the fourth. A zero-length wait
            // never reaches a clock, so the request is the only way to see it did not wait.
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

            // Four rounds is enough to show the wait growing and reaching the cap.
            await rounded.WaitForAsync(rounds);

            List<TimeSpan> waited;
            lock (backoffs)
            {
                waited = new List<TimeSpan>(backoffs);
            }

            // Held at the fourth, so no further round can run while this is read.
            configListener.Dispose();
            held.TrySetResult(true);
            await configListener.DisposeAsync();

            // Ten times longer each round, capped at MaxDelayMs: with every endpoint failing the
            // listener must not reattempt as fast as the failures come back.
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

            // Stand in for a backoff just begun: it ends only when the listener's token is cancelled.
            var backingOff = new AsyncCounter();
            var backoffToken = CancellationToken.None;
            configListener.Delay = (_, token) =>
            {
                backoffToken = token;
                backingOff.Increment();

                // Only wait when the token can end it: given one which cannot — the defect this
                // guards — an unending wait would hang the test instead of failing the assert below.
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

        [Fact]
        public async Task Disposing_Mid_Round_Does_Not_Try_The_Remaining_Nodes()
        {
            var messageHandler = new ThrowsEveryTimeMessageHandler();
            await using var configListener = CreateListener(
                nameof(Disposing_Mid_Round_Does_Not_Try_The_Remaining_Nodes), messageHandler, nodeCount: 3);

            // Park the round inside the first node's request, so the close below lands with two nodes
            // still to go. Each one tried would log its cancellation as "HTTP Streaming error." — a
            // failstorm of one entry per management node on an ordinary bucket close.
            var atFirstNode = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            messageHandler.OnCall = () =>
            {
                atFirstNode.TrySetResult(true);
                release.Task.GetAwaiter().GetResult();
            };

            configListener.StartListening();
            await atFirstNode.Task;

            // Only the test thread disposes: two threads through Dispose can Cancel a source the
            // other has already disposed.
            configListener.Dispose();
            release.TrySetResult(true);

            // Returns only once the loop has exited, so the count is final.
            await configListener.DisposeAsync();

            Assert.Equal(1, messageHandler.Calls.Count);
        }

        private static HttpStreamingConfigListener CreateListener(string testName,
            ThrowsEveryTimeMessageHandler messageHandler, int nodeCount = 1)
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
            for (var i = 0; i < nodeCount; i++)
            {
                var host = $"NOSUCHHOST{testName}{i}";
                var clusterNode = new Mock<IClusterNode>();
                clusterNode.Setup(x => x.NodesAdapter).Returns(new NodeAdapter
                {
                    MgmtApi = 8091,
                    MgmtApiSsl = 18091
                });

                clusterNode.Setup(x => x.HasManagement).Returns(true);
                clusterNode.Setup(x => x.KeyEndPoints).Returns(new ReadOnlyObservableCollection<HostEndpointWithPort>(new ObservableCollection<HostEndpointWithPort>()));
                clusterNode.Setup(x => x.ManagementUri).Returns(new Uri($"http://{host}:8091"));
                clusterNode.Setup(x => x.EndPoint).Returns(new HostEndpointWithPort(host, 11210));
                nodeList.Add(clusterNode.Object);
                mockBucket.Object.Nodes.Add(clusterNode.Object);
            }

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

            /// <summary>
            /// Runs on each call, before it fails, for a test which needs something to happen while the
            /// listener is part-way through a round.
            /// </summary>
            public Action OnCall { get; set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls.Increment();
                OnCall?.Invoke();
                throw new NotImplementedException();
            }
        }
    }
}
