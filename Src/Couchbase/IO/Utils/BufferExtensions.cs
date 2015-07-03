using System;
using System.Linq;
using Couchbase.IO.Operations;

namespace Couchbase.IO.Utils
{
    /// <summary>
    /// Extension methods for reading values from a buffer and converting them to CLR types.
    /// </summary>
    public static class BufferExtensions
    {
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
        /// Gets the length of a buffer.
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns>0 if the buffer is null, otherwise the length of the buffer.</returns>
        public static int GetLengthSafe(this byte[] buffer)
        {
            var length = 0;
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