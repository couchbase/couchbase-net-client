using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.IO
{
    /// <summary>
    /// Represents a temporary buffer provided by a <see cref="BufferAllocator"/>.  It should be released after use
    /// using <see cref="BufferAllocator.ReleaseBuffer(IOBuffer)"/>.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class IOBuffer
    {
        /// <summary>
        /// Byte array that the IOBuffer exists within
        /// </summary>
        public byte[] Buffer { get; private set; }

        /// <summary>
        /// Offset of the buffer within the byte array
        /// </summary>
        public int Offset { get; private set; }

        /// <summary>
        /// Length of the buffer within the byte array
        /// </summary>
        public int Length { get; private set; }

        public IOBuffer(byte[] buffer, int offset, int length)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }
            if ((offset < 0) || (offset >= buffer.Length))
            {
                throw new ArgumentOutOfRangeException("offset");
            }
            if (offset + length > buffer.Length)
            {
                throw new ArgumentOutOfRangeException("length");
            }

            Buffer = buffer;
            Offset = offset;
            Length = length;
        }
    }
}
