using System;
using System.Collections;
using System.Collections.Generic;
using Couchbase.Core.Buckets;

namespace Couchbase.N1QL
{
    /// <summary>
    /// Represents a composition of <see cref="MutationToken"/>'s into a single
    /// unit for performing "read your own writes" or RYOW semantics on a N1QL query.
    /// </summary>
    public sealed class MutationState : IEnumerable<MutationToken>
    {
        private readonly List<MutationToken> _tokens = new List<MutationToken>();

        /// <summary>
        /// Creates a <see cref="MutationToken"/> from a list of <see cref="IDocument"/>'s assuming enhanced durability is enabled.
        /// </summary>
        /// <param name="documents">The documents.</param>
        /// <returns></returns>
        public static MutationState From(params IDocument[] documents)
        {
            return new MutationState().Add(documents);
        }

        /// <summary>
        /// Creates a <see cref="MutationState"/> from a list of <see cref="IDocumentFragment"/>'s assuming enhanced durability is enabled.
        /// </summary>
        /// <param name="fragments">The fragments.</param>
        /// <returns>The <see cref="MutationState"/> object itself.</returns>
        public static MutationState From(params IDocumentFragment[] fragments)
        {
            return new MutationState().Add(fragments);
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
        /// Adds a <see cref="MutationToken"/> to the <see cref="MutationState"/> from a list of <see cref="IDocument"/> assuming enhanced durability is enabled.
        /// </summary>
        /// <param name="documents">The documents.</param>
        /// <exception cref="ArgumentException">If a <see cref="IDocument"/> does not contain a valid <see cref="MutationToken"/>.</exception>
        /// <returns>The <see cref="MutationState"/> object itself.</returns>
        public MutationState Add(params IDocument[] documents)
        {
            foreach (var document in documents)
            {
                if (document.Token.IsSet)
                {
                    _tokens.Add(document.Token);
                }
                else
                {
                    throw new ArgumentException("Document does not contain valid MutationToken.");
                }
            }
            return this;
        }

        /// <summary>
        /// Adds a <see cref="MutationToken"/> to the <see cref="MutationState"/> from a list of <see cref="IDocumentFragment"/> assuming enhanced durability is enabled.
        /// </summary>
        /// <param name="fragments">The fragments.</param>
        /// <exception cref="ArgumentException">If a <see cref="IDocument"/> does not contain a valid <see cref="MutationToken"/>.</exception>
        /// <returns>The <see cref="MutationState"/> object itself.</returns>
        public MutationState Add(params IDocumentFragment[] fragments)
        {
            foreach (var documentFragment in fragments)
            {
                if (documentFragment.Token.IsSet)
                {
                    _tokens.Add(documentFragment.Token);
                }
                else
                {
                    throw new ArgumentException("DocumentFragment does not contain valid MutationToken.");
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
