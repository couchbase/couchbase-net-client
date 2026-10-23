using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Connections.Channels;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.IO.Connections.Channels
{
    // NCBC-4373: the pool recorded the bucket only after every select succeeded. After a failed select,
    // replacement connections opened with no bucket selected, so the node answered every operation for the
    // bucket with BucketNotConnected until the application restarted.
    [Collection("NonParallel")]
    public class ChannelConnectionPoolSelectBucketTests
    {
        private const string Bucket = "travel-sample";

        private readonly ITestOutputHelper _testOutput;
        private readonly HostEndpointWithPort _hostEndpoint = new("localhost", 9999);

        public ChannelConnectionPoolSelectBucketTests(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        [Fact]
        public async Task SelectBucketAsync_NoLiveConnections_ReplacementsSelectTheBucket()
        {
            // Arrange

            var harness = new Harness(initialConnections: 2);
            var pool = CreatePool(harness);
            await pool.InitializeAsync();
            harness.Connections.ForEach(c => c.Dead = true);

            // Act

            await Assert.ThrowsAsync<DocumentNotFoundException>(() => pool.SelectBucketAsync(Bucket));

            // A queued operation wakes the processors, which replace their dead connections.
            await pool.SendAsync(new FakeOperation());
            await harness.AllReplacementsCreated;

            // Assert

            // Replacements are added while the pool is locked, so freezing waits for them to finish.
            await using (await pool.FreezePoolAsync())
            {
                var replacements = harness.Connections.Skip(2).ToList();
                Assert.Equal(2, replacements.Count);
                Assert.All(replacements, r => Assert.Equal(Bucket, r.SelectedBucket));
            }
        }

        [Fact]
        public async Task SelectBucketAsync_OneSelectFails_ItsReplacementSelectsTheBucket()
        {
            // Arrange

            var harness = new Harness(initialConnections: 2, replacements: 1);
            var pool = CreatePool(harness);
            await pool.InitializeAsync();
            harness.Connections[0].FailSelect = true;

            // Act

            await Assert.ThrowsAnyAsync<Exception>(() => pool.SelectBucketAsync(Bucket));

            // The pool disposed the connection whose select failed; the next queued operation wakes its processor.
            await pool.SendAsync(new FakeOperation());
            await harness.AllReplacementsCreated;

            // Assert

            await using (await pool.FreezePoolAsync())
            {
                Assert.True(harness.Connections[0].Dead);
                Assert.Equal(Bucket, harness.Connections[1].SelectedBucket);
                Assert.Equal(Bucket, harness.Connections[2].SelectedBucket);
            }
        }

        #region Helpers

        private ChannelConnectionPool CreatePool(Harness harness) =>
            new(harness.Initializer, harness.Factory,
                new Mock<IConnectionPoolScaleController>().Object,
                new Redactor(RedactionLevel.None),
                new Logger(_testOutput),
                (int) new ClusterOptions().KvSendQueueCapacity)
            {
                MinimumSize = harness.InitialConnections,
                MaximumSize = harness.InitialConnections,
            };

        /// <summary>
        /// Creates connections whose death and bucket selection the test controls, and signals when the expected
        /// number of replacement connections has been created.
        /// </summary>
        private sealed class Harness
        {
            private readonly TaskCompletionSource<bool> _allReplacementsCreated =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Harness(int initialConnections, int? replacements = null)
            {
                InitialConnections = initialConnections;
                var expected = initialConnections + (replacements ?? initialConnections);

                var factory = new Mock<IConnectionFactory>();
                factory
                    .Setup(m => m.CreateAndConnectAsync(It.IsAny<HostEndpointWithPort>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() =>
                    {
                        var connection = new TestConnection();
                        lock (Connections)
                        {
                            Connections.Add(connection);
                        }

                        return connection.Object;
                    });
                Factory = factory.Object;

                var initializer = new Mock<IConnectionInitializer>();
                initializer.SetupGet(m => m.EndPoint).Returns(new HostEndpointWithPort("localhost", 9999));
                initializer
                    .Setup(m => m.InitializeConnectionAsync(It.IsAny<IConnection>(), It.IsAny<CancellationToken>()))
                    .Returns(() =>
                    {
                        lock (Connections)
                        {
                            if (Connections.Count == expected)
                            {
                                // The last replacement is being initialized; its select (if any) runs next,
                                // before the pool releases its lock.
                                _allReplacementsCreated.TrySetResult(true);
                            }
                        }

                        return Task.CompletedTask;
                    });
                initializer
                    .Setup(m => m.SelectBucketAsync(It.IsAny<IConnection>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns((IConnection c, string name, CancellationToken _) =>
                    {
                        var connection = Find(c);
                        if (connection.FailSelect)
                        {
                            return Task.FromException(new InvalidOperationException("select failed"));
                        }

                        connection.SelectedBucket = name;
                        return Task.CompletedTask;
                    });
                Initializer = initializer.Object;
            }

            public int InitialConnections { get; }

            public List<TestConnection> Connections { get; } = new();

            public IConnectionFactory Factory { get; }

            public IConnectionInitializer Initializer { get; }

            public Task AllReplacementsCreated => _allReplacementsCreated.Task;

            private TestConnection Find(IConnection c)
            {
                lock (Connections)
                {
                    return Connections.Single(t => t.Object == c);
                }
            }
        }

        private sealed class TestConnection
        {
            private volatile bool _dead;

            public TestConnection()
            {
                var mock = new Mock<IConnection>();
                mock.SetupGet(m => m.IsDead).Returns(() => _dead);
                mock.Setup(m => m.Dispose()).Callback(() => _dead = true);
                Object = mock.Object;
            }

            public IConnection Object { get; }

            public bool Dead
            {
                get => _dead;
                set => _dead = value;
            }

            public bool FailSelect { get; set; }

            public string SelectedBucket { get; set; }
        }

        private sealed class FakeOperation : OperationBase
        {
            public override OpCode OpCode => OpCode.NoOp;

            public override Task SendAsync(IConnection connection, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }

        private sealed class Logger : ILogger<ChannelConnectionPool>
        {
            private readonly ITestOutputHelper _testOutput;

            public Logger(ITestOutputHelper testOutput)
            {
                _testOutput = testOutput;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter)
            {
                try
                {
                    _testOutput.WriteLine(formatter(state, exception));
                }
                catch (InvalidOperationException)
                {
                    // Background pool work can log after the test has finished.
                }
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public IDisposable BeginScope<TState>(TState state) => null;
        }

        #endregion
    }
}
