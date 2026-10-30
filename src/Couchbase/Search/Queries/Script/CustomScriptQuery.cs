#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Script
{
    /// <summary>
    /// Shared state and JSON export for <see cref="CustomScoreQuery"/> and <see cref="CustomFilterQuery"/>,
    /// which differ only in their top-level JSON key.
    /// </summary>
    internal sealed class CustomScriptQuery
    {
        public CustomScriptQuery(ISearchQuery query, string source)
        {
            Query = query ?? throw new ArgumentNullException(nameof(query));
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException("The source must not be empty or whitespace.", nameof(source));
            }
            Source = source;
        }

        public ISearchQuery Query { get; }

        public string Source { get; }

        public IReadOnlyList<string>? Fields { get; private set; }

        public IReadOnlyDictionary<string, object?>? Parameters { get; private set; }

        // Both setters copy, so changing the caller's collection afterwards does not change the query.
        public void SetFields(string[] fields)
        {
            if (fields is null)
            {
                throw new ArgumentNullException(nameof(fields));
            }
            Fields = fields.ToArray();
        }

        public void SetParameters(IDictionary<string, object?> parameters)
        {
            if (parameters is null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }
            Parameters = new Dictionary<string, object?>(parameters);
        }

        [RequiresUnreferencedCode(SearchClient.SearchRequiresUnreferencedMembersWarning)]
        [RequiresDynamicCode(SearchClient.SearchRequiresDynamicCodeWarning)]
        public JObject Export(string queryType)
        {
            var body = new JObject
            {
                { "query", Query.Export() },
                { "source", Source }
            };

            // The RFC says to omit both when not set, so an empty value is treated as not set.
            if (Fields is { Count: > 0 })
            {
                body.Add("fields", new JArray(Fields));
            }
            if (Parameters is { Count: > 0 })
            {
                body.Add("params", JObject.FromObject(Parameters));
            }

            return new JObject { { queryType, body } };
        }
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
