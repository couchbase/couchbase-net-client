using System;

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
        public static ReadOnlySpan<byte> Utf8Bom => new byte[] { 0xEF, 0xBB, 0xBF };

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
