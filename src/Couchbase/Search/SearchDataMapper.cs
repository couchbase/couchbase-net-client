using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.DataMapping;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search
{
    internal class SearchDataMapper  : IDataMapper
    {
        public T Map<T>(Stream stream) where T : class
        {
            var response = new SearchResult();
            using (var reader = new JsonTextReader(new StreamReader(stream)))
            {
                while (reader.Read())
                {
                    ReadStatus(reader, response);
                    ReadHits(reader, response);
                }
            }
            return response as T;
        }

        public async ValueTask<T> MapAsync<T>(Stream stream, CancellationToken cancellationToken = default) where T : class
        {
            var response = new SearchResult();
            using (var reader = new JsonTextReader(new StreamReader(stream)))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    ReadStatus(reader, response);
                    await ReadHitsAsync(reader, response, cancellationToken).ConfigureAwait(false);
                }
            }
            return response as T;
        }

        private static void ReadHits(JsonTextReader reader, SearchResult response)
        {
            if (reader.TokenType == JsonToken.StartObject && reader.Path.Contains("hits["))
            {
                var hit = JObject.Load(reader);
                response.Add(ParseSearchQueryRow(hit));
            }
        }

        private static async Task ReadHitsAsync(JsonTextReader reader, SearchResult response, CancellationToken cancellationToken)
        {
            if (reader.TokenType == JsonToken.StartObject && reader.Path.Contains("hits["))
            {
                var hit = await JObject.LoadAsync(reader, cancellationToken).ConfigureAwait(false);
                response.Add(ParseSearchQueryRow(hit));
            }
        }

        private static SearchQueryRow ParseSearchQueryRow(JObject hit) => new SearchQueryRow
        {
            Index = hit["index"].Value<string>(),
            Id = hit["id"].Value<string>(),
            Score = hit["score"].Value<double>(),
            Explanation = ReadValue<dynamic>(hit, "explanation"),
            Locations = ReadValue<dynamic>(hit, "locations"),
            Fragments = ReadValue<Dictionary<string, List<string>>>(hit, "fragments"),
            Fields = ReadValue<Dictionary<string, dynamic>>(hit, "fields")
        };

        private static T ReadValue<T>(JObject hit, string field)
        {
            if (hit.Properties().Any(x => x.Name == field))
            {
                return hit[field].ToObject<T>();
            }
            return default(T);
        }

        private static void ReadStatus(JsonTextReader reader, SearchResult response)
        {
            if (reader.Path == "total_hits")
            {
                var totalHits = reader.ReadAsInt32();
                if (totalHits != null)
                {
                    response.MetaData.TotalHits = (long)totalHits;
                    return;
                }
            }
            if (reader.Path == "max_score")
            {
                var maxScore = reader.ReadAsDecimal();
                if (maxScore != null)
                {
                    response.MetaData.MaxScore = (double)maxScore;
                    return;
                }
            }
            if (reader.Path == "took")
            {
                var took = reader.ReadAsString();
                if (took != null)
                {
                    response.MetaData.TimeTook = new TimeSpan(long.Parse(took));
                    return;
                }
            }
            if (reader.Path == "status.failed")
            {
                var failed = reader.ReadAsInt32();
                if (failed != null)
                {
                    response.MetaData.ErrorCount = (long)failed;
                    //response.Success = failed.Value == 0;
                    return;
                }
            }
            if (reader.Path == "status.successful")
            {
                var success = reader.ReadAsInt32();
                if (success != null)
                {
                    response.MetaData.SuccessCount = success.Value;
                    return;
                }
            }
            if (reader.Path == "status.total")
            {
                var total = reader.ReadAsInt32();
                if (total != null)
                {
                    response.MetaData.TotalCount = total.Value;
                }
            }
            if (reader.Path == "status.errors" && reader.TokenType == JsonToken.StartObject)
            {
                var obj = JObject.Load(reader);
                if(obj != null)
                {
                    response.MetaData.ErrorCount = obj.Count;
                    //if(response.Errors == null)
                    //{
                    //    response.Errors = new List<string>();
                    //}
                    //foreach(var err in obj)
                    //{
                    //    response.Errors.Add(string.Concat(err.Key, " : ", err.Value));
                    //}
                }
            }
            if (reader.Path == "facets" && reader.TokenType == JsonToken.StartObject)
            {
                var obj = JObject.Load(reader);
                if (obj != null)
                {
                    foreach (var item in obj)
                    {
                        if (item.Value["terms"] != null)
                        {
                            var result = item.Value.ToObject<TermFacetResult>();
                            result.Name = item.Key;
                            result.Terms = item.Value["terms"].ToObject<List<Term>>();
                            response.Facets.Add(item.Key, result);
                        }
                        else if (item.Value["numeric_ranges"] != null)
                        {
                            var result = item.Value.ToObject<NumericRangeFacetResult>();
                            result.Name = item.Key;
                            result.NumericRanges = item.Value["numeric_ranges"].ToObject<List<NumericRange>>();
                            response.Facets.Add(item.Key, result);
                        }
                        else if (item.Value["date_ranges"] != null)
                        {
                            var result =  item.Value.ToObject<DateRangeFacetResult>();
                            result.Name = item.Key;
                            result.DateRanges = item.Value["date_ranges"].ToObject<List<DateRange>>();
                            response.Facets.Add(item.Key, result);
                        }
                        else
                        {
                            var result = item.Value.ToObject<DefaultFacetResult>();
                            result.Name = item.Key;
                            response.Facets.Add(item.Key, result);
                        }
                    }
                }
            }
        }

        public ValueTask<ISearchResult> MapAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            return ((IDataMapper) this).MapAsync<ISearchResult>(stream, cancellationToken);
        }
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
