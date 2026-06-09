using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.FitPerformer.Utils.Options;
using Couchbase.Grpc.Protocol.Sdk.Search;
using Couchbase.Grpc.Protocol.Shared;
using Couchbase.KeyValue;
using Couchbase.Search;
using Couchbase.FitPerformer.Utils;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BooleanFieldQuery = Couchbase.Search.Queries.Simple.BooleanFieldQuery;
using BooleanQuery = Couchbase.Search.Queries.Compound.BooleanQuery;
using ConjunctionQuery = Couchbase.Search.Queries.Compound.ConjunctionQuery;
using DateRangeQuery = Couchbase.Search.Queries.Range.DateRangeQuery;
using DisjunctionQuery = Couchbase.Search.Queries.Compound.DisjunctionQuery;
using DocIdQuery = Couchbase.Search.Queries.Simple.DocIdQuery;
using GeoBoundingBoxQuery = Couchbase.Search.Queries.Geo.GeoBoundingBoxQuery;
using GeoDistanceQuery = Couchbase.Search.Queries.Geo.GeoDistanceQuery;
using MatchAllQuery = Couchbase.Search.Queries.Simple.MatchAllQuery;
using MatchNoneQuery = Couchbase.Search.Queries.Simple.MatchNoneQuery;
using MatchPhraseQuery = Couchbase.Search.Queries.Simple.MatchPhraseQuery;
using MatchQuery = Couchbase.Search.Queries.Simple.MatchQuery;
using NumericRangeQuery = Couchbase.Search.Queries.Range.NumericRangeQuery;
using PhraseQuery = Couchbase.Search.Queries.Simple.PhraseQuery;
using PrefixQuery = Couchbase.Search.Queries.Simple.PrefixQuery;
using QueryStringQuery = Couchbase.Search.Queries.Simple.QueryStringQuery;
using RegexpQuery = Couchbase.Search.Queries.Simple.RegexpQuery;
using SearchMetrics = Couchbase.Grpc.Protocol.Sdk.Search.SearchMetrics;
using TermQuery = Couchbase.Search.Queries.Simple.TermQuery;
using TermRangeQuery = Couchbase.Search.Queries.Range.TermRangeQuery;
using WildcardQuery = Couchbase.Search.Queries.Simple.WildcardQuery;
using VectorQueryOptions = Couchbase.Search.Queries.Vector.VectorQueryOptions;
using VectorSearch = Couchbase.Search.Queries.Vector.VectorSearch;
using VectorSearchOptions = Couchbase.Search.Queries.Vector.VectorSearchOptions;
using CoreSearch = Couchbase.Search;
using ProtoSearch = Couchbase.Grpc.Protocol.Sdk.Search;

namespace Couchbase.FitPerformer.Workload;
#nullable enable

public class SearchHelper
{
    public static async Task ExecuteSearchQuery(Couchbase.Grpc.Protocol.Sdk.Search.Search searchRequest, ConcurrentDictionary<string, IRequestSpan> spans, Couchbase.Grpc.Protocol.Run.Result result, ICluster cluster)
    {
        var options = SearchUtil.ConvertOptions(searchRequest.Options, spans);
        var searchQuery = ProtoSearchQueryToCore(searchRequest.Query);

        var response = await cluster.SearchQueryAsync(searchRequest.IndexName, searchQuery, options).ConfigureAwait(false);
        //Parse response

        result.Sdk = new();
        result.Sdk.SearchBlockingResult = new BlockingSearchResult();
        result.Sdk.SearchBlockingResult.Facets = new SearchFacets();
        result.Sdk.SearchBlockingResult.Facets.Facets.Add(CoreFacetToProto(response.Facets));
        result.Sdk.SearchBlockingResult.MetaData = CoreMetadataToProto(response.MetaData);
        result.Sdk.SearchBlockingResult.Rows.AddRange(CoreRowToProto(response.Hits, searchRequest.FieldsAs));
    }

