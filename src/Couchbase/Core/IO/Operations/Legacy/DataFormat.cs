namespace Couchbase.Core.IO.Operations.Legacy
{
    /// <summary>
    /// Specifies the formatting of data across all SDKs
    /// </summary>
    public enum DataFormat : byte
    {
        /// <summary>
        /// Reserved bit position to avoid zeroing out upper 8 bits
        /// </summary>
        Reserved = 0,

        /// <summary>
        /// Used for SDK specific encodings
        /// </summary>
        Private  = 1,

        /// <summary>
        /// Encode as Json
        /// </summary>
        Json = 2,

        /// <summary>
        /// Store as raw binary format
        /// </summary>
        Binary = 3,

        /// <summary>
        /// Store as a UTF8 string
        /// </summary>
        String = 4
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
