using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core.IO.SubDocument;
using Couchbase.IO;

namespace Couchbase
{
    /// <summary>
    /// Represents one more fragments of an <see cref="IDocument{T}"/> that is returned by the sub-document API.
    /// </summary>
    /// <typeparam name="TDocument">The document</typeparam>
    public class DocumentFragment<TDocument> : OperationResult, IDocumentFragment<TDocument>
    {
        public DocumentFragment()
        {
            Value= new List<SubDocOperationResult>();
        }

        /// <summary>
        /// The unique identifier or key for the document.
        /// </summary>
        /// <remarks>Must not be null or empty.</remarks>
        public string Id { get; set; }

        /// <summary>
        /// The time-to-live or TTL for the document before it's evicted from disk in milliseconds.
        /// </summary>
        /// <remarks>Setting this to zero or less will give the document infinite lifetime</remarks>
        public uint Expiry { get; set; }

        /// <summary>
        /// The value if it exists for a specific path.
        /// </summary>
        /// <typeparam name="TContent">The <see cref="Type"/> to cast the value to.</typeparam>
        /// <param name="path">The path of the operation to retrieve the value from.</param>
        /// <returns>An object of type <see cref="Type"/> representing the value of the operation.</returns>
        /// <remarks>If no value exists, the default value for the <see cref="Type"/> will be returned.</remarks>
        public TContent Content<TContent>(string path)
        {
            return (TContent) Value.Single(x => x.Path.Equals(path)).Value;
        }

        /// <summary>
        /// The value if it exists for a specific index.
        /// </summary>
        /// <typeparam name="TContent">The <see cref="Type"/> to cast the value to.</typeparam>
        /// <param name="index">The ordinal of the operation to retrieve the value from.</param>
        /// <returns>An object of type <see cref="Type"/> representing the value of the operation.</returns>
        /// <remarks>If no value exists, the default value for the <see cref="Type"/> will be returned.</remarks>
        public TContent Content<TContent>(int index)
        {
            return (TContent) Value[index].Value;
        }

        /// <summary>
        /// The value if it exists for a specific path.
        /// </summary>
        /// <param name="path">The path of the operation to retrieve the value from.</param>
        /// <returns>An <see cref="object"/> representing the result of a operation.</returns>
        /// <remarks>If no value exists, the default value (null) for the <see cref="object"/> will be returned.</remarks>
        public object Content(string path)
        {
            return Content<object>(path);
        }

        /// <summary>
        /// The value if it exists for a specific index.
        /// </summary>
        /// <param name="index">The ordinal of the operation to retrieve the value from.</param>
        /// <returns>An <see cref="object"/> representing the result of a operation.</returns>
        /// <remarks>If no value exists, the default value for the <see cref="Type"/> will be returned.</remarks>
        /// <remarks>If no value exists, the default value (null) for the <see cref="object"/> will be returned.</remarks>
        public object Content(int index)
        {
            return Content<object>(index);
        }

        /// <summary>
        /// Checks whether the given path is part of this result set, eg. an operation targeted it, and the operation executed successfully.
        /// </summary>
        /// <param name="path">The path for the sub-document operation.</param>
        /// <returns><s>true</s> if that path is part of the successful result set, <s>false</s> in any other case.</returns>
        public bool Exists(string path)
        {
            return Value.Any(x => x.Path.Equals(path));
        }

        /// <summary>
        /// The count of the sub-document operations chained togather.
        /// </summary>
        /// <returns>An <see cref="int"/> that is the count of the total operations chained togather.</returns>
        public int Count()
        {
            return Value.Count;
        }

        /// <summary>
        /// Gets the <see cref="ResponseStatus"/> for a specific operation at it's path.
        /// </summary>
        /// <param name="path">The path of the operation.</param>
        /// <returns>The <see cref="ResponseStatus"/> that the server returned.</returns>
        public ResponseStatus OpStatus(string path)
        {
            return Value.Single(x => x.Path.Equals(path)).Status;
        }

        /// <summary>
        /// Gets the <see cref="ResponseStatus"/> for a specific operation at it's index.
        /// </summary>
        /// <param name="index">The ordinal of the operation.</param>
        /// <returns>The <see cref="ResponseStatus"/> that the server returned.</returns>
        public ResponseStatus OpStatus(int index)
        {
            return Value[index].Status;
        }

        [Obsolete("For backwards compatibility with regular K/V and Document API internals. Do not use.")]
        // ReSharper disable once UnassignedGetOnlyAutoProperty
        TDocument IOperationResult<TDocument>.Value { get { return default(TDocument); } }

        /// <summary>
        /// An adapter between <see cref="IOperationResult{T}"/> and the sub document API.
        /// </summary>
        /// <remarks>For internal use only.</remarks>
        internal IList<SubDocOperationResult> Value { get; set; }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
