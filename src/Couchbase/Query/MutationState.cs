using System;
using System.Collections;
using System.Collections.Generic;
using Couchbase.Core;
using Couchbase.KeyValue;
using Newtonsoft.Json.Linq;

namespace Couchbase.Query
{
   /// <summary>
    /// Represents a composition of <see cref="MutationToken"/>'s into a single
    /// unit for performing "read your own writes" or RYOW semantics on a N1QL query.
    /// </summary>
    public sealed class MutationState : IEnumerable<MutationToken>
    {
        private readonly List<MutationToken> _tokens = new List<MutationToken>();

        /// <summary>
        /// Creates a <see cref="MutationToken"/> from a list of <see cref="IMutationResult"/>'s assuming enhanced durability is enabled.
        /// </summary>
        /// <param name="mutationResults">The mutationResults.</param>
        /// <returns></returns>
        public static MutationState From(params IMutationResult[] mutationResults)
        {
            return new MutationState().Add(mutationResults);
        }

        /// <summary>
        /// Creates a<see cref= "MutationState" /> from another <see cref="MutationState"/> assuming enhanced durability is enabled.
        /// </summary>
        /// <param name="mutationState">State of the mutation.</param>
        /// <returns>The <see cref="MutationState"/> object itself.</returns>
        public static MutationState From(MutationState mutationState)
        {
            return new MutationState().Add(mutationState);
        }

        /// <summary>
        /// Adds a <see cref="MutationToken"/> to the <see cref="MutationState"/> from a list of <see cref="IMutationResult"/> assuming enhanced durability is enabled.
        /// </summary>
        /// <param name="mutationResults">The mutationResults.</param>
        /// <exception cref="ArgumentException">If a <see cref="IMutationResult"/> does not contain a valid <see cref="MutationToken"/>.</exception>
        /// <returns>The <see cref="MutationState"/> object itself.</returns>
        public MutationState Add(params IMutationResult[] mutationResults)
        {
            foreach (var document in mutationResults)
            {
                if (document.MutationToken.IsSet)
                {
                    _tokens.Add(document.MutationToken);
                }
                else
                {
                    throw new ArgumentException("ReadResult does not contain valid MutationToken.");
                }
            }
            return this;
        }

        /// <summary>
        /// Adds the <see cref="MutationToken"/>'s from another <see cref="MutationState"/>.
        /// </summary>
        /// <param name="mutationState">State of the mutation.</param>
        /// <returns>The <see cref="MutationState"/> object itself.</returns>
        public MutationState Add(MutationState mutationState)
        {
            _tokens.AddRange(mutationState._tokens);
            return this;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection of <see cref="MutationToken"/>'s.
        /// </summary>
        /// <returns>
        /// An enumerator that can be used to iterate through the collection.
        /// </returns>
        public IEnumerator<MutationToken> GetEnumerator()
        {
            return ((IEnumerable<MutationToken>) _tokens).GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection of <see cref="MutationToken"/>'s.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Exports this <see cref="MutationToken"/> in the FTS/Search specific format.
        /// </summary>
        /// <param name="indexName">The index name as the key.</param>
        /// <returns>A <see cref="JProperty"/> in the correct FTS/Search format.</returns>
        internal Dictionary<string, Dictionary<string, long>> ExportForSearch(string indexName)
        {
            indexName = indexName ?? throw new ArgumentNullException(nameof(indexName));

            var vectors = new Dictionary<string, long>(_tokens.Count);
            foreach (var token in _tokens)
            {
                var vectorKey = $"{token.VBucketId}/{token.VBucketUuid}";
                vectors[vectorKey] = token.SequenceNumber;
            }
            return new Dictionary<string, Dictionary<string, long>>() { { indexName, vectors } };
        }
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
