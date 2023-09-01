using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Simple
{
    /// <summary>
    /// The phrase query allows to query for exact term phrases in the index. The provided
    /// terms must exist in the correct order, at the correct index offsets, in the specified field
    /// (as no analyzer are applied to the terms). Queried field must have been indexed with
    /// includeTermVectors set to true. It is generally more useful in debugging scenarios,
    /// and the Match Phrase Query should usually be preferred for real-world use cases.
    /// </summary>
    /// <seealso cref="SearchQueryBase" />
    public sealed class PhraseQuery : SearchQueryBase
    {
        private readonly List<string> _terms = new List<string>();
        private string _field;

        public PhraseQuery(params string[] terms)
        {
            if (terms == null || terms.Length == 0)
            {
                throw new ArgumentNullException("terms");
            }
            _terms.AddRange(terms);
        }

        /// <summary>
        /// The field to search against.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns></returns>
        public PhraseQuery Field(string field)
        {
            _field = field;
            return this;
        }

        [RequiresUnreferencedCode(SearchClient.SearchRequiresUnreferencedMembersWarning)]
        [RequiresDynamicCode(SearchClient.SearchRequiresDynamicCodeWarning)]
        public override JObject Export()
        {
            var json = base.Export();
            json.Add(new JProperty("terms", new JArray(_terms)));

            if (!string.IsNullOrEmpty(_field))
            {
                json.Add(new JProperty("field", _field));
            }

            return json;
        }

        public void Deconstruct(out IReadOnlyList<string> terms, out string field)
        {
            terms = _terms;
            field = _field;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out IReadOnlyList<string> terms, out string field);
            return new ReadOnly(terms, field);
        }

        public record ReadOnly(IReadOnlyList<string> Terms, string Field);
    }

    #region [ License information ]

    /* ************************************************************
     *
     *    @author Couchbase <info@couchbase.com>
     *    @copyright 2014 Couchbase, Inc.
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
}
