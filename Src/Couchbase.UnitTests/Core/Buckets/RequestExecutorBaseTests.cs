using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.Core.Buckets
{
    [TestFixture]
    public class RequestExecutorBaseTests
    {
        #region RetryRequestAsync

        [Test]
        public async Task RetryRequestAsync_NoCancellationToken_Timeout()
        {
            // Arrange

            var mockServer = new Mock<IServer>();

            // Act

            var didTimeout = await RequestExecuterBase.RetryRequestAsync(
                () => mockServer.Object,
                async (server, request, token) =>
                {
                    try
                    {
                        await Task.Delay(2000, token);

                        // Return false if we reached the 2000ms delay
                        return false;
                    }
                    catch (TaskCanceledException)
                    {
                        // Return true if cancelled due to timeout
                        return true;
                    }
                },
                (request, result) => false,
                new object(),
                CancellationToken.None,
                100);

            // Assert

            Assert.True(didTimeout);
        }

        [Test]
        public async Task RetryRequestAsync_CancellationToken_Timeout()
        {
            // Arrange

            var mockServer = new Mock<IServer>();

            var cts = new CancellationTokenSource(2000);

            // Act

            var didTimeout = await RequestExecuterBase.RetryRequestAsync(
                () => mockServer.Object,
                async (server, request, token) =>
                {
                    try
                    {
                        await Task.Delay(3000, token);

                        // Return false if we reached the 3000ms delay
                        return false;
                    }
                    catch (TaskCanceledException)
                    {
                        // Return true if cancelled due to timeout
                        // And the triggering token was not the cts
                        return !cts.IsCancellationRequested;
                    }
                },
                (request, result) => false,
                new object(),
                cts.Token,
                100);

            // Assert

            Assert.True(didTimeout);
        }

        [Test, Ignore("fails intermittently")]
        public async Task RetryRequestAsync_CancellationToken_CanBeCancelled()
        {
            // Arrange

            var mockServer = new Mock<IServer>();

            var cts = new CancellationTokenSource(100);

            // Act

            var wasCancelledByCts = await RequestExecuterBase.RetryRequestAsync(
                () => mockServer.Object,
                async (server, request, token) =>
                {
                    try
                    {
                        await Task.Delay(1000, token);

                        // Return false if we reached the 2000ms delay
                        return false;
                    }
                    catch (TaskCanceledException)
                    {
                        // Return true if cancelled due to cts
                        return true;
                    }
                },
                (request, result) => false,
                new object(),
                cts.Token,
                2000);

            // Assert

            Assert.True(wasCancelledByCts);
        }

        #endregion
    }
}
