using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Serializers;
using Couchbase.Protostellar.Search.V1;
using Couchbase.Search;
using Couchbase.Stellar.Core;
using DateRangeFacet = Couchbase.Search.DateRangeFacet;
using NumericRangeFacet = Couchbase.Search.NumericRangeFacet;
using TermFacet = Couchbase.Search.TermFacet;


#nullable enable
namespace Couchbase.Stellar.Search
{
    public class StellarSearchClient
    {
        private readonly SearchService.SearchServiceClient _searchClient;
        private readonly StellarCluster _stellarCluster;
        private readonly ITypeSerializer _serializer;
        private readonly StellarSearchDataMapper _dataMapper;

        internal StellarSearchClient(StellarCluster stellarCluster)
        {
            _stellarCluster = stellarCluster;
            _serializer = stellarCluster.TypeSerializer;
            _searchClient = new SearchService.SearchServiceClient(_stellarCluster.GrpcChannel);
            _dataMapper = new StellarSearchDataMapper();
        }

        public async Task<ISearchResult> QueryAsync(string indexName, ISearchQuery query, SearchOptions? options = null, CancellationToken cancellationToken = default)
        {
            var opts = options?.AsReadOnly() ?? SearchOptions.DefaultReadOnly;

            var searchQuery = QueryConverter(query);

            var searchQueryRequest = new Couchbase.Protostellar.Search.V1.SearchQueryRequest
            {
                IndexName = indexName,
                Query = searchQuery,
                IncludeExplanation = opts.IncludeLocations,
                DisableScoring = opts.DisableScoring,
                IncludeLocations = opts.IncludeLocations
            };

            if (opts.Limit.HasValue) searchQueryRequest.Limit = (uint)opts.Limit.Value;
            if (opts.Skip.HasValue) searchQueryRequest.Skip = (uint)opts.Skip.Value;
            if (opts.ScanConsistency.HasValue) searchQueryRequest.ScanConsistency = opts.ScanConsistency.Value.ToProto();
            if (opts.HighLightStyle != null) searchQueryRequest.HighlightStyle = Enum.Parse<HighLightStyle>(opts.HighLightStyle, ignoreCase: true).ToProto();
            if (opts.Explain.HasValue) searchQueryRequest.IncludeExplanation = opts.Explain.Value;
            if (opts.ScopeName != null) searchQueryRequest.ScopeName = opts.ScopeName;
            if (opts.CollectionNames != null) searchQueryRequest.Collections.AddRange(opts.CollectionNames);
            opts.Facets?.ToList().ForEach(facet => searchQueryRequest.Facets.Add(facet.Name, ParseFacet(facet)));

            var response = _searchClient.SearchQuery(searchQueryRequest, _stellarCluster.GrpcCallOptions(opts.TimeoutValue, opts.Token));

            var searchResult = await _dataMapper.MapAsync(response.ResponseStream, cancellationToken).ConfigureAwait(false);

            return searchResult;
        }

        private static Couchbase.Protostellar.Search.V1.Facet ParseFacet(ISearchFacet coreFacet)
        {
            if (coreFacet is TermFacet)
            {
                return new Couchbase.Protostellar.Search.V1.Facet
                {
                    TermFacet = new Couchbase.Protostellar.Search.V1.TermFacet { Field = coreFacet.Field, Size = (uint)coreFacet.Size }
                };
            }
            if (coreFacet is DateRangeFacet)
            {
                return new Couchbase.Protostellar.Search.V1.Facet
                {
                    DateRangeFacet = new Couchbase.Protostellar.Search.V1.DateRangeFacet { Field = coreFacet.Field, Size = (uint)coreFacet.Size }
                };
            }
            if (coreFacet is NumericRangeFacet)
            {
                return new Couchbase.Protostellar.Search.V1.Facet
                {
                    NumericRangeFacet = new Couchbase.Protostellar.Search.V1.NumericRangeFacet { Field = coreFacet.Field, Size = (uint)coreFacet.Size }
                };
            }

            throw new ArgumentOutOfRangeException($"Could not parse Facet {coreFacet} to a Proto object.");
        }

