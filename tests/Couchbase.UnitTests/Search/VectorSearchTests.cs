using System;
using System.Collections.Generic;
using Couchbase.Core.Exceptions;
using Couchbase.Management.Search;
using Couchbase.Search.Queries.Vector;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Couchbase.UnitTests.Search;

public class VectorSearchTests
{
    [Fact]
    public void Float_And_Base64_VectorQuery_Throws_InvalidArgument()
    {
        Assert.Throws<InvalidArgumentException>(() =>
        {
            _ = new VectorQuery("hello", new VectorQueryOptions()).WithVector([0.36378238f]).WithBase64EncodedVector("AAA==26sbi");
        });
        Assert.Throws<InvalidArgumentException>(() =>
        {
            _ = new VectorQuery("hello", new VectorQueryOptions()).WithBase64EncodedVector("AAA==26sbi").WithVector([0.36378238f]);
        });
        Assert.Throws<InvalidArgumentException>(() =>
        {
            _ = new VectorQuery("hello", new VectorQueryOptions()).WithVector(null).WithBase64EncodedVector(null);
        });
    }

    [Fact]
    public void Serialized_Vector_Query_Should_Only_Contain_Non_Null_Fields()
    {
        var onlyName = new VectorQuery("vectorName");
        var onlyNameEmptyOptions = new VectorQuery("vectorName", new VectorQueryOptions());
        var nameAndBoost = new VectorQuery("vectorName", new VectorQueryOptions(4.247f));
        var vectorQuery = VectorQuery.Create("vectorName", vector: [0.3523f, 0.823f, 1f], new VectorQueryOptions(4.247f));
        var base64VectorQuery = VectorQuery.Create("vectorName", base64EncodedVector: "AA=27wdsooswub", new VectorQueryOptions(4.247f));


        var onlyNameSerialized = JsonConvert.SerializeObject(onlyName);
        var onlyNameEmptyOptionsSerialized = JsonConvert.SerializeObject(onlyNameEmptyOptions);
        var nameAndBoostSerialized = JsonConvert.SerializeObject(nameAndBoost);
        var vectorQuerySerialized = JsonConvert.SerializeObject(vectorQuery);
        var base64VectorQuerySerialized = JsonConvert.SerializeObject(base64VectorQuery);

        Assert.Equal("{\"field\":\"vectorName\",\"k\":3}", onlyNameSerialized);
        Assert.Equal("{\"field\":\"vectorName\",\"k\":3}", onlyNameEmptyOptionsSerialized);
        Assert.Equal("{\"field\":\"vectorName\",\"k\":3,\"boost\":4.247}", nameAndBoostSerialized);
        Assert.Equal("{\"field\":\"vectorName\",\"vector\":[0.3523,0.823,1.0],\"k\":3,\"boost\":4.247}", vectorQuerySerialized);
        Assert.Equal("{\"field\":\"vectorName\",\"vector_base64\":\"AA=27wdsooswub\",\"k\":3,\"boost\":4.247}", base64VectorQuerySerialized);
    }

    [Fact]
    public void SearchIndexManager_Should_Successfully_Detect_Indexes_With_Vector_Mappings()
    {
        var vectorIndex = JsonConvert.DeserializeObject<SearchIndex>(VectorIndex);
        var nonVectorIndex = JsonConvert.DeserializeObject<SearchIndex>(NonVectorIndex);
        var vectorIndexNesting = JsonConvert.DeserializeObject<SearchIndex>(VectorMappingsWithNesting);
        var vectorIndexTripleNested = JsonConvert.DeserializeObject<SearchIndex>(VectorIndexTripleNested);

        var shouldBeTrue = ContainsVectorMappings(vectorIndex);
        var shouldBeFalse = ContainsVectorMappings(nonVectorIndex);
        var shouldBeTrueTripleNested = ContainsVectorMappings(vectorIndexTripleNested);
        var shouldBeTrueNesting = ContainsVectorMappings(vectorIndexNesting);

        Assert.True(shouldBeTrue);
        Assert.False(shouldBeFalse);
        Assert.True(shouldBeTrueTripleNested);
        Assert.True(shouldBeTrueNesting);

    }

