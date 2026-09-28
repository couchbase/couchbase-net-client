using Couchbase.Core.Compatibility;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Optional parameters for <see cref="GetReplicaStrategy.FromIndex(ReplicaIndex, GetReplicaStrategyFromIndexOptions?)"/>.
    /// </summary>
    [InterfaceStability(Level.Uncommitted)]
    public class GetReplicaStrategyFromIndexOptions
    {
        internal static GetReplicaStrategyFromIndexOptions Default { get; } = new();

        internal bool WrapValue { get; private set; }

        /// <summary>
        /// Reads the next available replica when the requested one does not exist or is not available.
        /// Without wrap the operation fails instead.
        /// </summary>
        /// <param name="wrap">True to move to the next available replica.</param>
        /// <returns>A <see cref="GetReplicaStrategyFromIndexOptions"/> instance for chaining.</returns>
        public GetReplicaStrategyFromIndexOptions Wrap(bool wrap = true)
        {
            WrapValue = wrap;
            return this;
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
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
