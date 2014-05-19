using System;
using Couchbase.IO.Operations;

namespace Couchbase.IO.Utils
{
    /// <summary>
    /// Extension methods for reading values from a buffer and converting them to CLR types.
    /// </summary>
    public static class BufferExtensions
    {
        /// <summary>
        /// Converts a segment of a buffer, given an offset, to a <see cref="TypeCode"/> enumeration.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="offset">The offset to start from.</param>
        /// <returns>A <see cref="TypeCode"/> enumeration value.</returns>
        public static TypeCode ToTypeCode(this byte[] buffer, int offset)
        {
            return (TypeCode)BinaryConverter.DecodeInt32(buffer, offset);
        }

        /// <summary>
        /// Converts a <see cref="byte"/> to an <see cref="OperationCode"/> 
        /// </summary>
        /// <param name="value"></param> enumeration value.
        /// <returns>A <see cref="OperationCode"/> enumeration value.</returns>
        /// <remarks><see cref="OperationCode"/> are the available operations supported by Couchbase.</remarks>
        public static OperationCode ToOpCode(this byte value)
        {
            return (OperationCode)value;
        }

        /// <summary>
        /// Converts a segment of a buffer, given an offset, to a <see cref="ResponseStatus"/> enumeration.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="offset">The offset to start from.</param>
        /// <returns>A <see cref="ResponseStatus"/> enumeration value.</returns>
        public static ResponseStatus GetResponseStatus(this byte[] buffer, int offset)
        {
            return (ResponseStatus)BinaryConverter.DecodeUInt16(buffer, offset);
        }

        /// <summary>
        /// Converts a segment of a buffer, given an offset, to an <see cref="UInt64"/> value.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="offset">The offset to start from.</param>
        /// <returns>A <see cref="UInt64"/> value.</returns>
        public static ulong GetUInt64(this byte[] buffer, int offset)
        {
            return BinaryConverter.DecodeUInt64(buffer, offset);
        }

        /// <summary>
        /// Converts a segment of a buffer, given an offset, to an <see cref="Int64"/> value.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="offset">The offset to start from.</param>
        /// <returns>A <see cref="Int64"/> value.</returns>
        public static long GetInt64(this byte[] buffer, int offset)
        {
            return (long)GetUInt64(buffer, offset);
        }

        /// <summary>
        /// Converts a segment of a buffer, given an offset, to an <see cref="UInt32"/> value.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="offset">The offset to start from.</param>
        /// <returns>A <see cref="UInt32"/> value.</returns>
        public static uint GetUInt32(this byte[] buffer, int offset)
        {
            return BinaryConverter.DecodeUInt32(buffer, offset);
        }

        /// <summary>
        /// Converts a segment of a buffer, given an offset, to an <see cref="Int32"/> value.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="offset">The offset to start from.</param>
        /// <returns>A <see cref="Int32"/> value.</returns>
        public static int GetInt32(this byte[] buffer, int offset)
        {
            return BinaryConverter.DecodeInt32(buffer, offset);
        }

        /// <summary>
        /// Converts a segment of a buffer, given an offset, to an <see cref="UInt16"/> value.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="offset">The offset to start from.</param>
        /// <returns>A <see cref="UInt16"/> value.</returns>
        public static ushort GetUInt16(this byte[] buffer, int offset)
        {
            return BinaryConverter.DecodeUInt16(buffer, offset);
        }

        /// <summary>
        /// Converts a segment of a buffer, given an offset, to an <see cref="Int16"/> value.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="offset">The offset to start from.</param>
        /// <returns>A <see cref="Int16"/> value.</returns>
        public static short GetInt16(this byte[] buffer, int offset)
        {
            return (short)GetUInt16(buffer, offset);
        }

        /// <summary>
        /// Gets the length of a buffer.
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns>0 if the buffer is null, otherwise the length of the buffer.</returns>
        public static int GetLengthSafe(this byte[] buffer)
        {
            int length = 0;
            if (buffer != null)
            {
                length = buffer.Length;
            }
            return length;
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