using System;

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Flags for indicating additional actions when working with subdocument paths.
    /// </summary>
    [Flags]
    public enum SubdocPathFlags : byte
    {
        /// <summary>
        /// No path flags have been specified.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Creates path if it does not exist.
        /// </summary>
        CreatePath = 0x01,

        /// <summary>
        /// Path refers to a location within the document’s attributes section.
        /// </summary>
        Xattr = 0x04,

        /// <summary>
        /// Indicates that the server should expand any macros before storing the value. Infers <see cref="F:SubdocDocFlags.Xattr"/>.
        /// Only for internal diagnostic use only and is an unsupported feature.
        /// </summary>
        ExpandMacroValues = 0x010,

        /// <summary>
        /// Indicates this field is binary.   This is used in conjunction with ReplaceBodyWithXattr
        /// in the transactions to properly re-encode (on the server side) the base64 encoded
        /// xattr, so it shows up as a decoded base64 document body.
        /// </summary>
        BinaryValue = 0x020,
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
