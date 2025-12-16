using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.Authentication.Authenticators;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core;

public class ClusterReauthenticationTests
{
    #region Cluster.Authenticator() Tests

    /// <summary>
    /// When a JwtAuthenticator is set via Cluster.Authenticator(), the SDK must
    /// re-authenticate all existing KV connections with the new JWT token.
    /// </summary>
    [Fact]
    public async Task Authenticator_WithJwtAuthenticator_TriggersReauthentication()
    {
        // Arrange
        var reauthCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mockNode = CreateMockClusterNode(hasKv: true, isDead: false);
        mockNode
            .Setup(n => n.ReauthenticateKvConnectionsAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                reauthCompleted.TrySetResult(true);
                return Task.CompletedTask;
            });

        var options = CreateClusterOptions();
        var cluster = CreateClusterWithNodes(options, mockNode.Object);

        var jwtAuthenticator = new JwtAuthenticator("test-jwt-token");

        // Act
        cluster.Authenticator(jwtAuthenticator);

        // Wait for the fire-and-forget re-auth to complete (deterministic)
#if NET5_0_OR_GREATER
        await reauthCompleted.Task.WaitAsync(TimeSpan.FromSeconds(1));
#else
        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await reauthCompleted.Task.WaitAsync(cts.Token);
#endif


