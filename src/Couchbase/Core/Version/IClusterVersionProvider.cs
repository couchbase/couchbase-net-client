using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Core.Version
{
    /// <summary>
    /// Provides version information about the cluster.
    /// </summary>
    /// <remarks>
    /// The implementation of this interface is typically obtained from <see cref="ICluster.ClusterServices"/>.
    /// </remarks>
    public interface IClusterVersionProvider
    {
        /// <summary>
        /// Gets the <see cref="ClusterVersion"/> from the currently connected cluster, if available.
        /// </summary>
        /// <returns>The <see cref="ClusterVersion"/>, or null if unavailable.</returns>
        ValueTask<ClusterVersion?> GetVersionAsync();

        /// <summary>
        /// Clear any cached value, getting a fresh value from the cluster on the next request.
        /// </summary>
        void ClearCache();
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
