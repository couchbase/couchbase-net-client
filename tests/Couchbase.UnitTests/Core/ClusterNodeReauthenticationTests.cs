using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Authentication.Authenticators;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Test.Common.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.ObjectPool;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core;
/// <summary>
/// Tests for ClusterNode.ReauthenticateKvConnectionsAsync() which handles re-authentication
/// of individual connections within a node's connection pool when a new JWT token is provided.
/// </summary>
public class ClusterNodeReauthenticationTests
{
    private readonly ITestOutputHelper _outputHelper;

    public ClusterNodeReauthenticationTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }

    #region ReauthenticateKvConnectionsAsync Tests

    /// <summary>
    /// When using JwtAuthenticator, all connections in the pool should be re-authenticated
    /// in parallel using SASL OAUTHBEARER with the new token.
    /// </summary>
    [Fact]
    public async Task ReauthenticateKvConnectionsAsync_WithJwtAuthenticator_ReauthenticatesAllConnections()
    {
        // Arrange
        var connections = new List<Mock<IConnection>>
        {
            CreateMockConnection(1),
            CreateMockConnection(2),
            CreateMockConnection(3)
        };

        var mockConnectionPool = new Mock<IConnectionPool>();
        mockConnectionPool
            .Setup(p => p.GetConnections())
            .Returns(connections.ConvertAll(c => c.Object));
        mockConnectionPool
            .SetupGet(p => p.Size)
            .Returns(connections.Count);
        SetupPoolRefillDefaults(mockConnectionPool);

        var mockSaslMechanismFactory = new Mock<ISaslMechanismFactory>();
        var mockMechanism = new Mock<ISaslMechanism>();
        mockMechanism
            .Setup(m => m.AuthenticateAsync(It.IsAny<IConnection>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockSaslMechanismFactory
            .Setup(f => f.CreateOAuthBearerMechanism(It.IsAny<string>()))
            .Returns(mockMechanism.Object);

        var jwtAuthenticator = new JwtAuthenticator("test-jwt-token");
        var clusterNode = CreateClusterNode(mockConnectionPool.Object, mockSaslMechanismFactory.Object, jwtAuthenticator);

        // Act
        await clusterNode.ReauthenticateKvConnectionsAsync();

        // Assert - All connections should have been authenticated
        mockSaslMechanismFactory.Verify(
            f => f.CreateOAuthBearerMechanism(It.Is<string>(t => t == "test-jwt-token")),
            Times.Exactly(3));
        mockMechanism.Verify(
            m => m.AuthenticateAsync(It.IsAny<IConnection>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    /// <summary>
    /// Per RFC, PasswordAuthenticator should NOT re-authenticate existing connections.
    /// The method should return immediately without attempting any authentication.
    /// Password-based auth is only done at connection creation time.
    /// </summary>
    [Fact]
    public async Task ReauthenticateKvConnectionsAsync_WithPasswordAuthenticator_DoesNothing()
    {
        // Arrange
        var connections = new List<Mock<IConnection>>
        {
            CreateMockConnection(1),
            CreateMockConnection(2)
        };

        var mockConnectionPool = new Mock<IConnectionPool>();
        mockConnectionPool
            .Setup(p => p.GetConnections())
            .Returns(connections.ConvertAll(c => c.Object));
        mockConnectionPool
            .SetupGet(p => p.Size)
            .Returns(connections.Count);

        var mockSaslMechanismFactory = new Mock<ISaslMechanismFactory>();
        var passwordAuthenticator = new PasswordAuthenticator("user", "pass");
        var clusterNode = CreateClusterNode(mockConnectionPool.Object, mockSaslMechanismFactory.Object, passwordAuthenticator);

        // Act
        await clusterNode.ReauthenticateKvConnectionsAsync();

        // Assert - No authentication attempts should have been made
        mockSaslMechanismFactory.Verify(
            f => f.CreateOAuthBearerMechanism(It.IsAny<string>()),
            Times.Never);
    }

    /// <summary>
    /// Edge case: the connection pool has already drained to zero (e.g. all connections
    /// died when the previous JWT expired). Re-authentication must proactively refill the
    /// pool to MinimumSize using the freshly-swapped authenticator — otherwise every KV
    /// op times out until the 30s scale-controller tick fires.
    /// </summary>
    [Fact]
    public async Task ReauthenticateKvConnectionsAsync_EmptyPool_RefillsToMinimumSize()
    {
        // Arrange
        var mockConnectionPool = new Mock<IConnectionPool>();
        mockConnectionPool
            .Setup(p => p.GetConnections())
            .Returns(Array.Empty<IConnection>());
        mockConnectionPool.SetupGet(p => p.Size).Returns(0);
        SetupPoolRefillDefaults(mockConnectionPool);
        mockConnectionPool.SetupGet(p => p.MinimumSize).Returns(2);

        var mockSaslMechanismFactory = new Mock<ISaslMechanismFactory>();
        var jwtAuthenticator = new JwtAuthenticator("test-jwt-token");
        var clusterNode = CreateClusterNode(mockConnectionPool.Object, mockSaslMechanismFactory.Object, jwtAuthenticator);

        // Act & Assert - Should not throw
        var exception = await Record.ExceptionAsync(() => clusterNode.ReauthenticateKvConnectionsAsync());
        Assert.Null(exception);

        // Pool should be asked to refill back to MinimumSize under a freeze.
        mockConnectionPool.Verify(p => p.FreezePoolAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockConnectionPool.Verify(p => p.ScaleAsync(2), Times.Once);
    }

    /// <summary>
    /// When re-authentication fails for a connection (e.g. token rejected by server),
    /// the connection should be closed gracefully using CloseAsync() instead of Dispose().
    /// </summary>
    [Fact]
    public async Task ReauthenticateKvConnectionsAsync_ConnectionFails_ClosesConnectionGracefully()
    {
        // Arrange
        var mockConnection = CreateMockConnection(1);

        var mockConnectionPool = new Mock<IConnectionPool>();
        mockConnectionPool
            .Setup(p => p.GetConnections())
            .Returns(new[] { mockConnection.Object });
        mockConnectionPool
            .SetupGet(p => p.Size)
            .Returns(1);
        SetupPoolRefillDefaults(mockConnectionPool);

        var mockSaslMechanismFactory = new Mock<ISaslMechanismFactory>();
        var mockMechanism = new Mock<ISaslMechanism>();
        mockMechanism
            .Setup(m => m.AuthenticateAsync(It.IsAny<IConnection>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AuthenticationFailureException("Auth failed"));
        mockSaslMechanismFactory
            .Setup(f => f.CreateOAuthBearerMechanism(It.IsAny<string>()))
            .Returns(mockMechanism.Object);

        var jwtAuthenticator = new JwtAuthenticator("test-jwt-token");
        var clusterNode = CreateClusterNode(mockConnectionPool.Object, mockSaslMechanismFactory.Object, jwtAuthenticator);

        // Act - Should not throw
        var exception = await Record.ExceptionAsync(() => clusterNode.ReauthenticateKvConnectionsAsync());

        // Assert
        Assert.Null(exception);
        // Verify CloseAsync was called with a timeout
        mockConnection.Verify(c => c.CloseAsync(It.Is<TimeSpan>(ts => ts > TimeSpan.Zero)), Times.Once);
        // Verify Dispose was not called directly
        mockConnection.Verify(c => c.Dispose(), Times.Never);
    }

    /// <summary>
    /// Re-authentication is done in parallel across all connections. If one connection fails
    /// to re-authenticate, the others should still be attempted.
    /// Only the failing connection gets closed, successful ones remain open.
    /// </summary>
    [Fact]
    public async Task ReauthenticateKvConnectionsAsync_ConnectionFails_OtherConnectionsContinue()
    {
        // Arrange
        var failingConnection = CreateMockConnection(1);
        var successfulConnection1 = CreateMockConnection(2);
        var successfulConnection2 = CreateMockConnection(3);

        var mockConnectionPool = new Mock<IConnectionPool>();
        mockConnectionPool
            .Setup(p => p.GetConnections())
            .Returns(new[] { failingConnection.Object, successfulConnection1.Object, successfulConnection2.Object });
        mockConnectionPool
            .SetupGet(p => p.Size)
            .Returns(3);
        SetupPoolRefillDefaults(mockConnectionPool);

        var mockSaslMechanismFactory = new Mock<ISaslMechanismFactory>();

        var callCount = 0;
        var mockMechanism = new Mock<ISaslMechanism>();
        mockMechanism
            .Setup(m => m.AuthenticateAsync(It.IsAny<IConnection>(), It.IsAny<CancellationToken>()))
            .Returns<IConnection, CancellationToken>((conn, ct) =>
            {
                Interlocked.Increment(ref callCount);
                // Fail for the first connection
                if (conn.ConnectionId == 1)
                {
                    throw new AuthenticationFailureException("Auth failed for connection 1");
                }
                return Task.CompletedTask;
            });
        mockSaslMechanismFactory
            .Setup(f => f.CreateOAuthBearerMechanism(It.IsAny<string>()))
            .Returns(mockMechanism.Object);

        var jwtAuthenticator = new JwtAuthenticator("test-jwt-token");
        var clusterNode = CreateClusterNode(mockConnectionPool.Object, mockSaslMechanismFactory.Object, jwtAuthenticator);

        // Act
        await clusterNode.ReauthenticateKvConnectionsAsync();

        // Assert - All connections should have been attempted
        Assert.Equal(3, callCount);
        // Failing connection should be closed
        failingConnection.Verify(c => c.CloseAsync(It.IsAny<TimeSpan>()), Times.Once);
        // Successful connections should NOT be closed
        successfulConnection1.Verify(c => c.CloseAsync(It.IsAny<TimeSpan>()), Times.Never);
        successfulConnection2.Verify(c => c.CloseAsync(It.IsAny<TimeSpan>()), Times.Never);
    }

    /// <summary>
    /// When the cancellation token is triggered (e.g. because Cluster.Dispose() was called
    /// or a new JWT was provided), the re-authentication should stop and propagate the
    /// OperationCanceledException. Connections that haven't been re-authed yet will be
    /// handled by the next re-auth attempt or will be replaced by the pool.
    /// </summary>
    [Fact]
    public async Task ReauthenticateKvConnectionsAsync_Cancelled_ThrowsOperationCancelledException()
    {
        // Arrange
        var mockConnection = CreateMockConnection(1);

        var mockConnectionPool = new Mock<IConnectionPool>();
        mockConnectionPool
            .Setup(p => p.GetConnections())
            .Returns(new[] { mockConnection.Object });
        mockConnectionPool
            .SetupGet(p => p.Size)
            .Returns(1);

        var mockSaslMechanismFactory = new Mock<ISaslMechanismFactory>();
        var mockMechanism = new Mock<ISaslMechanism>();
        mockMechanism
            .Setup(m => m.AuthenticateAsync(It.IsAny<IConnection>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        mockSaslMechanismFactory
            .Setup(f => f.CreateOAuthBearerMechanism(It.IsAny<string>()))
            .Returns(mockMechanism.Object);

        var jwtAuthenticator = new JwtAuthenticator("test-jwt-token");
        var clusterNode = CreateClusterNode(mockConnectionPool.Object, mockSaslMechanismFactory.Object, jwtAuthenticator);

        // Create a pre-cancelled token
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => clusterNode.ReauthenticateKvConnectionsAsync(cts.Token));
    }

    /// <summary>
    /// If the ClusterNode has been disposed (IsDead=true), ReauthenticateKvConnectionsAsync
    /// should return immediately without attempting any work.
    /// </summary>
    [Fact]
    public async Task ReauthenticateKvConnectionsAsync_WhenDisposed_ReturnsImmediately()
    {
        // Arrange
        var mockConnectionPool = new Mock<IConnectionPool>();
        mockConnectionPool
            .Setup(p => p.GetConnections())
            .Returns(Array.Empty<IConnection>());

        var mockSaslMechanismFactory = new Mock<ISaslMechanismFactory>();
        var jwtAuthenticator = new JwtAuthenticator("test-jwt-token");
        var clusterNode = CreateClusterNode(mockConnectionPool.Object, mockSaslMechanismFactory.Object, jwtAuthenticator);

        // Dispose the node
        clusterNode.Dispose();

        // Act & Assert - Should not throw, should return immediately
        var exception = await Record.ExceptionAsync(() => clusterNode.ReauthenticateKvConnectionsAsync());
        Assert.Null(exception);

        // GetConnections should not be called since node is disposed
        mockConnectionPool.Verify(p => p.GetConnections(), Times.Never);
    }

    /// <summary>
    /// When every connection fails re-auth (e.g. the new JWT is still invalid on the server),
    /// each connection is graceful-closed and the pool must still be refilled back to
    /// MinimumSize. This guards against the failure mode where all connections get stuck in
    /// the _closing graceful window, and the pool is never repopulated.
    /// </summary>
    [Fact]
    public async Task ReauthenticateKvConnectionsAsync_AllReauthsFail_RefillsPool()
    {
        // Arrange
        var connections = new List<Mock<IConnection>>
        {
            CreateMockConnection(1),
            CreateMockConnection(2)
        };

        // By the time we enter the refill path, the graceful-closed connections have been
        // evicted by the pool (IsDead now returns true once _closing is set, so the pool
        // processor removes them promptly). Reflect that by reporting Size=0.
        var mockConnectionPool = new Mock<IConnectionPool>();
        mockConnectionPool
            .Setup(p => p.GetConnections())
            .Returns(connections.ConvertAll(c => c.Object));
        mockConnectionPool.SetupGet(p => p.Size).Returns(0);
        SetupPoolRefillDefaults(mockConnectionPool);
        mockConnectionPool.SetupGet(p => p.MinimumSize).Returns(2);

        var mockSaslMechanismFactory = new Mock<ISaslMechanismFactory>();
        var mockMechanism = new Mock<ISaslMechanism>();
        mockMechanism
            .Setup(m => m.AuthenticateAsync(It.IsAny<IConnection>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AuthenticationFailureException("Auth failed"));
        mockSaslMechanismFactory
            .Setup(f => f.CreateOAuthBearerMechanism(It.IsAny<string>()))
            .Returns(mockMechanism.Object);

        var jwtAuthenticator = new JwtAuthenticator("test-jwt-token");
        var clusterNode = CreateClusterNode(mockConnectionPool.Object, mockSaslMechanismFactory.Object, jwtAuthenticator);

        // Act
        await clusterNode.ReauthenticateKvConnectionsAsync();

        // Assert
        foreach (var c in connections)
        {
            c.Verify(x => x.CloseAsync(It.IsAny<TimeSpan>()), Times.Once);
        }
        mockConnectionPool.Verify(p => p.FreezePoolAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockConnectionPool.Verify(p => p.ScaleAsync(2), Times.Once);
    }

    /// <summary>
    /// Freeze-before-scale contract: ScaleAsync must be called only after FreezePoolAsync has
    /// produced its disposable (per IConnectionPool doc: "the caller has already frozen the
    /// pool before calling ScalePool").
    /// </summary>
    [Fact]
    public async Task ReauthenticateKvConnectionsAsync_PoolBelowMinimum_ScalesUpUnderFreeze()
    {
        // Arrange
        var mockConnectionPool = new Mock<IConnectionPool>();
        mockConnectionPool
            .Setup(p => p.GetConnections())
            .Returns(Array.Empty<IConnection>());
        mockConnectionPool.SetupGet(p => p.Size).Returns(0);
        mockConnectionPool.SetupGet(p => p.MinimumSize).Returns(3);
        mockConnectionPool.Setup(p => p.ScaleAsync(It.IsAny<int>())).Returns(Task.CompletedTask);

        var events = new List<string>();
        var mockFreezeDisposable = new Mock<IAsyncDisposable>();
        mockFreezeDisposable
            .Setup(d => d.DisposeAsync())
            .Callback(() => events.Add("unfreeze"))
            .Returns(default(ValueTask));
        mockConnectionPool
            .Setup(p => p.FreezePoolAsync(It.IsAny<CancellationToken>()))
            .Callback(() => events.Add("freeze"))
            .Returns(new ValueTask<IAsyncDisposable>(mockFreezeDisposable.Object));
        mockConnectionPool
            .Setup(p => p.ScaleAsync(It.IsAny<int>()))
            .Callback<int>(_ => events.Add("scale"))
            .Returns(Task.CompletedTask);

        var mockSaslMechanismFactory = new Mock<ISaslMechanismFactory>();
        var jwtAuthenticator = new JwtAuthenticator("test-jwt-token");
        var clusterNode = CreateClusterNode(mockConnectionPool.Object, mockSaslMechanismFactory.Object, jwtAuthenticator);

        // Act
        await clusterNode.ReauthenticateKvConnectionsAsync();

        // Assert - freeze, then scale, then unfreeze.
        Assert.Equal(new[] { "freeze", "scale", "unfreeze" }, events);
    }

    #endregion

    #region Helper Methods

    private static Mock<IConnection> CreateMockConnection(ulong connectionId)
    {
        var mockConnection = new Mock<IConnection>();
        mockConnection.SetupGet(c => c.ConnectionId).Returns(connectionId);
        mockConnection.SetupGet(c => c.IsDead).Returns(false);
        mockConnection.SetupGet(c => c.IsConnected).Returns(true);
        mockConnection.SetupGet(c => c.EndPoint).Returns(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 11210));
        mockConnection.Setup(c => c.CloseAsync(It.IsAny<TimeSpan>())).Returns(default(ValueTask));
        return mockConnection;
    }

    /// <summary>
    /// Sets up <see cref="IConnectionPool.FreezePoolAsync"/>, <see cref="IConnectionPool.MinimumSize"/>,
    /// and <see cref="IConnectionPool.ScaleAsync"/> with inert defaults so that the refill code path at
    /// the end of ReauthenticateKvConnectionsAsync is a no-op unless the test overrides these.
    /// </summary>
    private static void SetupPoolRefillDefaults(Mock<IConnectionPool> mockConnectionPool)
    {
        var mockFreezeDisposable = new Mock<IAsyncDisposable>();
        mockFreezeDisposable.Setup(d => d.DisposeAsync()).Returns(default(ValueTask));
        mockConnectionPool
            .Setup(p => p.FreezePoolAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IAsyncDisposable>(mockFreezeDisposable.Object));
        mockConnectionPool.SetupGet(p => p.MinimumSize).Returns(0);
        mockConnectionPool.Setup(p => p.ScaleAsync(It.IsAny<int>())).Returns(Task.CompletedTask);
    }

    private ClusterNode CreateClusterNode(
        IConnectionPool connectionPool,
        ISaslMechanismFactory saslMechanismFactory,
        IAuthenticator authenticator)
    {
        var clusterOptions = new ClusterOptions()
            .WithConnectionString("couchbases://localhost");
        clusterOptions.Authenticator = authenticator;

        var context = new ClusterContext(null, clusterOptions);
        var pool = new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy());
        var loggerFactory = new TestOutputLoggerFactory(_outputHelper);
        var logger = new Logger<ClusterNode>(loggerFactory);

        var mockConnectionPoolFactory = new Mock<IConnectionPoolFactory>();
        mockConnectionPoolFactory
            .Setup(m => m.Create(It.IsAny<ClusterNode>()))
            .Returns(connectionPool);

        var node = new ClusterNode(
            context: context,
            connectionPoolFactory: mockConnectionPoolFactory.Object,
            logger: logger,
            operationBuilderPool: pool,
            circuitBreaker: new CircuitBreaker(TimeProvider.System, new CircuitBreakerConfiguration { Enabled = false }),
            saslMechanismFactory: saslMechanismFactory,
            redactor: new TypedRedactor(RedactionLevel.None),
            endPoint: new HostEndpointWithPort("localhost", 11210),
            nodeAdapter: new NodeAdapter { Hostname = "localhost" },
            tracer: new NoopRequestTracer(),
            operationConfigurator: new Mock<IOperationConfigurator>().Object);

        return node;
    }

    #endregion
}
