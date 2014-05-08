using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.IO.Strategies.Awaitable
{
    /// <summary>
    /// A buffer allocator for <see cref="SocketAsyncEventArgs"/> instances.
    /// </summary>
    /// <remarks>Used to reduce memory fragmentation do to pinning.</remarks>
    internal sealed class BufferAllocator
    {
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
        /// <param name="eventArgs"></param>
        /// <returns></returns>
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
        /// <param name="eventArgs"></param>
        public void ReleaseBuffer(SocketAsyncEventArgs eventArgs)
        {
            lock (_freeIndexPool)
            {
                _freeIndexPool.Push(eventArgs.Offset);
                eventArgs.SetBuffer(null, 0, 0);
            }
        }
    }
}
