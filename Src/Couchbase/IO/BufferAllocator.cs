using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase.IO
{
    /// <summary>
    /// A buffer allocator for <see cref="SocketAsyncEventArgs"/> instances.
    /// </summary>
    /// <remarks>Used to reduce memory fragmentation do to pinning.</remarks>
    /// <remarks>Near identical to implementation found in MSDN documentation: http://msdn.microsoft.com/en-us/library/bb517542%28v=vs.110%29.aspx</remarks>
    internal sealed class BufferAllocator
    {
        private static readonly ILogger Log = new LoggerFactory().CreateLogger<BufferAllocator>();
        private readonly int _numberOfBytes;
        private readonly byte[] _buffer;
        private readonly Stack<int> _freeIndexPool;
        private readonly int _bufferSize;
        private int _currentIndex;

        public BufferAllocator(int totalBytes, int bufferSize)
        {
            _numberOfBytes = totalBytes;
            _currentIndex = 0;
            _bufferSize = bufferSize;
            _freeIndexPool = new Stack<int>();
            _buffer = new byte[_numberOfBytes];
        }

        /// <summary>
        /// Sets the buffer for a <see cref="SocketAsyncEventArgs"/> object.
        /// </summary>
        /// <param name="eventArgs">The SAEA whose buffer will be set</param>
        /// <returns>True if the bucket was set.</returns>
        public bool SetBuffer(SocketAsyncEventArgs eventArgs)
        {
            lock (_freeIndexPool)
            {
                var isBufferSet = true;
                if (_freeIndexPool.Count > 0)
                {
                    eventArgs.SetBuffer(_buffer, _freeIndexPool.Pop(), _bufferSize);
                }
                else
                {
                    if ((_numberOfBytes - _bufferSize) < _currentIndex)
                    {
                        isBufferSet = false;
                    }
                    else
                    {
                        eventArgs.SetBuffer(_buffer, _currentIndex, _bufferSize);
                        _currentIndex += _bufferSize;
                    }
                }
                return isBufferSet;
            }
        }

        /// <summary>
        /// Releases the buffer allocate to a <see cref="SocketAsyncEventArgs"/> instance.
        /// </summary>
        /// <param name="eventArgs">The SAEA instance the buffer will be released from.</param>
        public void ReleaseBuffer(SocketAsyncEventArgs eventArgs)
        {
            lock (_freeIndexPool)
            {
                try
                {
                    _freeIndexPool.Push(eventArgs.Offset);
                    eventArgs.SetBuffer(null, 0, 0);
                }
                catch (Exception e)
                {
                    Log.Info(e);
                }
            }
        }
    }
}

#region [ License information ]

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