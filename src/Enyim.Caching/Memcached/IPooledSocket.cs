using System;
using System.Collections.Generic;

namespace Enyim.Caching.Memcached
{
    public interface IPooledSocket : IDisposable
    {
        [Obsolete]
        Action<IPooledSocket> CleanupCallback { get; set; }

        int Available { get; }

        bool IsAlive { get; }

        void Reset();

        Guid InstanceId { get; }

        void Close();

        bool IsConnected { get; }

        bool IsInUse { get; set; }

        /// <summary>
        /// replaces call to Dispose to release instance back into pool
        /// </summary>
        void Release();

        /// <summary>
        /// Reads the next byte from the server's response.
        /// </summary>
        /// <remarks>This method blocks and will not return until the value is read.</remarks>
        int ReadByte();

        /// <summary>
        /// Reads data from the server into the specified buffer.
        /// </summary>
        /// <param name="buffer">An array of <see cref="T:System.Byte"/> that is the storage location for the received data.</param>
        /// <param name="offset">The location in buffer to store the received data.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <remarks>This method blocks and will not return until the specified amount of bytes are read.</remarks>
        void Read(byte[] buffer, int offset, int count);

        void Write(byte[] data, int offset, int length);

        void Write(IList<ArraySegment<byte>> buffers);

        /// <summary>
        /// Receives data asynchronously. Returns true if the IO is pending. Returns false if the socket already failed or the data was available in the buffer.
        /// p.Next will only be called if the call completes asynchronously.
        /// </summary>
        [Obsolete]
        bool ReceiveAsync(AsyncIOArgs p);
    }
}