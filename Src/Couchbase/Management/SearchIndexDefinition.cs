using Couchbase.Utils;
using Newtonsoft.Json.Linq;

namespace Couchbase.Management
{
    /// <summary>
    /// Definition used to create Full Text Search indexes.
    /// </summary>
    public class SearchIndexDefinition
    {
        public string IndexName { get; }
        public string SourceName { get; }

        public SearchIndexType IndexType { get; set; } = SearchIndexType.FullText;
        public SearchIndexSourceType SourceType { get; set; } = SearchIndexSourceType.Couchbase;
        public string Uuid { get; set; }
        public string SourceUuid { get; set; }
        public string PlanParameters { get; set; }
        public string IndexParameters { get; set; }
        public string SourceParameters { get; set; }

        public SearchIndexDefinition(string name, string sourceName)
        {
            IndexName = name;
            SourceName = sourceName;
        }

        public string ToJson()
        {
            var json = new JObject
            {
                {"type", IndexType.GetDescription()},
                {"name", IndexName},
                {"sourceType", SourceType.GetDescription()},
                {"sourceName", SourceName}
            };

            if (!string.IsNullOrWhiteSpace(Uuid))
            {
                json.Add("uuid", Uuid);
            }

            if (!string.IsNullOrWhiteSpace(SourceUuid))
            {
                json.Add("sourceUUID", SourceUuid);
            }

            if (!string.IsNullOrWhiteSpace(PlanParameters))
            {
                json.Add("planParams", PlanParameters);
            }

            if (!string.IsNullOrWhiteSpace(IndexParameters))
            {
                json.Add("params", IndexParameters);
            }

            if (!string.IsNullOrWhiteSpace(SourceParameters))
            {
                json.Add("sourceParams", SourceParameters);
            }

            return json.ToString();
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2018 Couchbase, Inc.
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
