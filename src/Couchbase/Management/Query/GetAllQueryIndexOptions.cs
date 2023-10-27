using System;
using System.Threading;
using CancellationTokenCls = System.Threading.CancellationToken;

#nullable enable

namespace Couchbase.Management.Query
{
    public class GetAllQueryIndexOptions
    {
        public static readonly ReadOnly DefaultReadOnly = Default.AsReadOnly();
        internal CancellationToken TokenValue { get; set; } = CancellationTokenCls.None;
        internal string? ScopeNameValue { get; set; }
        internal string? CollectionNameValue { get; set; }

        internal string? QueryContext { get; set; }

        /// <summary>
        /// Sets the scope name for this query management operation.
        /// </summary>
        /// <remarks>If the scope name is set then the collection name must be set as well.</remarks>
        /// <param name="scopeName">The scope name to use.</param>
        /// <returns>A GetAllQueryIndexOptions for chaining options.</returns>
        [Obsolete("Use collection.QueryIndexes instead.")]
        public GetAllQueryIndexOptions ScopeName(string scopeName)
        {
            ScopeNameValue = scopeName;
            return this;
        }

        /// <summary>
        /// Sets the collection name for this query management operation.
        /// </summary>
        /// <remarks>If the collection name is set then the scope name must be set as well.</remarks>
        /// <param name="collectionName">The collection name to use.</param>
        /// <returns>A GetAllQueryIndexOptions for chaining options.</returns>
        [Obsolete("Use collection.QueryIndexes instead.")]
        public GetAllQueryIndexOptions CollectionName(string collectionName)
        {
            CollectionNameValue = collectionName;
            return this;
        }

        public GetAllQueryIndexOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static GetAllQueryIndexOptions Default => new GetAllQueryIndexOptions();

        public void Deconstruct(out CancellationToken tokenValue, out string? scopeNameValue, out string? collectionNameValue, out string? queryContext)
        {
            tokenValue = TokenValue;
            scopeNameValue = ScopeNameValue;
            collectionNameValue = CollectionNameValue;
            queryContext = QueryContext;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out CancellationToken tokenValue, out string? scopeNameValue, out string? collectionNameValue, out string? queryContext);
            return new ReadOnly(tokenValue, scopeNameValue, collectionNameValue, queryContext);
        }

        public record ReadOnly(
            CancellationToken TokenValue,
            string? ScopeNameValue,
            string? CollectionNameValue,
            string? QueryContext);
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
