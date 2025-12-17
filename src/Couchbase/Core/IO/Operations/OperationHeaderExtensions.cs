using System;
using Couchbase.Core.IO.Converters;

namespace Couchbase.Core.IO.Operations
{
    internal static class OperationHeaderExtensions
    {
        internal static long? GetServerDuration(this in OperationHeader header, ReadOnlySpan<byte> buffer)
        {
            var framingExtrasLength = header.FramingExtrasLength;
            if (framingExtrasLength <= 0)
            {
                return null;
            }

            return GetServerDuration(buffer.Slice(OperationHeader.Length, framingExtrasLength));
        }

        internal static long? GetServerDuration(ReadOnlySpan<byte> buffer)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var control = buffer[offset++];
                var type = (ResponseFramingExtraType) (control & 0xF0); // first 4 bits
                var length = control & 0x0F; // last 4 bits

                if (type == ResponseFramingExtraType.ServerDuration)
                {
                    // read encoded two byte server duration
                    var encoded = ByteConverter.ToUInt16(buffer.Slice(offset));
                    if (encoded > 0)
                    {
                        // decode into microseconds
                        return (long) Math.Pow(encoded, 1.74) / 2;
                    }
                }

                offset += length;
            }

            return null;
        }
    }
}

#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2018 Couchbase, Inc.
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
