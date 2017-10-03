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
