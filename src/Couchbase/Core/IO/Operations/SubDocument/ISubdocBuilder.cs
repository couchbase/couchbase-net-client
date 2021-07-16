using System;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;

namespace Couchbase.Core.IO.Operations.SubDocument
{
    [Obsolete("This interface is not required and will be removed in a future release.")] // Delete
    public interface ISubDocBuilder<TDocument> : ITypeSerializerProvider
    {
        /// <summary>
        /// Executes the chained operations.
        /// </summary>
        /// <returns>
        /// A <see cref="T:Couchbase.IDocumentFragment`1" /> representing the results of the chained operations.
        /// </returns>
        IDocumentFragment<TDocument> Execute();

        /// <summary>
        /// Executes the chained operations.
        /// </summary>
        /// <returns>
        /// A <see cref="T:Couchbase.IDocumentFragment`1" /> representing the results of the chained operations.
        /// </returns>
        Task<IDocumentFragment<TDocument>> ExecuteAsync();

        /// <summary>
        /// Gets or sets the unique identifier for the document.
        /// </summary>
        /// <value>
        /// The key.
        /// </value>
        string Key { get; }

        /// <summary>
        /// Returns a count of the currently chained operations.
        /// </summary>
        /// <returns></returns>
        int Count { get; }

        /// <summary>
        /// The maximum time allowed for an operation to live before timing out.
        /// </summary>
        TimeSpan? Timeout { get; }
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
