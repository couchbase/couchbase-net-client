using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers.CarrierPublication;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.Configuration.Server.Providers.CarrierPublication
{
    [TestFixture]
    public class CarrierPublicationProviderTests
    {
        #region heartbeat

        [Test]
        public void ctor_EnableConfigHeartbeat_TriggersHeartbeat()
        {
            // Arrange

            var clientConfig = new ClientConfiguration()
            {
                EnableConfigHeartBeat = true,
                HeartbeatConfigInterval = 20 //ms
            };

            var tcs = new TaskCompletionSource<int>();

            var mockProvider = new Mock<CarrierPublicationProvider>(clientConfig, null, null, null, null, null)
            {
                CallBase = true
            };
            mockProvider
                .Setup(m => m.GetUpdatedConfig())
                .Callback(() => tcs.SetResult(0));

            // Act

            bool result;
            var obj = mockProvider.Object;
            try
            {
                // Wait 50 milliseconds to see if the heartbeat fires
                result = Task.WaitAll(new Task[] { tcs.Task }, 50);
            }
            finally
            {
                mockProvider.Object.Dispose();
            }

            // Assert

            Assert.True(result);
        }

        [Test]
        public void ctor_EnableConfigHeartbeat_KeepsFiring()
        {
            // Arrange

            var clientConfig = new ClientConfiguration()
            {
                EnableConfigHeartBeat = true,
                HeartbeatConfigInterval = 20 //ms
            };

            var onIndex = 0;
            var tcs = new[] {
                new TaskCompletionSource<int>(),
                new TaskCompletionSource<int>(),
                new TaskCompletionSource<int>()
            };

            var mockProvider = new Mock<CarrierPublicationProvider>(clientConfig, null, null, null, null, null)
            {
                CallBase = true
            };
            mockProvider
                .Setup(m => m.GetUpdatedConfig())
                .Callback(() =>
                {
                    if (onIndex < tcs.Length)
                    {
                        tcs[onIndex].SetResult(0);
                    }

                    onIndex++;
                });

            // Act

            bool result;
            var obj = mockProvider.Object;
            try
            {
                // Wait 150 milliseconds to see if the heartbeat fires three times
                result = Task.WaitAll(tcs.Select(p => (Task) p.Task).ToArray(), 150);
            }
            finally
            {
                mockProvider.Object.Dispose();
            }

            // Assert

            Assert.True(result);
        }

        [Test]
        public void ctor_DisableConfigHeartbeat_DoesntTriggersHeartbeat()
        {
            // Arrange

            var clientConfig = new ClientConfiguration()
            {
                EnableConfigHeartBeat = false,
                HeartbeatConfigInterval = 20 //ms
            };

            var tcs = new TaskCompletionSource<int>();

            var mockProvider = new Mock<CarrierPublicationProvider>(clientConfig, null, null, null, null, null)
            {
                CallBase = true
            };
            mockProvider
                .Setup(m => m.GetUpdatedConfig())
                .Callback(() => tcs.SetResult(0));

            // Act

            bool result;
            var obj = mockProvider.Object;
            try
            {
                // Wait 50 milliseconds to see if the heartbeat fires
                result = Task.WaitAll(new Task[] { tcs.Task }, 50);
            }
            finally
            {
                mockProvider.Object.Dispose();
            }

            // Assert

            Assert.False(result);
        }

        #endregion
    }
}
