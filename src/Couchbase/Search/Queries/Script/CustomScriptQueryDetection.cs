#nullable enable
using System.Linq;
using Couchbase.Search.Queries.Compound;

namespace Couchbase.Search.Queries.Script
{
    /// <summary>
    /// Finds <see cref="CustomScoreQuery"/> and <see cref="CustomFilterQuery"/> anywhere in a search request, so the
    /// SDK can check for the cluster capability before it sends anything (SDK-RFC 52).
    /// </summary>
    internal static class CustomScriptQueryDetection
    {
        // A user's own ISearchQuery implementation is opaque, so only the SDK's compound types are searched.
        internal static bool ContainsCustomScriptQuery(this ISearchQuery? query) => query switch
        {
            CustomScoreQuery or CustomFilterQuery => true,
            ConjunctionQuery conjunction => conjunction.Any(ContainsCustomScriptQuery),
            DisjunctionQuery disjunction => disjunction.Any(ContainsCustomScriptQuery),
            BooleanQuery boolean => boolean.AsReadOnly() is var parts
                                    && (parts.MustQueries.ContainsCustomScriptQuery()
                                        || parts.ShouldQueries.ContainsCustomScriptQuery()
                                        || parts.MustNotQueries.ContainsCustomScriptQuery()),
            _ => false
        };

        // Includes vector query prefilters, which are tested by FIT even though the RFC names only compound queries.
        internal static bool ContainsCustomScriptQuery(this SearchRequest request) =>
            request.SearchQuery.ContainsCustomScriptQuery()
            || request.VectorSearch?.VectorQueries?.Any(vectorQuery => vectorQuery.Filter.ContainsCustomScriptQuery()) == true;
    }
}

#region [ License information          ]

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

#endregion
