using System.Linq;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Services;
using Couchbase.UnitTests.IO.Operations;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Services
{
    [TestFixture]
    public class MultiplexingIOServiceTests
    {
        [Test]
        public void When_EnhanchedDurability_Is_True_Hello_Requests_MutationSeqNo()
        {
            var mockConnection = new Mock<IConnection>();
            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);
            mockConnectionPool.SetupGet(x => x.Configuration).Returns(new PoolConfiguration {UseEnhancedDurability = true});

            var service = new MultiplexingIOService(mockConnectionPool.Object);

            service.Execute(new FakeOperationWithRequiredKey("key", null, new DefaultTranscoder(), 0));

            var features = new short[] {(byte) ServerFeatures.SubdocXAttributes, (byte) ServerFeatures.MutationSeqno};
            var expectedBytes = new Hello(features.ToArray(), new DefaultTranscoder(), 0, 0).Write();

            mockConnectionPool.Verify(x => x.Acquire(), Times.Once);
            mockConnection.Verify(x => x.Send(It.Is<byte[]>(bytes => bytes.SequenceEqual(expectedBytes))));
        }

        [Test]
        public void When_EnhanchedDurability_Is_False_Hello_Doesnt_Requests_MutationSeqNo()
        {
            var mockConnection = new Mock<IConnection>();
            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);
            mockConnectionPool.SetupGet(x => x.Configuration).Returns(new PoolConfiguration { UseEnhancedDurability = false });

            var service = new MultiplexingIOService(mockConnectionPool.Object);

            service.Execute(new FakeOperationWithRequiredKey("key", null, new DefaultTranscoder(), 0));

            var features = new short[] { (byte)ServerFeatures.SubdocXAttributes };
            var expectedBytes = new Hello(features.ToArray(), new DefaultTranscoder(), 0, 0).Write();

            mockConnectionPool.Verify(x => x.Acquire(), Times.Once);
            mockConnection.Verify(x => x.Send(It.Is<byte[]>(bytes => bytes.SequenceEqual(expectedBytes))));
        }

        [Test]
        public void When_NotConnected_Execute_ReturnsTransportFailureException()
        {
            var mockConnection = new Mock<IConnection>();
            mockConnection.Setup(x => x.IsConnected).Returns(false);

            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);
            mockConnectionPool.SetupGet(x => x.Configuration).Returns(new PoolConfiguration { UseEnhancedDurability = false });

            var service = new MultiplexingIOService(mockConnectionPool.Object);
            var result = service.Execute(new FakeOperationWithRequiredKey("key", null, new DefaultTranscoder(), 0));

            Assert.AreEqual(result.Status, ResponseStatus.TransportFailure);
            Assert.IsFalse(result.Success);
            Assert.IsInstanceOf<TransportFailureException>(result.Exception);
        }

        [Test]
        public async Task When_NotConnected_ExecuteAsync_ReturnsTransportFailureException()
        {
            var mockConnection = new Mock<IConnection>();
            mockConnection.Setup(x => x.IsConnected).Returns(false);

            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);
            mockConnectionPool.SetupGet(x => x.Configuration).Returns(new PoolConfiguration { UseEnhancedDurability = false });

            var service = new MultiplexingIOService(mockConnectionPool.Object);
            await service.ExecuteAsync(new FakeOperationWithRequiredKey("key", null, new DefaultTranscoder(), 0)
            {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
                Completed = async s =>
                {
                    Assert.AreEqual(s.Status, ResponseStatus.TransportFailure);
                    Assert.IsInstanceOf<TransportFailureException>(s.Exception);
                }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
            });
        }
    }
}