        private Couchbase.Protostellar.Search.V1.Query QueryConverter(ISearchQuery searchRequest,
            ISearchQuery? couchbaseQuery = null)
        {
            var protoQuery = new Couchbase.Protostellar.Search.V1.Query();

            //If no specific query is passed then use the SearchRequest.
            //This allows to determine whether the function call is recursive or not (if null then not recursive)
            couchbaseQuery ??= searchRequest;

            switch (couchbaseQuery)
            {
                case Couchbase.Search.Queries.Simple.BooleanFieldQuery query:
                {
                    var coreQuery = query.AsReadOnly();
                    protoQuery.BooleanFieldQuery = new Couchbase.Protostellar.Search.V1.BooleanFieldQuery
                    {
                        Boost = query.BoostValue,
                        Value = coreQuery.FieldMatch
                    };
                    if (coreQuery.Field != null) protoQuery.BooleanFieldQuery.Field = coreQuery.Field;
                }
                    break;
                case Couchbase.Search.Queries.Compound.BooleanQuery query:
                {
                    var coreQuery = query.AsReadOnly();
                    //--------Declare and Initialise the 3 compound queries--------
                    Couchbase.Protostellar.Search.V1.DisjunctionQuery mustNot, should;
                    Couchbase.Protostellar.Search.V1.ConjunctionQuery must;
                    mustNot = should = new Couchbase.Protostellar.Search.V1.DisjunctionQuery { Boost = coreQuery.MustNotQueries.BoostValue };
                    must = new ConjunctionQuery { Boost = coreQuery.MustQueries.BoostValue };

                    //--------Recurse to add converted queries to the above compound queries--------
                    foreach (var q in coreQuery.MustQueries.AsReadOnly().Queries)
                    {
                        must.Queries.Add(QueryConverter(searchRequest, q));
                    }

                    foreach (var q in coreQuery.MustNotQueries.AsReadOnly().Queries)
                    {
                        mustNot.Queries.Add(QueryConverter(searchRequest, q));
                    }

                    foreach (var q in coreQuery.ShouldQueries.AsReadOnly().Queries)
                    {
                        should.Queries.Add(QueryConverter(searchRequest, q));
                    }

                    //--------Initialise BooleanQuery--------
                    protoQuery.BooleanQuery = new Couchbase.Protostellar.Search.V1.BooleanQuery
                    {
                        Boost = query.BoostValue,
                        Must = must,
                        MustNot = mustNot,
                        Should = should
                    };
                }
                    break;
                case Couchbase.Search.Queries.Compound.ConjunctionQuery query:
                {
                    var coreQuery = query.AsReadOnly();
                    protoQuery.ConjunctionQuery = new Couchbase.Protostellar.Search.V1.ConjunctionQuery
                    {
                        Boost = query.BoostValue
                    };
                    if (coreQuery.Queries != null)
                    {
                        foreach (var q in coreQuery.Queries)
                        {
                            protoQuery.ConjunctionQuery.Queries.Add(QueryConverter(searchRequest, q));
                        }
                    }
                }
                    break;
                case Couchbase.Search.Queries.Compound.DisjunctionQuery query:
                {
                    var coreQuery = query.AsReadOnly();
                    protoQuery.DisjunctionQuery = new Couchbase.Protostellar.Search.V1.DisjunctionQuery
                    {
                        Boost = query.BoostValue,
                        Minimum = (uint)coreQuery.Min
                    };
                    if (coreQuery.Queries != null)
                    {
                        foreach (var q in coreQuery.Queries)
                        {
                            protoQuery.DisjunctionQuery.Queries.Add(QueryConverter(searchRequest, q));
                        }
                    }
                }
                    break;
                case Couchbase.Search.Queries.Range.DateRangeQuery query:
                {
                    var coreQuery = query.AsReadOnly();
                    protoQuery.DateRangeQuery = new Couchbase.Protostellar.Search.V1.DateRangeQuery
                    {
                        Boost = query.BoostValue
                    };
                    if (coreQuery.Field != null) protoQuery.DateRangeQuery.Field = coreQuery.Field;
                    if (coreQuery.ParserName != null) protoQuery.DateRangeQuery.DateTimeParser = coreQuery.ParserName;
                    if (coreQuery.StartTime != null)
                        protoQuery.DateRangeQuery.StartDate =
                            coreQuery.StartTime.ToString(); //TODO: Verify the ToString() returns the expected format
                    if (coreQuery.EndTime != null) protoQuery.DateRangeQuery.EndDate = coreQuery.EndTime.ToString();
                }
                    break;
                case Couchbase.Search.Queries.Range.NumericRangeQuery query:
                {
                    var coreQuery = query.AsReadOnly();
                    protoQuery.NumericRangeQuery = new Couchbase.Protostellar.Search.V1.NumericRangeQuery
                    {
                        Boost = query.BoostValue,
                        Field = coreQuery.Field,
                        InclusiveMin = coreQuery.MinInclusive,
                        InclusiveMax = coreQuery.MaxInclusive
                    };
                    if (!coreQuery.Max.HasValue && !coreQuery.Min.HasValue)
                    {
                        throw new InvalidArgumentException(
                            "NumericRangeQuery: Either Min or Max can be omitted, but not both.");
                    }

                    if (coreQuery.Max != null) protoQuery.NumericRangeQuery.Max = (float)coreQuery.Max;
                    if (coreQuery.Min != null) protoQuery.NumericRangeQuery.Max = (float)coreQuery.Min;
                }
                    break;
                case Couchbase.Search.Queries.Range.TermRangeQuery query:
                {
                    var coreQuery = query.AsReadOnly();
                    protoQuery.TermRangeQuery = new Couchbase.Protostellar.Search.V1.TermRangeQuery
                    {
                        Boost = query.BoostValue,
                        InclusiveMin = coreQuery.MinInclusive,
                        InclusiveMax = coreQuery.MaxInclusive
                    };
                    if (coreQuery.Field != null) protoQuery.TermRangeQuery.Field = coreQuery.Field;
                    if (coreQuery.Min != null) protoQuery.TermRangeQuery.Min = coreQuery.Min;
                    if (coreQuery.Max != null) protoQuery.TermRangeQuery.Max = coreQuery.Max;
                }
                    break;
                case Couchbase.Search.Queries.Simple.TermQuery query:
                {
                    var coreQuery = query.AsReadOnly();
                    protoQuery.TermQuery = new Couchbase.Protostellar.Search.V1.TermQuery
                    {
                        Boost = query.BoostValue,
                        Fuzziness = (ulong)coreQuery.Fuzziness,
                        PrefixLength = (ulong)coreQuery.PrefixLength
                    };
                    if (coreQuery.Field != null) protoQuery.TermQuery.Field = coreQuery.Field;
                    if (coreQuery.Term != null) protoQuery.TermQuery.Term = coreQuery.Term;
                }
                    break;
                case Couchbase.Search.Queries.Geo.GeoDistanceQuery query:
                {
                    var coreQuery = query.AsReadOnly();
                    protoQuery.GeoDistanceQuery = new Couchbase.Protostellar.Search.V1.GeoDistanceQuery
                    {
                        Boost = query.BoostValue
                    };
                    if (coreQuery.Field != null) protoQuery.GeoDistanceQuery.Field = coreQuery.Field;
                    if (coreQuery.Distance != null) protoQuery.GeoDistanceQuery.Distance = coreQuery.Distance;
                    protoQuery.GeoDistanceQuery.Center = new LatLng
                    {
                        Latitude = coreQuery.Latitude ?? Double.MinValue,
                        Longitude = coreQuery.Longitude ?? Double.MaxValue
                    };
                }
                    break;
                case Couchbase.Search.Queries.Geo.GeoPolygonQuery query:
                {
                    var coreQuery = query.AsReadOnly();
                    protoQuery.GeoPolygonQuery = new Couchbase.Protostellar.Search.V1.GeoPolygonQuery
                    {
                        Boost = query.BoostValue
                    };
                    if (coreQuery.Field != null) protoQuery.GeoPolygonQuery.Field = coreQuery.Field;
                    if (coreQuery.Coordinates != null)
                    {
                        foreach (var coordinate in coreQuery.Coordinates)
                        {
                            protoQuery.GeoPolygonQuery.Vertices.Add(new LatLng
                            {
                                Latitude = coordinate.Lat,
                                Longitude = coordinate.Lon
                            });
                        }
                    }
                }
                    break;
                case Couchbase.Search.Queries.Geo.GeoBoundingBoxQuery query:
                {
                    var coreQuery = query.AsReadOnly();
                    protoQuery.GeoBoundingBoxQuery = new Couchbase.Protostellar.Search.V1.GeoBoundingBoxQuery
                    {
                        Boost = query.BoostValue,
                        BottomRight = new LatLng
                        {
                            Latitude = coreQuery.BottomRightLatitude ??
                                       Double.MinValue, // TODO: NCBC-3338 those fields aren't supposed to be nullable
                            Longitude = coreQuery.BottomRightLongitude ?? Double.MaxValue
                        },
                        TopLeft = new LatLng
                        {
                            Latitude = coreQuery.TopLeftLatitude ?? Double.MinValue,
                            Longitude = coreQuery.TopLeftLongitude ?? Double.MinValue
                        }
                    };
                    if (coreQuery.Field != null) protoQuery.GeoBoundingBoxQuery.Field = coreQuery.Field;
                }
                    break;
                case Couchbase.Search.Queries.Simple.DocIdQuery query:
                {
                    var coreQuery = query.AsReadOnly();
                    protoQuery.DocIdQuery = new Couchbase.Protostellar.Search.V1.DocIdQuery
                    {
                        Boost = query.BoostValue
                    };
                    if (coreQuery.DocIds != null) protoQuery.DocIdQuery.Ids.AddRange(coreQuery.DocIds);
                }
                    break;
                case Couchbase.Search.Queries.Simple.WildcardQuery query:
                {
                    var coreQuery = query.AsReadOnly();
                    protoQuery.WildcardQuery = new Couchbase.Protostellar.Search.V1.WildcardQuery
                    {
                        Boost = query.BoostValue
                    };
                    if (coreQuery.Field != null) protoQuery.WildcardQuery.Field = coreQuery.Field;
                    if (coreQuery.WildCard != null) protoQuery.WildcardQuery.Wildcard = coreQuery.WildCard;
                }
                    break;
                case Couchbase.Search.Queries.Simple.PhraseQuery query:
                {
                    var coreQuery = query.AsReadOnly();
                    protoQuery.PhraseQuery = new Couchbase.Protostellar.Search.V1.PhraseQuery
                    {
                        Boost = query.BoostValue
                    };
                    if (coreQuery.Field != null) protoQuery.PhraseQuery.Field = coreQuery.Field;
                    if (coreQuery.Terms != null) protoQuery.PhraseQuery.Terms.AddRange(coreQuery.Terms);
                }
                    break;
                case Couchbase.Search.Queries.Simple.PrefixQuery query:
                {
                    var coreQuery = query.AsReadOnly();
                    protoQuery.PrefixQuery = new Couchbase.Protostellar.Search.V1.PrefixQuery
                    {
                        Boost = query.BoostValue
                    };
                    if (coreQuery.Field != null) protoQuery.PrefixQuery.Field = coreQuery.Field;
                    if (coreQuery.Prefix != null) protoQuery.PrefixQuery.Prefix = coreQuery.Prefix;
                }
                    break;
                case Couchbase.Search.Queries.Simple.QueryStringQuery query:
                {
                    var coreQuery = query.AsReadOnly();
                    protoQuery.QueryStringQuery = new Couchbase.Protostellar.Search.V1.QueryStringQuery
                    {
                        Boost = query.BoostValue
                    };
                    if (coreQuery.Query != null) protoQuery.QueryStringQuery.QueryString = coreQuery.Query;
                }
                    break;
                case Couchbase.Search.Queries.Simple.RegexpQuery query:
                {
                    var coreQuery = query.AsReadOnly();
                    protoQuery.RegexpQuery = new Couchbase.Protostellar.Search.V1.RegexpQuery
                    {
                        Boost = query.BoostValue
                    };
                    if (coreQuery.Field != null) protoQuery.RegexpQuery.Field = coreQuery.Field;
                    if (coreQuery.Regex != null) protoQuery.RegexpQuery.Regexp = coreQuery.Regex;
                }
                    break;
                case Couchbase.Search.Queries.Simple.MatchAllQuery:
                    protoQuery.MatchAllQuery = new Couchbase.Protostellar.Search.V1.MatchAllQuery();
                    break;
                case Couchbase.Search.Queries.Simple.MatchNoneQuery:
                    protoQuery.MatchNoneQuery = new Couchbase.Protostellar.Search.V1.MatchNoneQuery();
                    break;
                case Couchbase.Search.Queries.Simple.MatchPhraseQuery query:
                {
                    var coreQuery = query.AsReadOnly();
                    protoQuery.MatchPhraseQuery = new Couchbase.Protostellar.Search.V1.MatchPhraseQuery
                    {
                        Boost = query.BoostValue
                    };
                    if (coreQuery.Analyzer != null) protoQuery.MatchPhraseQuery.Analyzer = coreQuery.Analyzer;
                    if (coreQuery.MatchPhrase != null) protoQuery.MatchPhraseQuery.Phrase = coreQuery.MatchPhrase;
                    if (coreQuery.Field != null) protoQuery.MatchPhraseQuery.Field = coreQuery.Field;
                }
                    break;
                case Couchbase.Search.Queries.Simple.MatchQuery query:
                {
                    var coreQuery = query.AsReadOnly();
                    protoQuery.MatchQuery = new Couchbase.Protostellar.Search.V1.MatchQuery
                    {
                        Boost = query.BoostValue,
                        Fuzziness = (ulong)coreQuery.Fuzziness,
                        PrefixLength = (ulong)coreQuery.PrefixLength
                    };
                    if (coreQuery.Field != null) protoQuery.MatchQuery.Field = coreQuery.Field;
                    if (coreQuery.Match != null) protoQuery.MatchQuery.Value = coreQuery.Match;
                    if (coreQuery.Analyzer != null) protoQuery.MatchQuery.Analyzer = coreQuery.Analyzer;
                    if (coreQuery.MatchOperator != null)
                        protoQuery.MatchQuery.Operator = coreQuery.MatchOperator.Value.ToProto();
                }
                    break;
            }

            return protoQuery;
        }
    }
}
