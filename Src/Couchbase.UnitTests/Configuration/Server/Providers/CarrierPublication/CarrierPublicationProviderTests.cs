using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers.CarrierPublication;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.Authentication;
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

        [TestCase(true)]
        [TestCase(false)]
        public void Only_Execute_SelectBuket_When_EnhancedAuthentication_Is_Enabled(bool enabled)
        {
            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.EndPoint)
                .Returns(new IPEndPoint(IPAddress.Any, 0));
            var mockSaslMech = new Mock<ISaslMechanism>();
            var mockIOService = new Mock<IIOService>();
            mockIOService.Setup(x => x.ConnectionPool)
                .Returns(mockConnectionPool.Object);
            mockIOService.Setup(x => x.SupportsEnhancedAuthentication)
                .Returns(enabled);
            mockIOService.Setup(x => x.Execute(It.IsAny<IOperation>()))
                .Returns(new OperationResult {Success = true});
            mockIOService.Setup(x => x.Execute(It.IsAny<IOperation<BucketConfig>>()))
                .Returns(new OperationResult<BucketConfig>
                {
                    Success = true,
                    Value = new BucketConfig {Name = "default", Nodes = new [] { new Node {Hostname = "localhost"} }}
                });

            var config = new ClientConfiguration();
            var provider = new CarrierPublicationProvider(
                config,
                pool => mockIOService.Object,
                (c, e) => mockConnectionPool.Object,
                (ac, b, c, d) => mockSaslMech.Object,
                new DefaultConverter(),
                new DefaultTranscoder()
            );

            provider.GetConfig("bucket", "username", "password");
            mockIOService.Verify(x => x.Execute(It.IsAny<SelectBucket>()), Times.Exactly(enabled ? 1 : 0));
        }
    }
}
