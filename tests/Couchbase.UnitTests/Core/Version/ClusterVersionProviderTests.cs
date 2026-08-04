using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Version;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.Version
{
    public class ClusterVersionProviderTests
    {
        // Never completes on its own; only responds to the CancellationToken it's given, so the test
        // can trigger the cancellation while a "download" is in flight rather than before it starts.
        private class HangingClusterVersionProvider : ClusterVersionProvider
        {
            public HangingClusterVersionProvider(ClusterContext clusterContext, ILogger<ClusterVersionProvider> logger)
                : base(clusterContext, logger)
            {
            }

            protected override async Task<Pools> DownloadConfigAsync(HttpClient httpClient, Uri server,
                CancellationToken cancellationToken)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                return null!; // unreachable; Task.Delay(Timeout.Infinite, ...) only ever throws
            }
        }

        [Fact]
        public async Task GetVersionAsync_CallerTokenCancelledMidRequest_ThrowsOperationCanceled()
        {
            using var clusterContextCts = new CancellationTokenSource();
            using var clusterContext = new ClusterContext(clusterContextCts,
                new ClusterOptions().WithPasswordAuthentication("username", "password"));

            var mockNode = new Mock<IClusterNode>();
            mockNode.Setup(n => n.ManagementUri).Returns(new Uri("http://localhost:8091"));
            clusterContext.Nodes.Add(mockNode.Object);

            var provider = new HangingClusterVersionProvider(clusterContext, new Mock<ILogger<ClusterVersionProvider>>().Object);

            using var callerCts = new CancellationTokenSource();
            var versionTask = provider.GetVersionAsync(callerCts.Token).AsTask();

            // Cancel once the request is in flight (hung inside DownloadConfigAsync), not before it starts,
            // so this exercises the per-server catch block rather than the loop's upfront cancellation check.
            callerCts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => versionTask);
        }

        // Extension-method dispatch: covers ClusterVersionProviderExtensions.GetVersionAsync directly,
        // independent of the concrete ClusterVersionProvider, since IClusterVersionProvider is a public
        // interface any external implementer could satisfy without supporting cancellation at all.

        [Fact]
        public async Task Extension_GetVersionAsync_DispatchesToCancellableOverload_WhenProviderSupportsIt()
        {
            var version = new ClusterVersion(new System.Version(7, 6, 2));
            var mockProvider = new Mock<IClusterVersionProviderCancellable>();
            mockProvider.Setup(p => p.GetVersionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(version);

            using var cts = new CancellationTokenSource();
            var result = await ((IClusterVersionProvider)mockProvider.Object).GetVersionAsync(cts.Token);

            Assert.Equal(version, result);
            mockProvider.Verify(p => p.GetVersionAsync(cts.Token), Times.Once);
            mockProvider.Verify(p => p.GetVersionAsync(), Times.Never);
        }

        [Fact]
        public async Task Extension_GetVersionAsync_IgnoresToken_WhenProviderDoesNotSupportCancellation()
        {
            // A plain IClusterVersionProvider implementation (no cancellation support at all) - this is
            // what an external implementer written against the pre-existing interface looks like.
            var version = new ClusterVersion(new System.Version(7, 6, 2));
            var mockProvider = new Mock<IClusterVersionProvider>();
            mockProvider.Setup(p => p.GetVersionAsync()).ReturnsAsync(version);

            // Token is already cancelled - proves we don't fake cancellation by throwing anyway; the
            // token is silently ignored because there's no way to actually honor it here.
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await mockProvider.Object.GetVersionAsync(cts.Token);

            Assert.Equal(version, result);
            mockProvider.Verify(p => p.GetVersionAsync(), Times.Once);
        }
    }
}