    private static bool ContainsVectorMappings(SearchIndex index)
    {
        var json = JObject.Parse(JsonConvert.SerializeObject(index));
        var typesObject = json.SelectToken("params.mapping.types");
        if (typesObject != null)
        {
            foreach (var typesProperty in ((JObject)typesObject).Properties())
            {
                if (((JObject)typesProperty.Value).TryGetValue("properties", out var props))
                {
                    using var enumerator = ((JObject)props).Properties().GetEnumerator();
                    return RecurseInnerProperties(enumerator);
                }
            }
        }
        return false;
    }
    private static bool RecurseInnerProperties(IEnumerator<JProperty> innerProperties)
    {
        while (innerProperties.MoveNext())
        {
            var element = innerProperties.Current;
            if (((JObject)element!.Value).TryGetValue("fields", out var fields))
            {
                foreach (var jToken in (JArray)fields)
                {
                    var field = (JObject)jToken;
                    if (field.TryGetValue("type", out var typeValue))
                    {
                        if (typeValue.ToString().StartsWith("vector", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            else if (((JObject)element.Value).TryGetValue("properties", out var innerProps))
            {
                return RecurseInnerProperties(((JObject)innerProps).Properties().GetEnumerator());
            }

            return false;
        }
        return false;
    }


    private static readonly string VectorIndex = """
                                           {"uuid": "null",
                                             "name": "global_index_56178f",
                                             "sourceName": "default",
                                             "type": "fulltext-index",
                                             "params": {
                                               "doc_config": {
                                                 "mode": "scope.collection.type_field",
                                                 "docid_prefix_delim": "",
                                                 "docid_regexp": "",
                                                 "type_field": "type"
                                               },
                                               "mapping": {
                                                 "default_datetime_parser": "dateTimeOptional",
                                                 "docvalues_dynamic": false,
                                                 "types": {
                                                   "scope_56178f.coll_56178f": {
                                                     "dynamic": false,
                                                     "enabled": true,
                                                     "properties": {
                                                       "vector_field": {
                                                         "fields": [
                                                           {
                                                             "name": "vector_field",
                                                             "index": true,
                                                             "store": true,
                                                             "dims": 1536,
                                                             "type": "vector",
                                                             "similarity": "l2_norm"
                                                           }
                                                         ],
                                                         "dynamic": false,
                                                         "enabled": true
                                                       },
                                                       "text": {
                                                         "fields": [
                                                           {
                                                             "name": "text",
                                                             "index": true,
                                                             "store": true,
                                                             "type": "text"
                                                           }
                                                         ],
                                                         "dynamic": false,
                                                         "enabled": true
                                                       }
                                                     }
                                                   }
                                                 },
                                                 "default_type": "_default",
                                                 "type_field": "_type",
                                                 "default_field": "_all",
                                                 "default_mapping": {
                                                   "dynamic": true,
                                                   "enabled": false
                                                 },
                                                 "index_dynamic": true,
                                                 "default_analyzer": "standard",
                                                 "store_dynamic": false
                                               },
                                               "store": {
                                                 "segmentVersion": 16,
                                                 "indexType": "scorch"
                                               }
                                             },
                                             "sourceUuid": "null",
                                             "sourceParams": {},
                                             "sourceType": "gocbcore",
                                             "planParams": {
                                               "numReplicas": 0,
                                               "indexPartitions": 1,
                                               "maxPartitionsPerPIndex": 1024
                                             }
                                           }

                                           """;

    private static readonly string VectorIndexTripleNested = """
                                           {
                                            "uuid": "null",
                                            "name": "global_index_56178f",
                                            "sourceName": "default",
                                            "type": "fulltext-index",
                                            "params": {
                                                  "doc_config": {
                                                    "mode": "scope.collection.type_field",
                                                    "docid_prefix_delim": "",
                                                    "docid_regexp": "",
                                                    "type_field": "type"
                                                  },
                                                  "mapping": {
                                                      "default_analyzer": "standard",
                                                      "default_datetime_parser": "dateTimeOptional",
                                                      "default_field": "_all",
                                                      "default_mapping": {
                                                          "dynamic": true,
                                                          "enabled": false
                                                      },
                                                      "default_type": "_default",
                                                      "docvalues_dynamic": false,
                                                      "index_dynamic": true,
                                                      "store_dynamic": false,
                                                      "type_field": "_type",
                                                      "types": {
                                                          "inventory.hotel": {
                                                              "dynamic": false,
                                                              "enabled": true,
                                                              "properties": {
                                                                  "reviews": {
                                                                      "dynamic": false,
                                                                      "enabled": true,
                                                                      "properties": {
                                                                          "reviews2": {
                                                                               "properties": {
                                                                                   "reviews3": {
                                                                                       "properties": {
                                                                                          "vector_field": {
                                                                                              "enabled": true,
                                                                                              "dynamic": false,
                                                                                              "fields": [
                                                                                                  {
                                                                                                      "dims": 1536,
                                                                                                      "index": true,
                                                                                                      "name": "vector_field",
                                                                                                      "similarity": "l2_norm",
                                                                                                      "store": true,
                                                                                                      "type": "vector"
                                                                                                  }
                                                                                              ]
                                                                                          }
                                                                                       }
                                                                                   }
                                                                               }
                                                                          }
                                                                      }
                                                                  },
                                                                  "city": {
                                                                      "enabled": true,
                                                                      "dynamic": false,
                                                                      "fields": [
                                                                          {
                                                                              "docvalues": true,
                                                                              "include_in_all": true,
                                                                              "include_term_vectors": true,
                                                                              "index": true,
                                                                              "name": "city",
                                                                              "store": true,
                                                                              "type": "text"
                                                                          }
                                                                      ]
                                                                  }
                                                              }
                                                          }
                                                      },
                                                      "store": {
                                                          "indexType": "scorch",
                                                          "segmentVersion": 16
                                                      }
                                                  }
                                               }
                                           }

                                           """;
    private static readonly string NonVectorIndex = """
                                           {
                                            "uuid": "null",
                                            "name": "global_index_56178f",
                                            "sourceName": "default",
                                            "type": "fulltext-index",
                                            "params": {
                                                  "doc_config": {
                                                    "mode": "scope.collection.type_field",
                                                    "docid_prefix_delim": "",
                                                    "docid_regexp": "",
                                                    "type_field": "type"
                                                  },
                                                  "mapping": {
                                                      "default_analyzer": "standard",
                                                      "default_datetime_parser": "dateTimeOptional",
                                                      "default_field": "_all",
                                                      "default_mapping": {
                                                          "dynamic": true,
                                                          "enabled": false
                                                      },
                                                      "default_type": "_default",
                                                      "docvalues_dynamic": false,
                                                      "index_dynamic": true,
                                                      "store_dynamic": false,
                                                      "type_field": "_type",
                                                      "types": {
                                                          "inventory.hotel": {
                                                              "dynamic": false,
                                                              "enabled": true,
                                                              "properties": {
                                                                  "reviews": {
                                                                      "dynamic": false,
                                                                      "enabled": true,
                                                                      "properties": {
                                                                          "reviews2": {
                                                                               "properties": {
                                                                                   "reviews3": {
                                                                                       "properties": {
                                                                                          "vector_field": {
                                                                                              "enabled": true,
                                                                                              "dynamic": false,
                                                                                              "fields": [
                                                                                                  {
                                                                                                      "dims": 1536,
                                                                                                      "index": true,
                                                                                                      "name": "vector_field",
                                                                                                      "similarity": "l2_norm",
                                                                                                      "store": true,
                                                                                                      "type": "NOT_vector"
                                                                                                  }
                                                                                              ]
                                                                                          }
                                                                                       }
                                                                                   }
                                                                               }
                                                                          }
                                                                      }
                                                                  },
                                                                  "city": {
                                                                      "enabled": true,
                                                                      "dynamic": false,
                                                                      "fields": [
                                                                          {
                                                                              "docvalues": true,
                                                                              "include_in_all": true,
                                                                              "include_term_vectors": true,
                                                                              "index": true,
                                                                              "name": "city",
                                                                              "store": true,
                                                                              "type": "text"
                                                                          }
                                                                      ]
                                                                  }
                                                              }
                                                          }
                                                      },
                                                      "store": {
                                                          "indexType": "scorch",
                                                          "segmentVersion": 16
                                                      }
                                                  }
                                               }
                                           }
                                           """;

    private static readonly string VectorMappingsWithNesting = """
                                                               {
                                                                 "uuid": "null",
                                                                 "name": "global_index_56178f",
                                                                 "sourceName": "default",
                                                                 "type": "fulltext-index",
                                                                 "params": {
                                                                   "doc_config": {
                                                                     "mode": "scope.collection.type_field",
                                                                     "docid_prefix_delim": "",
                                                                     "docid_regexp": "",
                                                                     "type_field": "type"
                                                                   },
                                                                   "doc_config": {
                                                                       "docid_prefix_delim": "",
                                                                       "docid_regexp": "",
                                                                       "mode": "scope.collection.type_field",
                                                                       "type_field": "type"
                                                                   },
                                                                   "mapping": {
                                                                       "default_analyzer": "standard",
                                                                       "default_datetime_parser": "dateTimeOptional",
                                                                       "default_field": "_all",
                                                                       "default_mapping": {
                                                                           "dynamic": true,
                                                                           "enabled": false
                                                                       },
                                                                       "default_type": "_default",
                                                                       "docvalues_dynamic": false,
                                                                       "index_dynamic": true,
                                                                       "store_dynamic": false,
                                                                       "type_field": "_type",
                                                                       "types": {
                                                                           "inventory.hotel": {
                                                                               "dynamic": false,
                                                                               "enabled": true,
                                                                               "properties": {
                                                                                   "reviews": {
                                                                                       "dynamic": false,
                                                                                       "enabled": true,
                                                                                       "properties": {
                                                                                           "vector_field": {
                                                                                               "enabled": true,
                                                                                               "dynamic": false,
                                                                                               "fields": [
                                                                                                   {
                                                                                                       "dims": 1536,
                                                                                                       "index": true,
                                                                                                       "name": "vector_field",
                                                                                                       "similarity": "l2_norm",
                                                                                                       "store": true,
                                                                                                       "type": "vector"
                                                                                                   }
                                                                                               ]
                                                                                           },
                                                                                           "content": {
                                                                                               "enabled": true,
                                                                                               "dynamic": false,
                                                                                               "fields": [
                                                                                                   {
                                                                                                       "docvalues": true,
                                                                                                       "include_in_all": true,
                                                                                                       "include_term_vectors": true,
                                                                                                       "index": true,
                                                                                                       "name": "content",
                                                                                                       "store": true,
                                                                                                       "type": "text",
                                                                                                       "analyzer": "My_Analyzer"
                                                                                                   }
                                                                                               ]
                                                                                           }
                                                                                       }
                                                                                   },
                                                                                   "city": {
                                                                                       "enabled": true,
                                                                                       "dynamic": false,
                                                                                       "fields": [
                                                                                           {
                                                                                               "docvalues": true,
                                                                                               "include_in_all": true,
                                                                                               "include_term_vectors": true,
                                                                                               "index": true,
                                                                                               "name": "city",
                                                                                               "store": true,
                                                                                               "type": "text"
                                                                                           }
                                                                                       ]
                                                                                   }
                                                                               }
                                                                           }
                                                                       },
                                                                       "store": {
                                                                           "indexType": "scorch",
                                                                           "segmentVersion": 16
                                                                       }
                                                                   }
                                                               }
                                                               }
                                                               """;
}