    public static async Task ExecuteSearchV2Query(
        SearchWrapper searchWrapper,
        ConcurrentDictionary<string, IRequestSpan> spans,
        Couchbase.Grpc.Protocol.Run.Result result,
        ICluster cluster,
        IScope scope = null)
    {
        CoreSearch.Queries.Vector.VectorSearch? vectorSearch = ProtoVectorToCoreVector(searchWrapper.Search.Request);
        CoreSearch.ISearchQuery? ftsQuery = null;
        if (searchWrapper.Search.Request.SearchQuery is not null)
        {
            ftsQuery = ProtoSearchQueryToCore(searchWrapper.Search.Request.SearchQuery);
        }

        CoreSearch.SearchRequest coreSearchRequest = new(
            SearchQuery: ftsQuery,
            VectorSearch: vectorSearch)
        {
            Scope = scope
        };

        var coreSearchOptions = SearchUtil.ConvertOptions(searchWrapper.Search.Options, spans);
        result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
        var sw = Stopwatch.StartNew();
        var coreSearchResult = await cluster.SearchAsync(
            searchIndexName: searchWrapper.Search.IndexName,
            searchRequest: coreSearchRequest,
            options: coreSearchOptions).ConfigureAwait(false);
        sw.Stop();
        result.ElapsedNanos = sw.Elapsed.CalculateNanos();

        result.Sdk = new();
        result.Sdk.SearchBlockingResult = new BlockingSearchResult();
        result.Sdk.SearchBlockingResult.Facets = new SearchFacets();
        result.Sdk.SearchBlockingResult.Facets.Facets.Add(CoreFacetToProto(coreSearchResult.Facets));
        result.Sdk.SearchBlockingResult.MetaData = CoreMetadataToProto(coreSearchResult.MetaData);
        foreach (var coreRow in coreSearchResult)
        {
            var protoRow = ConvertProtoRow(searchWrapper.FieldsAs, coreRow);
            result.Sdk.SearchBlockingResult.Rows.Add(protoRow);
        }
    }

    private static VectorSearch? ProtoVectorToCoreVector(ProtoSearch.SearchRequest searchRequest)
    {
        var fitVectorSearch = searchRequest.VectorSearch;
        if (fitVectorSearch is null)
        {
            return null;
        }

        var sdkVectorQueries = ProtoVectorQueryToCore(fitVectorSearch);

        var vectorSearchOptions = new VectorSearchOptions();
        if (fitVectorSearch.Options?.HasVectorQueryCombination == true)
        {
            vectorSearchOptions = vectorSearchOptions with
            {
                VectoryQueryCombination = fitVectorSearch.Options.VectorQueryCombination switch
                {
                    VectorQueryCombination.And => CoreSearch.Queries.Vector.VectorQueryCombination.And,
                    VectorQueryCombination.Or => CoreSearch.Queries.Vector.VectorQueryCombination.Or,
                    _ => throw new NotSupportedException(
                        $"Unsupported {nameof(VectorQueryCombination)} value: {fitVectorSearch.Options.VectorQueryCombination}")
                }
            };
        }

        var sdkVectorSearch = new VectorSearch(
            VectorQueries: sdkVectorQueries,
            Options: vectorSearchOptions);
        return sdkVectorSearch;
    }

