using System;
using System.Collections.Generic;

#nullable enable

namespace Couchbase.Query
{
    /// <summary>
    /// Interface for the results of a N1QL query.
    /// </summary>
    /// <typeparam name="T">Type of row returned by the N1QL query.</typeparam>
    public interface IQueryResult<out T> : IDisposable, IAsyncEnumerable<T>, IServiceResult
    {
        /// <summary>
        /// The results of the query as a <see cref="IAsyncEnumerable{T}"/>.
        /// </summary>
        /// <remarks>
        /// In most cases, the rows may be enumerated only once. If it's necessary to enumerate more than
        /// once, use <see cref="System.Linq.AsyncEnumerable.ToListAsync(IAsyncEnumerable{T}, System.Threading.CancellationToken)"/> to convert to a list.
        /// ToListAsync can also be used to enumerate with a synchronous foreach loop in C# 7.
        /// </remarks>
        IAsyncEnumerable<T> Rows { get; }

        /// <summary>
        /// Gets the meta data associated with the query result. May not be fully populated
        /// until after the rows are enumerated.
        /// </summary>
        QueryMetaData? MetaData { get; }

        /// <summary>
        /// Gets a list of 0 or more error objects; if an error occurred during processing of the request, it will be represented by an error object in this list.
        /// </summary>
        List<Error> Errors { get; }
    }
}
#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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
