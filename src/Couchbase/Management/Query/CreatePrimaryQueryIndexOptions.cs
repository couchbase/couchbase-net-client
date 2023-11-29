using System;
using System.Threading;

using CancellationTokenCls = System.Threading.CancellationToken;

#nullable enable

namespace Couchbase.Management.Query
{
    public class CreatePrimaryQueryIndexOptions
    {
        public static readonly ReadOnly DefaultReadOnly = Default.AsReadOnly();
        internal string? IndexNameValue { get; set; }
        internal bool IgnoreIfExistsValue { get; set; }
        internal bool DeferredValue { get; set; }
        internal CancellationToken TokenValue { get; private set; } = CancellationTokenCls.None;
        internal TimeSpan? TimeoutValue { get; set; }
        internal string? ScopeNameValue { get; set; }
        internal string? CollectionNameValue { get; set; }
        internal string? QueryContext { get; set; }

        /// <summary>
        /// Sets the scope name for this query management operation.
        /// </summary>
        /// <remarks>If the scope name is set then the collection name must be set as well.</remarks>
        /// <param name="scopeName">The scope name to use.</param>
        /// <returns>A CreateQueryIndexOptions for chaining options.</returns>
        [Obsolete("Use collection.QueryIndexes instead.")]
        public CreatePrimaryQueryIndexOptions ScopeName(string scopeName)
        {
            ScopeNameValue = scopeName;
            return this;
        }

        /// <summary>
        /// Sets the collection name for this query management operation.
        /// </summary>
        /// <remarks>If the collection name is set then the scope name must be set as well.</remarks>
        /// <param name="collectionName">The collection name to use.</param>
        /// <returns>A CreateQueryIndexOptions for chaining options.</returns>
        [Obsolete("Use collection.QueryIndexes instead.")]
        public CreatePrimaryQueryIndexOptions CollectionName(string collectionName)
        {
            CollectionNameValue = collectionName;
            return this;
        }

        public CreatePrimaryQueryIndexOptions IndexName(string indexName)
        {
            IndexNameValue = indexName;
            return this;
        }

        public CreatePrimaryQueryIndexOptions IgnoreIfExists(bool ignoreIfExists)
        {
            IgnoreIfExistsValue = ignoreIfExists;
            return this;
        }

        public CreatePrimaryQueryIndexOptions Deferred(bool deferred)
        {
            DeferredValue = deferred;
            return this;
        }

        public CreatePrimaryQueryIndexOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public CreatePrimaryQueryIndexOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public static CreatePrimaryQueryIndexOptions Default => new CreatePrimaryQueryIndexOptions();

        public void Deconstruct(out string? indexNameValue, out bool ignoreIfExistsValue, out bool deferredValue, out CancellationToken tokenValue, out string? scopeNameValue, out string? collectionNameValue, out string? queryContext, out TimeSpan? timeoutValue)
        {
            indexNameValue = IndexNameValue;
            ignoreIfExistsValue = IgnoreIfExistsValue;
            deferredValue = DeferredValue;
            tokenValue = TokenValue;
            scopeNameValue = ScopeNameValue;
            collectionNameValue = CollectionNameValue;
            queryContext = QueryContext;
            timeoutValue = TimeoutValue;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out string? indexNameValue, out bool ignoreIfExistsValue, out bool deferredValue, out CancellationToken tokenValue, out string? scopeNameValue, out string? collectionNameValue, out string? queryContext, out TimeSpan? timeoutValue);
            return new ReadOnly(indexNameValue, ignoreIfExistsValue, deferredValue, tokenValue, scopeNameValue,
                collectionNameValue, queryContext, timeoutValue);
        }
        public record ReadOnly(
            string? IndexNameValue,
            bool IgnoreIfExistsValue,
            bool DeferredValue,
            CancellationToken TokenValue,
            string? ScopeNameValue,
            string? CollectionNameValue,
            string? QueryContext,
            TimeSpan? TimeoutValue);
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
