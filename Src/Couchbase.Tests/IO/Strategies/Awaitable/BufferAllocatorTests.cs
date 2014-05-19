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

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion