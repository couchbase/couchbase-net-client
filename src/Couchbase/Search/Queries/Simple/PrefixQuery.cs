using System;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Simple
{
    /// <summary>
    /// The prefix query finds documents containing terms that start with the provided prefix.
    /// </summary>
    /// <seealso cref="SearchQueryBase" />
    public class PrefixQuery : SearchQueryBase
    {
        private readonly string _prefix;
        private string _field;

        public PrefixQuery(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                throw new ArgumentNullException("prefix");
            }
            _prefix = prefix;
        }

        /// <summary>
        /// The field to search against.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns></returns>
        public PrefixQuery Field(string field)
        {
            _field = field;
            return this;
        }

        [RequiresUnreferencedCode(SearchClient.SearchRequiresUnreferencedMembersWarning)]
        [RequiresDynamicCode(SearchClient.SearchRequiresDynamicCodeWarning)]
        public override JObject Export()
        {
            var json = base.Export();
            json.Add(new JProperty("prefix", _prefix));

            if (!string.IsNullOrEmpty(_field))
            {
                json.Add(new JProperty("field", _field));
            }

            return json;
        }

        public void Deconstruct(out string prefix, out string field)
        {
            prefix = _prefix;
            field = _field;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out string prefix, out string field);
            return new ReadOnly(prefix, field);
        }

        public record ReadOnly(string Prefix, string Field);
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
