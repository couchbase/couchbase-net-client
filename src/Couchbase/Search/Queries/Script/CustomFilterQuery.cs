#nullable enable
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Exceptions;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Script
{
    /// <summary>
    /// A query that passes the results of an inner query through a user-defined JavaScript filtering function.
    /// Only the results for which the function returns true are kept. Their scores are not changed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Requires Couchbase Server 8.5.0 or later, Enterprise Edition. Custom script queries are disabled on the
    /// Search service by default; enable them by setting <c>customScriptQueriesEnabled</c> to <c>"true"</c> with
    /// <c>PUT /api/managerOptions</c> on a Search node. The SDK cannot detect this setting, so while it is disabled
    /// the server rejects the query. Not available over couchbase2://.
    /// </para>
    /// <para>
    /// The function is called once for each result, with <c>doc</c> and <c>params</c>:
    /// <list type="bullet">
    /// <item><description><c>doc._id</c>: the document ID.</description></item>
    /// <item><description><c>doc.score</c>: the score from the inner query.</description></item>
    /// <item><description><c>doc.fields</c>: only the fields requested with <see cref="Fields"/>; undefined if
    /// none are requested. See <see cref="Fields"/> for how text values appear.</description></item>
    /// <item><description><c>params</c>: the object given to <see cref="Parameters"/>, the same for every
    /// result; <c>null</c> if none is given.</description></item>
    /// </list>
    /// An exception in the function fails the whole query.
    /// </para>
    /// </remarks>
    [InterfaceStability(Level.Uncommitted)]
    public sealed class CustomFilterQuery : ISearchQuery
    {
        private readonly CustomScriptQuery _query;

        /// <summary>
        /// Creates a <see cref="CustomFilterQuery"/>.
        /// </summary>
        /// <param name="query">The inner query whose results are passed through the filtering function.</param>
        /// <param name="source">
        /// A JavaScript function that takes <c>(doc, params)</c> and returns a boolean, for example
        /// <c>function filter(doc, params) { return doc.score >= 0.5; }</c>.
        /// </param>
        public CustomFilterQuery(ISearchQuery query, string source)
        {
            _query = new CustomScriptQuery(query, source);
        }

        /// <summary>
        /// The document fields made available to the filtering function, as <c>doc.fields</c>.
        /// The function sees no fields unless this is set. Pass <c>"*"</c> for all fields.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The values come from the Search index, not from the stored document, so a field must be indexed to be
        /// available. Numbers and booleans are unchanged. Text values are the terms produced by the field's
        /// analyzer. With the default <c>standard</c> analyzer, text is lowercased, punctuation and stop words are
        /// removed, and the remaining terms are de-duplicated and sorted. Word order is lost.
        /// </para>
        /// <para>
        /// A text value with one term is a string and a value with several terms is an array: <c>"Dublin"</c>
        /// becomes <c>"dublin"</c>, but <c>"New York"</c> becomes <c>["new", "york"]</c>. Write functions that
        /// handle both, for example <c>[].concat(doc.fields.city || [])</c>. To get the exact original text, map
        /// the field with the <c>keyword</c> analyzer in the index definition.
        /// </para>
        /// </remarks>
        /// <returns>This <see cref="CustomFilterQuery"/> for chaining.</returns>
        public CustomFilterQuery Fields(params string[] fields)
        {
            _query.SetFields(fields);
            return this;
        }

        /// <summary>
        /// Parameters made available to the filtering function, as <c>params</c>.
        /// Values may be any JSON-serializable value, including nested dictionaries and lists.
        /// The same parameters are given to the function for every result.
        /// </summary>
        /// <returns>This <see cref="CustomFilterQuery"/> for chaining.</returns>
        public CustomFilterQuery Parameters(IDictionary<string, object?> parameters)
        {
            _query.SetParameters(parameters);
            return this;
        }

        /// <summary>
        /// Not supported. The Search service ignores a boost on this query type, so apply the boost to the inner
        /// query instead.
        /// </summary>
        /// <exception cref="InvalidArgumentException">Always.</exception>
        public ISearchQuery Boost(double boost) =>
            throw new InvalidArgumentException(
                "CustomFilterQuery does not support boost. Apply the boost to the inner query instead.");

        [RequiresUnreferencedCode(SearchClient.SearchRequiresUnreferencedMembersWarning)]
        [RequiresDynamicCode(SearchClient.SearchRequiresDynamicCodeWarning)]
        public JObject Export() => _query.Export("custom_filter");

        public ReadOnly AsReadOnly() =>
            new(_query.Query, _query.Source, _query.Fields, _query.Parameters);

        public record ReadOnly(
            ISearchQuery Query,
            string Source,
            IReadOnlyList<string>? Fields,
            IReadOnlyDictionary<string, object?>? Parameters);
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
