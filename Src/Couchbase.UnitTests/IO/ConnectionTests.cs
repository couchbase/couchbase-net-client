using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO
{
    [TestFixture]
    public class ConnectionTests
    {
        [Test]
        public void ctor_NoBuffers_ThrowsException()
        {
            // Arrange

            var connectionPool = new Mock<IConnectionPool>();
            connectionPool.SetupGet(m => m.Configuration).Returns(new PoolConfiguration());

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            var converter = new Mock<IByteConverter>();

            var allocator = new Mock<BufferAllocator>(1, 1);
            allocator.Setup(m => m.SetBuffer(It.IsAny<SocketAsyncEventArgs>())).Returns(false);

            // Act/Assert

            // ReSharper disable once ObjectCreationAsStatement
            var ex = Assert.Throws<BufferUnavailableException>(() => new Connection(connectionPool.Object, socket, converter.Object, allocator.Object));

            Assert.True(ex.Message.Contains("BufferAllocator"));
        }

        [Test]
        public void ctor_HasBuffers_Succeeds()
        {
            // Arrange

            var connectionPool = new Mock<IConnectionPool>();
            connectionPool.SetupGet(m => m.Configuration).Returns(new PoolConfiguration());

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            var converter = new Mock<IByteConverter>();

            var allocator = new Mock<BufferAllocator>(1, 1);
            allocator.Setup(m => m.SetBuffer(It.IsAny<SocketAsyncEventArgs>())).Returns(true);

            // Act

            // ReSharper disable once ObjectCreationAsStatement
            new Connection(connectionPool.Object, socket, converter.Object, allocator.Object);
        }
    }
}
