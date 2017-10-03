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

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
