using System;

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Flags for indicating additional actions when working with subdocument documents.
    /// </summary>
    [Flags]
    public enum SubdocDocFlags : byte
    {
        /// <summary>
        /// No document flags have been specified.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Creates the document if it does not exist.
        /// </summary>
        UpsertDocument = 0x01,

        /// <summary>
        /// Similar to <see cref="UpsertDocument"/>, except that the operation only succeds if the document does not exist.
        /// This option makes sense in the context of wishing to create a new document together with Xattrs.
        /// </summary>
        InsertDocument = 0x02,

        /// <summary>
        /// Allows access to a deleted document's attributes section.
        /// Only for internal diagnostic use only and is an unsupported feature.
        /// </summary>
        AccessDeleted = 0x04,

        /// <summary>
        /// If theserver needs to create a new document, it will create it as a tombstone.
        /// Any system or user xattrs provided in the request will be stored, but a document body will not be.
        /// </summary>
        CreateAsDeleted = 0x08
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
