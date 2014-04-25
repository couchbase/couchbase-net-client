using Couchbase.IO.Strategies.Awaitable;
using NUnit.Framework;
using System.Net.Sockets;

namespace Couchbase.Tests.IO.Strategies.Awaitable
{
    [TestFixture]
    public class BufferAllocatorTests
    {
        private BufferAllocator _bufferAllocator;

        [SetUp]
        public void SetUp()
        {
            //Create space for up to 1000 objects
            _bufferAllocator = new BufferAllocator(1000 * 512, 512);
        }

        [Test]
        public void Test()
        {
            var args = new SocketAsyncEventArgs();
            _bufferAllocator.SetBuffer(args);
            Assert.AreEqual(0, args.Offset);

            var args2 = new SocketAsyncEventArgs();
            _bufferAllocator.SetBuffer(args2);
            Assert.AreEqual(512, args2.Offset);

            _bufferAllocator.ReleaseBuffer(args);
            _bufferAllocator.ReleaseBuffer(args2);
        }
    }
}