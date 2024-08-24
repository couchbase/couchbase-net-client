using System;
using System.Buffers;

namespace Couchbase.Utils
{
    internal static class Utf8Helpers
    {
        /// <summary>
        /// UTF-8 Byte Order Mark as bytes.
        /// </summary>
        // Note: This property implementation appears like it allocates a byte[] on every get, but the modern C#
        // compiler optimizes this to a ReadOnlySpan<byte> that directly references the bytes in the resource
        // segment of the DLL.
        public static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

        /// <summary>
        /// If the data begins with a UTF-8 Byte Order Mark it is trimmed.
        /// </summary>
        /// <param name="data">Data to test and trim.</param>
        /// <returns>The trimmed data, or the original data if the BOM was not present.</returns>
        public static ReadOnlySpan<byte> TrimBomIfPresent(ReadOnlySpan<byte> data)
        {
            if (data.StartsWith(Utf8Bom))
            {
                return data.Slice(Utf8Bom.Length);
            }

            return data;
        }

        /// <summary>
        /// If the data begins with a UTF-8 Byte Order Mark it is trimmed.
        /// </summary>
        /// <param name="data">Data to test and trim.</param>
        /// <returns>The trimmed data, or the original data if the BOM was not present.</returns>
        public static ReadOnlyMemory<byte> TrimBomIfPresent(ReadOnlyMemory<byte> data)
        {
            if (data.Span.StartsWith(Utf8Bom))
            {
                return data.Slice(Utf8Bom.Length);
            }

            return data;
        }

        /// <summary>
        /// If the data begins with a UTF-8 Byte Order Mark it is trimmed.
        /// </summary>
        /// <param name="data">Data to test and trim.</param>
        /// <returns>The trimmed data, or the original data if the BOM was not present.</returns>
        public static ReadOnlySequence<byte> TrimBomIfPresent(ReadOnlySequence<byte> data)
        {
#if SPAN_SUPPORT
            var span = data.FirstSpan;
#else
            var span = data.First.Span;
#endif

            if (span.Length < Utf8Bom.Length)
            {
                // It's possible the first span is of insufficient length to store the BOM,
                // so a more complicated comparison is required.

                if (data.IsSingleSegment)
                {
                    // No need to do the more complicated comparison, there is no more data available.
                    return data;
                }

#if NET6_0_OR_GREATER
                var reader = new SequenceReader<byte>(data);
                if (reader.IsNext(Utf8Bom, advancePast: true))
                {
                    return reader.UnreadSequence;
                }
#else
                Span<byte> tempBuffer = stackalloc byte[Utf8Bom.Length];
                data.Slice(0, Utf8Bom.Length).CopyTo(tempBuffer);

                if (tempBuffer.StartsWith(Utf8Bom))
                {
                    return data.Slice(Utf8Bom.Length);
                }
#endif
            }
            else if (span.StartsWith(Utf8Bom))
            {
                return data.Slice(Utf8Bom.Length);
            }

            return data;
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
