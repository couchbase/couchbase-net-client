using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Sort
{
    /// <summary>
    /// Sorts the search results by a field in the hits.
    /// </summary>
    public class FieldSearchSort : SearchSortBase
    {
        private string _field;
        private FieldType _type;
        private FieldMode _mode;
        private FieldMissing _missing;

        protected override string By
        {
            get { return "field"; }
        }

        public FieldSearchSort(string field, FieldType type = FieldType.Auto, FieldMode mode = FieldMode.Default,
            FieldMissing missing = FieldMissing.Last, bool decending = false)
        {
            _field = field;
            _type = type;
            _mode = mode;
            _missing = missing;
            Decending = decending;
        }

        public FieldSearchSort Type(FieldType type)
        {
            _type = type;
            return this;
        }

        public FieldSearchSort Mode(FieldMode mode)
        {
            _mode = mode;
            return this;
        }

        public FieldSearchSort Missing(FieldMissing missing)
        {
            _missing = missing;
            return this;
        }

        [RequiresUnreferencedCode(SearchClient.SearchRequiresUnreferencedMembersWarning)]
        [RequiresDynamicCode(SearchClient.SearchRequiresDynamicCodeWarning)]
        public override JObject Export()
        {
            var json = base.Export();
            json.Add(new JProperty("field", _field));

            if (_type != FieldType.Auto)
            {
                json.Add(new JProperty("type", _type.ToString().ToLowerInvariant()));
            }
            if (_mode != FieldMode.Default)
            {
                json.Add(new JProperty("mode", _mode.ToString().ToLowerInvariant()));
            }
            if (_missing != FieldMissing.Last)
            {
                json.Add(new JProperty("missing", _missing.ToString().ToLowerInvariant()));
            }

            return json;
        }
    }

    public enum FieldType
    {
        Auto,
        String,
        Number,
        Date
    }

    public enum FieldMode
    {
        Default,
        Min,
        Max
    }

    public enum FieldMissing
    {
        First,
        Last
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