        // Assert
        Assert.Equal(jwtAuthenticator, options.Authenticator);
        mockNode.Verify(n => n.ReauthenticateKvConnectionsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// PasswordAuthenticator does not trigger re-authentication of existing connections.
    /// Password credentials are only used for new KV connections, existing connections continue
    /// with their original SASL authentication.
    /// </summary>
    [Fact]
    public void Authenticator_WithPasswordAuthenticator_DoesNotTriggerReauthentication()
    {
        // Arrange
        var mockNode = CreateMockClusterNode(hasKv: true, isDead: false);
        var options = CreateClusterOptions();
        options.WithPasswordAuthentication("username", "password");
        var cluster = CreateClusterWithNodes(options, mockNode.Object);

        var passwordAuthenticator = new PasswordAuthenticator("username", "password");

        // Act - PasswordAuthenticator does NOT trigger any async re-auth, so this is synchronous
        cluster.Authenticator(passwordAuthenticator);

        // Assert - No need to wait; PasswordAuthenticator path is synchronous (no fire-and-forget)
        Assert.Equal(passwordAuthenticator, options.Authenticator);
        mockNode.Verify(n => n.ReauthenticateKvConnectionsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Dead nodes should be skipped during re-authentication.
    /// </summary>
    [Fact]
    public async Task Authenticator_WithDeadNode_DoesNotReauthenticateThatNode()
    {
        // Arrange
        var liveNodeReauthCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var liveNode = CreateMockClusterNode(hasKv: true, isDead: false);
        liveNode
            .Setup(n => n.ReauthenticateKvConnectionsAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                liveNodeReauthCompleted.TrySetResult(true);
                return Task.CompletedTask;
            });

        var deadNode = CreateMockClusterNode(hasKv: true, isDead: true);
        var options = CreateClusterOptions();
        var cluster = CreateClusterWithNodes(options, liveNode.Object, deadNode.Object);

        var jwtAuthenticator = new JwtAuthenticator("test-jwt-token");

        // Act
        cluster.Authenticator(jwtAuthenticator);

        // Wait for the live node's re-auth to complete (deterministic)
#if NET5_0_OR_GREATER
        await liveNodeReauthCompleted.Task.WaitAsync(TimeSpan.FromSeconds(1));
#else
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await liveNodeReauthCompleted.Task.WaitAsync(cts.Token);
#endif

        // Assert
        liveNode.Verify(n => n.ReauthenticateKvConnectionsAsync(It.IsAny<CancellationToken>()), Times.Once);
        deadNode.Verify(n => n.ReauthenticateKvConnectionsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Non-KV nodes should be skipped during re-authentication.
    /// </summary>
    [Fact]
    public async Task Authenticator_WithNonKvNode_DoesNotReauthenticateThatNode()
    {
        // Arrange
        var kvNodeReauthCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var kvNode = CreateMockClusterNode(hasKv: true, isDead: false);
        kvNode
            .Setup(n => n.ReauthenticateKvConnectionsAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                kvNodeReauthCompleted.TrySetResult(true);
                return Task.CompletedTask;
            });

        var nonKvNode = CreateMockClusterNode(hasKv: false, isDead: false);
        var options = CreateClusterOptions();
        var cluster = CreateClusterWithNodes(options, kvNode.Object, nonKvNode.Object);

        var jwtAuthenticator = new JwtAuthenticator("test-jwt-token");

        // Act
        cluster.Authenticator(jwtAuthenticator);

        // Wait for the KV node's re-auth to complete (deterministic)
#if NET5_0_OR_GREATER
        await kvNodeReauthCompleted.Task.WaitAsync(TimeSpan.FromSeconds(1));
#else
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await kvNodeReauthCompleted.Task.WaitAsync(cts.Token);
#endif

        // Assert
        kvNode.Verify(n => n.ReauthenticateKvConnectionsAsync(It.IsAny<CancellationToken>()), Times.Once);
        nonKvNode.Verify(n => n.ReauthenticateKvConnectionsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Edge case: setting JwtAuthenticator when no nodes exist (e.g. before bootstrap completes)
    /// should not throw. The re-auth task will simply find no nodes to process and complete
    /// successfully.
    /// </summary>
    [Fact]
    public void Authenticator_NoNodes_CompletesSuccessfully()
    {
        // Arrange
        var options = CreateClusterOptions();
        var cluster = CreateClusterWithNodes(options); // No nodes

        var jwtAuthenticator = new JwtAuthenticator("test-jwt-token");

        // Act & Assert - Should not throw
        var exception = Record.Exception(() => cluster.Authenticator(jwtAuthenticator));
        Assert.Null(exception);
    }

    /// <summary>
    /// When Authenticator() is called multiple times, the last authenticator wins and should
    /// be immediately visible to the caller.
    /// </summary>
    [Fact]
    public void Authenticator_CalledMultipleTimes_SetsLatestAuthenticator()
    {
        // Arrange
        var options = CreateClusterOptions();
        var cluster = CreateClusterWithNodes(options);

        var jwtAuthenticator1 = new JwtAuthenticator("token-1");
        var jwtAuthenticator2 = new JwtAuthenticator("token-2");
        var jwtAuthenticator3 = new JwtAuthenticator("token-3");

        // Act
        cluster.Authenticator(jwtAuthenticator1);
        cluster.Authenticator(jwtAuthenticator2);
        cluster.Authenticator(jwtAuthenticator3);

        // Assert - Latest authenticator should be set
        Assert.Same(jwtAuthenticator3, options.Authenticator);
    }

    /// <summary>
    /// When a new JwtAuthenticator is set while re-authentication is still in progress from
    /// a previous call, the previous re-auth should be cancelled. This prevents race conditions
    /// where connections might be re-authenticated with an outdated token while a newer token
    /// is available. The cancellation is done via a linked CancellationTokenSource.
    /// </summary>
    [Fact]
    public async Task Authenticator_CalledMultipleTimes_CancelsAndRestartsReauthentication()
    {
        // Arrange
        var mockNode = CreateMockClusterNode(hasKv: true, isDead: false);

        // Synchronization: first re-auth will wait here until we signal it
        var firstReauthStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstReauthCanProceed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstCancellationToken = default(CancellationToken);
        var callCount = 0;

        mockNode
            .Setup(n => n.ReauthenticateKvConnectionsAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async ct =>
            {
                var currentCall = Interlocked.Increment(ref callCount);
                if (currentCall == 1)
                {
                    // First call: capture the token and wait for signal
                    firstCancellationToken = ct;
                    firstReauthStarted.SetResult(true);
                    await firstReauthCanProceed.Task;
                }
                // Second+ calls complete immediately
            });

        var options = CreateClusterOptions();
        var cluster = CreateClusterWithNodes(options, mockNode.Object);

        cluster.Authenticator(new JwtAuthenticator("token-1"));

        // Wait for first re-auth to actually start
        await firstReauthStarted.Task;

        // Trigger second re-auth while first is still in progress
        cluster.Authenticator(new JwtAuthenticator("token-2"));

        // Allow the first re-auth to proceed (it should observe the cancellation)
        firstReauthCanProceed.SetResult(true);

        //First token should have been cancelled when second Authenticator() was called
        Assert.True(firstCancellationToken.IsCancellationRequested,
            "First re-auth CancellationToken should be cancelled when Authenticator() is called again");
    }

    #endregion

    #region Dispose Tests

    /// <summary>
    /// When Cluster.Dispose() is called, any ongoing re-authentication task should be cancelled.
    /// Failing to cancel could cause operations to continue on disposed resources.
    /// </summary>
    [Fact]
    public async Task Dispose_CancelsOngoingReauthentication()
    {
        // Arrange
        var reauthStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reauthCancellationObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var mockNode = CreateMockClusterNode(hasKv: true, isDead: false);
        mockNode
            .Setup(n => n.ReauthenticateKvConnectionsAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async ct =>
            {
                reauthStarted.TrySetResult(true);
                try
                {
                    // Wait indefinitely until cancelled
                    await Task.Delay(Timeout.Infinite, ct);
                }
                catch (OperationCanceledException)
                {
                    reauthCancellationObserved.TrySetResult(true);
                    throw;
                }
            });

        var options = CreateClusterOptions();
        var cluster = CreateClusterWithNodes(options, mockNode.Object);

        // Act
        cluster.Authenticator(new JwtAuthenticator("test-token"));

        // Wait for re-auth to start (deterministic)
#if NET5_0_OR_GREATER
        await reauthStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
#else
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await reauthStarted.Task.WaitAsync(cts.Token);
#endif

        // Now dispose the cluster - this should cancel the re-auth
        cluster.Dispose();

        // Wait for cancellation to be observed (deterministic, with timeout for safety)
#if NET5_0_OR_GREATER
        var cancelled = await reauthCancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
#else
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var cancelled = await reauthCancellationObserved.Task.WaitAsync(cts2.Token);
#endif

        // Assert
        Assert.True(cancelled, "Re-auth should have been cancelled when Cluster.Dispose() was called");
    }

    #endregion

    #region Helper Methods

    private static ClusterOptions CreateClusterOptions()
    {
        return new ClusterOptions()
            .WithJwtAuthentication("initial-token")
            .WithConnectionString("couchbases://localhost");
    }

    private static Mock<IClusterNode> CreateMockClusterNode(bool hasKv, bool isDead)
    {
        var mockNode = new Mock<IClusterNode>();
        mockNode.SetupGet(n => n.HasKv).Returns(hasKv);
        mockNode.SetupGet(n => n.IsDead).Returns(isDead);
        mockNode.SetupGet(n => n.EndPoint).Returns(new HostEndpointWithPort("localhost", 11210));
        mockNode
            .Setup(n => n.ReauthenticateKvConnectionsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mockNode;
    }

    private static Cluster CreateClusterWithNodes(ClusterOptions options, params IClusterNode[] nodes)
    {
        // Use the Mock<Cluster> pattern from ClusterTests.cs
        var mockCluster = new Mock<Cluster>(options)
        {
            CallBase = true
        };

        // Setup EnsureBootstrapped to prevent actual bootstrapping
        mockCluster
            .Setup(c => c.EnsureBootstrapped())
            .Returns(Task.CompletedTask);

        var cluster = mockCluster.Object;

        // Access ClusterContext through reflection to add nodes
        var contextField = typeof(Cluster).GetField("_context",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var context = (ClusterContext)contextField?.GetValue(cluster);
        if (context != null)
        {
            foreach (var node in nodes)
            {
                context.Nodes.Add(node);
            }
        }

        return cluster;
    }

    #endregion
}
