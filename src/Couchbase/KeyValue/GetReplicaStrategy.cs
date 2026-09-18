using System;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Chooses the one replica that <see cref="ICouchbaseCollection.GetReplicaAsync"/> reads from.
    /// </summary>
    [InterfaceStability(Level.Uncommitted)]
    public abstract class GetReplicaStrategy
    {
        private protected GetReplicaStrategy() { }

        /// <summary>
        /// Reads from the replica at the given position.
        /// </summary>
        /// <param name="index">Which replica to read from.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>A <see cref="GetReplicaStrategy"/> to pass to <see cref="ICouchbaseCollection.GetReplicaAsync"/>.</returns>
        public static GetReplicaStrategy FromIndex(ReplicaIndex index, GetReplicaStrategyFromIndexOptions? options = null)
        {
            if (index is < ReplicaIndex.First or > ReplicaIndex.Third)
            {
                ThrowHelper.ThrowInvalidArgumentException($"{index} is not a valid {nameof(ReplicaIndex)}.");
            }

            return new GetReplicaFromIndexStrategy((int) index, (options ?? GetReplicaStrategyFromIndexOptions.Default).WrapValue);
        }

        /// <summary>
        /// Returns the server index to read from. The replica chain is the vBucket map row without the active.
        /// </summary>
        internal abstract short SelectReplica(ReadOnlySpan<short> replicaChain, int numReplicas);
    }

    internal sealed class GetReplicaFromIndexStrategy : GetReplicaStrategy
    {
        private readonly int _index;
        private readonly bool _wrap;

        public GetReplicaFromIndexStrategy(int index, bool wrap)
        {
            _index = index;
            _wrap = wrap;
        }

        internal override short SelectReplica(ReadOnlySpan<short> replicaChain, int numReplicas)
        {
            if (numReplicas == 0)
            {
                throw new ReplicaIndexOutOfBoundsException(OutOfBoundsMessage(numReplicas));
            }

            if (_wrap)
            {
                var start = _index % numReplicas;
                var position = start;
                while (position >= replicaChain.Length || replicaChain[position] == -1)
                {
                    position = (position + 1) % numReplicas;
                    if (position == start)
                    {
                        throw new ReplicaIndexCurrentlyUnavailableException(UnavailableMessage(replicaChain.Length, numReplicas));
                    }
                }

                return replicaChain[position];
            }

            if (_index >= numReplicas)
            {
                throw new ReplicaIndexOutOfBoundsException(OutOfBoundsMessage(numReplicas));
            }

            if (_index >= replicaChain.Length || replicaChain[_index] == -1)
            {
                throw new ReplicaIndexCurrentlyUnavailableException(UnavailableMessage(replicaChain.Length, numReplicas));
            }

            return replicaChain[_index];
        }

        private string OutOfBoundsMessage(int numReplicas) =>
            $"Replica {(ReplicaIndex) _index} was requested but the bucket has {numReplicas} replica(s).";

        private string UnavailableMessage(int chainLength, int numReplicas) =>
            $"Replica {(ReplicaIndex) _index} is not available right now. The bucket has {numReplicas} replica(s) " +
            $"and the vBucket map lists {chainLength}.";

        public override string ToString() => $"FromIndex({(ReplicaIndex) _index}, wrap: {(_wrap ? "true" : "false")})";
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
