using Couchbase.Core.IO.Operations;
using System;

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Flags for indicating additional actions when working with MutateIn Sub-Document operations.
    /// </summary>
    [Flags]
    public enum StoreSemantics : byte
    {
        /// <summary>
        /// Replace the document; fail if the document does not exist.
        /// </summary>
        Replace = 0x00,

        /// <summary>
        /// Creates the document; update the document if it exists.
        /// </summary>
        Upsert = OpCode.Set,

        /// <summary>
        /// Create the document; fail if it exists.
        /// </summary>
        Insert = OpCode.Add,

        /// <summary>
        /// Allows access to a deleted document's attributes section.
        /// Only for internal diagnostic use only and is an unsupported feature.
        /// </summary>
        AccessDeleted = OpCode.Delete,

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
