using System;
using System.Collections;
using System.Collections.Generic;
using Couchbase.Core;

namespace Couchbase.Services.Query
{
   /// <summary>
    /// Represents a composition of <see cref="MutationToken"/>'s into a single
    /// unit for performing "read your own writes" or RYOW semantics on a N1QL query.
    /// </summary>
    public sealed class MutationState : IEnumerable<MutationToken>
    {
        private readonly List<MutationToken> _tokens = new List<MutationToken>();

        /// <summary>
        /// Creates a <see cref="MutationToken"/> from a list of <see cref="IGetResult{T}"/>'s assuming enhanced durability is enabled.
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
        /// Adds a <see cref="MutationToken"/> to the <see cref="MutationState"/> from a list of <see cref="IGetResult{T}"/> assuming enhanced durability is enabled.
        /// </summary>
        /// <param name="mutationResults">The mutationResults.</param>
        /// <exception cref="ArgumentException">If a <see cref="IGetResult{T}"/> does not contain a valid <see cref="MutationToken"/>.</exception>
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
    }
}
