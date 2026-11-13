using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Interface for any non-public methods or properties that are needed on a <see cref="ICouchbaseCollection"/>.
    /// </summary>
    internal interface IInternalCollection
    {
        /// <summary>
        /// Gets or sets the identifier for a <see cref="ICouchbaseCollection"/>.
        /// </summary>
        uint? Cid { get; set; }

        /// <summary>
        /// Fetches the collection ID from the server, unless it is cached and <paramref name="forceUpdate"/> is false.
        /// </summary>
        /// <param name="retryIfFailure">true to send the lookup through the retry orchestrator.</param>
        /// <param name="forceUpdate">true to fetch the ID even if it is cached.</param>
        /// <param name="parentSpan">The span of the operation that needs the ID. The get_collection_id span goes under it.</param>
        ValueTask PopulateCidAsync(bool retryIfFailure = true, bool forceUpdate = false, IRequestSpan? parentSpan = null);
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
