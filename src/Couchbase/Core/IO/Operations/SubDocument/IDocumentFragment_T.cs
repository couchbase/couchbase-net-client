using System;

namespace Couchbase.Core.IO.Operations.SubDocument
{
    /// <summary>
    /// Represents one more fragments of a document that is returned by the sub-document API.
    /// </summary>
    /// <typeparam name="TDocument">The document</typeparam>
    [Obsolete("This interface is not required and will be removed in a future release.")] // Delete
    public interface IDocumentFragment<out TDocument> : IOperationResult<TDocument>, IDocumentFragment
    {
        /// <summary>
        /// The time-to-live or TTL for the document before it's evicted from disk in milliseconds.
        /// </summary>
        /// <remarks>Setting this to zero or less will give the document infinite lifetime</remarks>
        uint Expiry { get; set; }
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
