
namespace Couchbase
{
    /// <summary>
    /// Provides a method signature for invalidating and clearing a cache.
    /// </summary>
    public interface IQueryCacheInvalidator
    {
        /// <summary>
        /// Invalidates and clears the query cache. This method can be used to explicitly clear the internal N1QL query cache. This cache will
        /// be filled with non-adhoc query statements (query plans) to speed up those subsequent executions. Triggering this method will wipe
        /// out the complete cache, which will not cause an interruption but rather all queries need to be re-prepared internally. This method
        /// is likely to be deprecated in the future once the server side query engine distributes its state throughout the cluster.
        /// </summary>
        /// <returns>An <see cref="int"/> representing the size of the cache before it was cleared.</returns>
        int InvalidateQueryCache();
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
