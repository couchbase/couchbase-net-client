using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Simple
{
    /// <summary>
    /// A boolean field query matches documents which have a boolean field which corresponds to the requested boolean value.
    /// </summary>
    /// <seealso cref="SearchQueryBase" />
    public class BooleanFieldQuery : SearchQueryBase
    {
        private readonly bool _fieldMatch;
        private string _field;

        public BooleanFieldQuery(bool fieldMatch)
        {
            _fieldMatch = fieldMatch;
        }

        /// <summary>
        /// The field for the match.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns></returns>
        public BooleanFieldQuery Field(string field)
        {
            _field = field;
            return this;
        }

        [RequiresUnreferencedCode(SearchClient.SearchRequiresUnreferencedMembersWarning)]
        [RequiresDynamicCode(SearchClient.SearchRequiresDynamicCodeWarning)]
        public override JObject Export()
        {
            var json = base.Export();
            json.Add(new JProperty("bool", _fieldMatch));

            if (!string.IsNullOrEmpty(_field))
            {
                json.Add(new JProperty("field", _field));
            }

            return json;
        }

        public void Deconstruct(out bool fieldMatch, out string field)
        {
            fieldMatch = _fieldMatch;
            field = _field;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out bool fieldMatch, out string field);
            return new ReadOnly(fieldMatch, field);
        }

        public record ReadOnly(bool FieldMatch, string Field);
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