    private static List<CoreSearch.Queries.Vector.VectorQuery> ProtoVectorQueryToCore(ProtoSearch.VectorSearch fitVectorSearch)
    {

        return (
            from vq
                in fitVectorSearch.VectorQuery
            select vq.HasBase64VectorQuery
                ? CoreSearch.Queries.Vector.VectorQuery.Create(
                vectorFieldName: vq.VectorFieldName,
                base64EncodedVector: vq.Base64VectorQuery,
                options: new VectorQueryOptions
                {
                    Boost = vq.Options?.HasBoost == true ? vq.Options.Boost : null,
                    NumCandidates = vq.Options?.HasNumCandidates == true ? (uint)vq.Options.NumCandidates : (uint?)null,
                    Filter = vq.Options?.Prefilter.IsInitialized() == true ? ProtoSearchQueryToCore(vq.Options.Prefilter) : null,
                })
                : CoreSearch.Queries.Vector.VectorQuery.Create(
                    vectorFieldName: vq.VectorFieldName,
                    vector: vq.VectorQuery_.ToArray(),
                    options: new VectorQueryOptions
                    {
                        Boost = vq.Options?.HasBoost == true ? vq.Options.Boost : null,
                        NumCandidates = vq.Options?.HasNumCandidates == true ? (uint)vq.Options.NumCandidates : (uint?)null,
                        Filter = vq.Options?.Prefilter.IsInitialized() == true ? ProtoSearchQueryToCore(vq.Options.Prefilter) : null,
                    }
                )).ToList();
    }


    private static RepeatedField<Couchbase.Grpc.Protocol.Sdk.Search.SearchRow> CoreRowToProto(IList<ISearchQueryRow> coreHits, ContentAs contentAs)
    {
        var protoRows = new RepeatedField<SearchRow>();
        foreach (var coreHit in coreHits)
        {
            var protoRow = ConvertProtoRow(contentAs, coreHit);
            protoRows.Add(protoRow);
        }
        return protoRows;
    }

    private static SearchRow ConvertProtoRow(ContentAs contentAs, ISearchQueryRow coreHit)
    {
        var protoRow = new SearchRow
        {
            Index = coreHit.Index,
            Id = coreHit.Id,
            Score = coreHit.Score,
        };

        if (coreHit.Explanation is not null)
        {
            protoRow.Explanation = ByteString.CopyFromUtf8(coreHit.Explanation.ToString());
        }

        if (coreHit.Locations is not null)
        {
            foreach (var coreLocation in (JObject)coreHit.Locations)
            {
                var location = new SearchRowLocation();
                var element = ((JObject)coreLocation.Value!).Properties().First();
                var values = element.Value.First!;

                location.Field = coreLocation.Key;
                location.Term = element.Name;
                location.Start = (uint)values["start"]!;
                location.End = (uint)values["end"]!;
                location.Position = (uint)values["pos"]!;
                protoRow.Locations.Add(location);
            }
        }


        protoRow.Fields = new ContentTypes();


        if (coreHit.Fields is not null)
        {
            var jsonConverted = JsonConvert.SerializeObject(coreHit.Fields);
            switch (contentAs?.AsCase)
            {
                case ContentAs.AsOneofCase.AsBoolean:
                { }
                    break;
                case ContentAs.AsOneofCase.AsInteger:
                {}
                    break;
                case ContentAs.AsOneofCase.AsString:
                {
                    protoRow.Fields.ContentAsString = jsonConverted;
                }
                    break;
                case ContentAs.AsOneofCase.AsByteArray:
                {
                    protoRow.Fields.ContentAsBytes = ByteString.CopyFromUtf8(jsonConverted);
                }
                    break;
                case ContentAs.AsOneofCase.AsFloatingPoint:
                {}
                    break;
                case ContentAs.AsOneofCase.AsJsonArray:
                {}
                    break;
                case ContentAs.AsOneofCase.AsJsonObject:
                {
                    protoRow.Fields.ContentAsBytes = ByteString.CopyFromUtf8(jsonConverted);
                }
                    break;
            }
        }
        return protoRow;
    }

    private static SearchMetaData CoreMetadataToProto(Couchbase.Search.MetaData coreMetadata)
    {
        var protoMetadata = new SearchMetaData();
        protoMetadata.Metrics = new SearchMetrics
        {
            TookMsec = (long)coreMetadata.TimeTook.TotalMilliseconds,
            TotalRows = coreMetadata.TotalHits,
            MaxScore = coreMetadata.MaxScore,
            TotalPartitionCount = coreMetadata.TotalCount,
            SuccessPartitionCount = coreMetadata.SuccessCount,
            ErrorPartitionCount = coreMetadata.ErrorCount
        };
        foreach (var (key, value) in coreMetadata.Errors)
        {
            protoMetadata.Errors.Add(key, value);
        }
        return protoMetadata;
    }

