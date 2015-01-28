using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Utils
{
    /// <summary>
    /// Extension methods for copying values to <see cref="ArraySegment{T}"/> instances.
    /// </summary>
    public static class ArraySegmentExtensions
    {
        /// <summary>
        /// Takes a value, converts it from little-endian to big-endian and the copies it to the <see cref="ArraySegment{T}"/> at a given offset and length.
        /// </summary>
        /// <param name="arraySegment">The <see cref="ArraySegment{T}"/> to copy the value to.</param>
        /// <param name="value">An <see cref="UInt32"/> to copy to the <see cref="ArraySegment{T}"/>.</param>
        /// <param name="offset">The offset to write the value to within the <see cref="ArraySegment{T}"/></param>
        /// <param name="count">The length of the write - e.g. 4 for a int, 8 for a long.</param>
        public static void ConvertAndCopy(this ArraySegment<byte> arraySegment, uint value, int offset, int count)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);

            Buffer.BlockCopy(bytes, 0, arraySegment.Array, offset, count);
        }

        /// <summary>
        /// Takes a value, converts it from little-endian to big-endian and the copies it to the <see cref="ArraySegment{T}"/> at a given offset and length.
        /// </summary>
        /// <param name="arraySegment">The <see cref="ArraySegment{T}"/> to copy the value to.</param>
        /// <param name="value">An <see cref="UInt64"/> to copy to the <see cref="ArraySegment{T}"/>.</param>
        /// <param name="offset">The offset to write the value to within the <see cref="ArraySegment{T}"/></param>
        /// <param name="count">The length of the write - e.g. 4 for a int, 8 for a long.</param>
        public static void ConvertAndCopy(this ArraySegment<byte> arraySegment, ulong value, int offset, int count)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);

            Buffer.BlockCopy(bytes, 0, arraySegment.Array, offset, count);
        }
    }
}