    private static MapField<string, SearchFacetResult> CoreFacetToProto(IDictionary<string, IFacetResult> coreFacets)
    {
        var protoFacets = new MapField<string, SearchFacetResult>();
        foreach (var (name, facet) in coreFacets)
        {
            var protoFacet = new SearchFacetResult
            {
                Name = facet.Name,
                Field = facet.Field,
                Total = (ulong)facet.Total,
                Missing = (ulong)facet.Missing,
                Other = (ulong)facet.Other
            };
            protoFacets.Add(name, protoFacet);
        }

        return protoFacets;
    }

    private static Couchbase.Search.ISearchQuery ProtoSearchQueryToCore(Couchbase.Grpc.Protocol.Sdk.Search.SearchQuery protoQuery, SearchQuery? innerProtoQuery = null)
        {
            //If no specific query is passed then use the SearchRequest.
            //This allows to determine whether the function call is recursive or not (if null then not recursive)
            innerProtoQuery ??= protoQuery;

            switch (innerProtoQuery.QueryCase)
            {
                case SearchQuery.QueryOneofCase.Boolean:
                {
                    //--------Initialise BooleanQuery--------
                    var coreQuery = new BooleanQuery();
                    if (protoQuery.Boolean.HasBoost) coreQuery.Boost(protoQuery.Boolean.Boost);
                    if (protoQuery.Boolean.HasShouldMin) coreQuery.ShouldMin((int)protoQuery.Boolean.ShouldMin);

                    //--------Recurse to add converted queries to the above compound queries--------
                    if (innerProtoQuery.Boolean.Must.Count > 0)
                    {
                        foreach (var q in innerProtoQuery.Boolean.Must)
                        {
                            coreQuery.Must(ProtoSearchQueryToCore(protoQuery, q));
                        }
                    }

                    if (innerProtoQuery.Boolean.MustNot.Count > 0)
                    {
                        foreach (var q in innerProtoQuery.Boolean.MustNot)
                        {
                            coreQuery.MustNot(ProtoSearchQueryToCore(protoQuery, q));
                        }
                    }

                    if (innerProtoQuery.Boolean.Should.Count > 0)
                    {
                        foreach (var q in innerProtoQuery.Boolean.Should)
                        {
                            coreQuery.Should(ProtoSearchQueryToCore(protoQuery, q));
                        }
                    }
                    return coreQuery;
                }
                case SearchQuery.QueryOneofCase.Conjunction:
                {
                    var coreQuery = new ConjunctionQuery();
                    if (innerProtoQuery.Conjunction.HasBoost) coreQuery.Boost(innerProtoQuery.Conjunction.Boost);
                    if (innerProtoQuery.Conjunction.Conjuncts.Count > 0)
                    {
                        foreach (var q in innerProtoQuery.Conjunction.Conjuncts)
                        {
                            coreQuery.And(ProtoSearchQueryToCore(protoQuery, q));
                        }
                    }

                    return coreQuery;
                }
                case SearchQuery.QueryOneofCase.Disjunction:
                {
                    var coreQuery = new DisjunctionQuery();
                    if (innerProtoQuery.Disjunction.HasBoost) coreQuery.Boost(innerProtoQuery.Disjunction.Boost);
                    if (innerProtoQuery.Disjunction.HasMin) coreQuery.Min((int)innerProtoQuery.Disjunction.Min);
                    if (innerProtoQuery.Disjunction.Disjuncts.Count > 0)
                    {
                        foreach (var q in innerProtoQuery.Disjunction.Disjuncts)
                        {
                            coreQuery.Or(ProtoSearchQueryToCore(protoQuery, q));
                        }
                    }

                    return coreQuery;
                }
                case SearchQuery.QueryOneofCase.Match:
                {
                    var coreQuery = new MatchQuery(innerProtoQuery.Match.Match);
                    if (innerProtoQuery.Match.HasBoost) coreQuery.Boost(innerProtoQuery.Match.Boost);
                    if (innerProtoQuery.Match.HasAnalyzer) coreQuery.Analyzer(innerProtoQuery.Match.Analyzer);
                    if (innerProtoQuery.Match.HasField) coreQuery.Field(innerProtoQuery.Match.Field);
                    if (innerProtoQuery.Match.HasFuzziness) coreQuery.Fuzziness((int)innerProtoQuery.Match.Fuzziness);
                    if (innerProtoQuery.Match.HasOperator) coreQuery.MatchOperator(innerProtoQuery.Match.Operator.ToCore());
                    if (innerProtoQuery.Match.HasPrefixLength) coreQuery.PrefixLength((int)innerProtoQuery.Match.PrefixLength);
                    return coreQuery;
                }
                case SearchQuery.QueryOneofCase.Phrase:
                {
                    var coreQuery = new PhraseQuery(innerProtoQuery.Phrase.Terms.ToArray());
                    if (innerProtoQuery.Phrase.HasBoost) coreQuery.Boost(innerProtoQuery.Phrase.Boost);
                    if (innerProtoQuery.Phrase.HasField) coreQuery.Field(innerProtoQuery.Phrase.Field);
                    return coreQuery;
                }
                case SearchQuery.QueryOneofCase.Prefix:
                {
                    var coreQuery = new PrefixQuery(innerProtoQuery.Prefix.Prefix);
                    if (innerProtoQuery.Prefix.HasBoost) coreQuery.Boost(innerProtoQuery.Prefix.Boost);
                    if (innerProtoQuery.Prefix.HasField) coreQuery.Field(innerProtoQuery.Prefix.Field);
                    return coreQuery;
                }
                case SearchQuery.QueryOneofCase.Regexp:
                {
                    var coreQuery = new RegexpQuery(innerProtoQuery.Regexp.Regexp);
                    if (innerProtoQuery.Regexp.HasBoost) coreQuery.Boost(innerProtoQuery.Regexp.Boost);
                    if (innerProtoQuery.Regexp.HasField) coreQuery.Field(innerProtoQuery.Regexp.Field);
                    coreQuery.Field(innerProtoQuery.Regexp.Field);
                    return coreQuery;
                }
                case SearchQuery.QueryOneofCase.Term:
                {
                    var coreQuery = new TermQuery(innerProtoQuery.Term.Term);
                    if (innerProtoQuery.Term.HasBoost) coreQuery.Boost(innerProtoQuery.Term.Boost);
                    if (innerProtoQuery.Term.HasField) coreQuery.Field(innerProtoQuery.Term.Field);
                    if (innerProtoQuery.Term.HasPrefixLength) coreQuery.PrefixLength((int)innerProtoQuery.Term.PrefixLength);
                    if (innerProtoQuery.Term.HasFuzziness) coreQuery.Fuzziness((int)innerProtoQuery.Term.Fuzziness);
                    return coreQuery;
                }
                case SearchQuery.QueryOneofCase.Wildcard:
                {
                    var coreQuery = new WildcardQuery(innerProtoQuery.Wildcard.Wildcard);
                    if (innerProtoQuery.Wildcard.HasBoost) coreQuery.Boost(innerProtoQuery.Wildcard.Boost);
                    if (innerProtoQuery.Wildcard.HasField) coreQuery.Field(innerProtoQuery.Wildcard.Field);
                    return coreQuery;
                }
                case SearchQuery.QueryOneofCase.DateRange:
                {
                    var coreQuery = new DateRangeQuery();
                    if (innerProtoQuery.DateRange.HasBoost) coreQuery.Boost(innerProtoQuery.DateRange.Boost);
                    if (innerProtoQuery.DateRange.HasField) coreQuery.Field(innerProtoQuery.DateRange.Field);

                    if (innerProtoQuery.DateRange.HasInclusiveStart)
                    {
                        if (innerProtoQuery.DateRange.HasStart) coreQuery.Start(DateTime.Parse(innerProtoQuery.DateRange.Start), innerProtoQuery.DateRange.InclusiveStart);
                    }
                    else
                    {
                        if (innerProtoQuery.DateRange.HasStart)
                            coreQuery.Start(DateTime.Parse(innerProtoQuery.DateRange.Start));
                    }

                    if (innerProtoQuery.DateRange.HasInclusiveEnd)
                    {
                        if (innerProtoQuery.DateRange.HasEnd) coreQuery.End(DateTime.Parse(innerProtoQuery.DateRange.End), innerProtoQuery.DateRange.InclusiveEnd);
                    }
                    else
                    {
                        if (innerProtoQuery.DateRange.HasEnd) coreQuery.End(DateTime.Parse(innerProtoQuery.DateRange.End));
                    }
                    if (innerProtoQuery.DateRange.HasDatetimeParser) coreQuery.Parser(innerProtoQuery.DateRange.DatetimeParser);

                    return coreQuery;
                }
                case SearchQuery.QueryOneofCase.DocId:
                {
                    var coreQuery = new DocIdQuery(innerProtoQuery.DocId.Ids.ToArray());
                    if (innerProtoQuery.DocId.HasBoost) coreQuery.Boost(innerProtoQuery.DocId.Boost);
                    return coreQuery;
                }
                case SearchQuery.QueryOneofCase.GeoDistance:
                {
                    var coreQuery = new GeoDistanceQuery();
                    if (innerProtoQuery.GeoDistance.HasBoost) coreQuery.Boost(innerProtoQuery.GeoDistance.Boost);
                    if (innerProtoQuery.GeoDistance.HasField) coreQuery.Field(innerProtoQuery.GeoDistance.Field);
                    coreQuery.Distance(innerProtoQuery.GeoDistance.Distance);
                    coreQuery.Latitude(innerProtoQuery.GeoDistance.Location.Lat);
                    coreQuery.Longitude(innerProtoQuery.GeoDistance.Location.Lon);
                    return coreQuery;
                }
                case SearchQuery.QueryOneofCase.MatchAll:
                {
                    return new MatchAllQuery();
                }
                case SearchQuery.QueryOneofCase.MatchNone:
                {
                    return new MatchNoneQuery();
                }
                case SearchQuery.QueryOneofCase.MatchPhrase:
                {
                    var coreQuery = new MatchPhraseQuery(innerProtoQuery.MatchPhrase.MatchPhrase);
                    if (innerProtoQuery.MatchPhrase.HasBoost) coreQuery.Boost(innerProtoQuery.MatchPhrase.Boost);
                    if (innerProtoQuery.MatchPhrase.HasField) coreQuery.Field(innerProtoQuery.MatchPhrase.Field);
                    if (innerProtoQuery.MatchPhrase.HasAnalyzer) coreQuery.Analyzer(innerProtoQuery.MatchPhrase.Analyzer);
                    return coreQuery;
                }
                case SearchQuery.QueryOneofCase.NumericRange:
                {
                    var coreQuery = new NumericRangeQuery();
                    if (innerProtoQuery.NumericRange.HasBoost) coreQuery.Boost(innerProtoQuery.NumericRange.Boost);
                    if (innerProtoQuery.NumericRange.HasField) coreQuery.Field(innerProtoQuery.NumericRange.Field);
                    if (innerProtoQuery.NumericRange.HasInclusiveMin)
                    {
                        if (innerProtoQuery.NumericRange.HasMin) coreQuery.Min(innerProtoQuery.NumericRange.Min, innerProtoQuery.NumericRange.InclusiveMin);
                    }
                    else
                    {
                        if (innerProtoQuery.NumericRange.HasMin) coreQuery.Min(innerProtoQuery.NumericRange.Min);
                    }

                    if (innerProtoQuery.NumericRange.HasInclusiveMax)
                    {
                        if (innerProtoQuery.NumericRange.HasMax) coreQuery.Max(innerProtoQuery.NumericRange.Max, innerProtoQuery.NumericRange.InclusiveMax);
                    }
                    else
                    {
                        if (innerProtoQuery.NumericRange.HasInclusiveMax) coreQuery.Max(innerProtoQuery.NumericRange.Max);
                    }

                    return coreQuery;
                }
                case SearchQuery.QueryOneofCase.QueryString:
                {
                    var coreQuery = new QueryStringQuery(innerProtoQuery.QueryString.Query);
                    if (innerProtoQuery.QueryString.HasBoost) coreQuery.Boost(innerProtoQuery.QueryString.Boost);
                    return coreQuery;
                }
                case SearchQuery.QueryOneofCase.TermRange:
                {
                    var coreQuery = new TermRangeQuery();
                    if (innerProtoQuery.TermRange.HasBoost) coreQuery.Boost(innerProtoQuery.TermRange.Boost);
                    if (innerProtoQuery.TermRange.HasField) coreQuery.Field(innerProtoQuery.TermRange.Field);
                    if (innerProtoQuery.TermRange.HasInclusiveMin)
                    {
                        if (innerProtoQuery.TermRange.HasMin) coreQuery.Min(innerProtoQuery.TermRange.Min, innerProtoQuery.TermRange.InclusiveMin);
                    }
                    else
                    {
                        if (innerProtoQuery.TermRange.HasMin) coreQuery.Min(innerProtoQuery.TermRange.Min);
                    }

                    if (innerProtoQuery.TermRange.HasInclusiveMax)
                    {
                        if (innerProtoQuery.TermRange.HasMax) coreQuery.Max(innerProtoQuery.TermRange.Max, innerProtoQuery.TermRange.InclusiveMax);
                    }
                    else
                    {
                        if (innerProtoQuery.TermRange.HasInclusiveMax) coreQuery.Max(innerProtoQuery.TermRange.Max);
                    }

                    return coreQuery;
                }
                case SearchQuery.QueryOneofCase.GeoBoundingBox:
                {
                    var coreQuery = new GeoBoundingBoxQuery();
                    if (innerProtoQuery.GeoBoundingBox.HasBoost) coreQuery.Boost(innerProtoQuery.GeoBoundingBox.Boost);
                    if (innerProtoQuery.GeoBoundingBox.HasField) coreQuery.Field(innerProtoQuery.GeoBoundingBox.Field);
                    coreQuery.BottomRight(
                        innerProtoQuery.GeoBoundingBox.BottomRight.Lon,
                        innerProtoQuery.GeoBoundingBox.BottomRight.Lat);
                    coreQuery.TopLeft(
                        innerProtoQuery.GeoBoundingBox.TopLeft.Lon,
                        innerProtoQuery.GeoBoundingBox.TopLeft.Lat);
                    return coreQuery;
                }
                case SearchQuery.QueryOneofCase.SearchBooleanField:
                {
                    var coreQuery = new BooleanFieldQuery(innerProtoQuery.SearchBooleanField.Bool);
                    if (innerProtoQuery.SearchBooleanField.HasBoost) coreQuery.Boost(innerProtoQuery.SearchBooleanField.Boost);
                    if (innerProtoQuery.SearchBooleanField.HasField) coreQuery.Field(innerProtoQuery.SearchBooleanField.Field);
                    return coreQuery;
                }
            }

            throw new UnsupportedException($"Could not determine Search Query type: {protoQuery}");
        }
}