using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Grpc.Protocol.Sdk.Search;
using Couchbase.Query;
using Couchbase.Search;
using Couchbase.Search.Sort;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DateRangeFacet = Couchbase.Search.DateRangeFacet;
using JsonSerializer = System.Text.Json.JsonSerializer;
using NumericRangeFacet = Couchbase.Search.NumericRangeFacet;
using SearchFacet = Couchbase.Grpc.Protocol.Sdk.Search.SearchFacet;
using SearchOptions = Couchbase.Search.SearchOptions;

namespace Couchbase.FitPerformer.Utils.Options;

public static class SearchUtil
{
    public static SearchOptions ConvertOptions(Grpc.Protocol.Sdk.Search.SearchOptions? protoOptions, ConcurrentDictionary<string, IRequestSpan> spans, string? scopeName = null)
        {
            var ret = new SearchOptions();
            if (scopeName is not null) ret.Scope(scopeName);
            if (protoOptions != null)
            {
                if (protoOptions.HasParentSpanId) ret.RequestSpan(spans[protoOptions.ParentSpanId]);
                if (protoOptions.HasTimeoutMillis) ret.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMillis));
                if (protoOptions.HasScanConsistency) ret.ScanConsistency(protoOptions.ScanConsistency.ToCore());
                if (protoOptions.HasExplain) ret.Explain(protoOptions.Explain);
                if (protoOptions.HasLimit) ret.Limit((int)protoOptions.Limit);
                if (protoOptions.HasSkip) ret.Skip((int)protoOptions.Skip);
                if (protoOptions.HasIncludeLocations) ret.IncludeLocations(protoOptions.IncludeLocations);
                if (protoOptions.Facets.Count > 0) ret.Facets(SearchFacetToCore(protoOptions.Facets));
                if (protoOptions.Fields.Count > 0) ret.Fields(protoOptions.Fields.ToArray());
                if (protoOptions.Highlight?.HasStyle == true)
                {
                    if (protoOptions.Highlight.Fields.Count > 0)
                    {
                        ret.Highlight(protoOptions.Highlight.Style.ToCore(), protoOptions.Highlight.Fields.ToArray());
                    }
                    else
                    {
                        ret.Highlight(protoOptions.Highlight.Style.ToCore());
                    }

                }

                if (protoOptions.Sort?.Count > 0)
                {
                    foreach (var sort in protoOptions.Sort)
                    {
                        // TODO: these need filling out
                        switch (sort.SortCase)
                        {
                            case SearchSort.SortOneofCase.Field:
                            {
                                ret.Sort(JObject.Parse(sort.Field.ToString())); //TODO: Will this work?
                            }
                                break;
                            case SearchSort.SortOneofCase.Id:
                            {
                                ret.Sort(JObject.FromObject(new { by = "id" }));
                            }
                                break;
                            case SearchSort.SortOneofCase.Raw:
                            {
                                ret.Sort(JObject.Parse(sort.Raw.ToString()));
                            }
                                break;
                            case SearchSort.SortOneofCase.Score:
                            {
                                var descending = false;
                                if (sort.Score.HasDesc)
                                {
                                    descending = sort.Score.Desc;
                                }

                                ret.Sort(new ScoreSearchSort(decending: descending));
                            }
                                break;
                            case SearchSort.SortOneofCase.GeoDistance:
                            {
                                ret.Sort(JObject.Parse(sort.GeoDistance.ToString()));
                            }
                                break;
                            default:
                                throw new InvalidArgumentException("Could not parse Sort.");
                        }
                    }
                }

                _ = protoOptions.Raw.Select(kvp => ret.Raw(kvp.Key, kvp.Value));
                if (protoOptions.ConsistentWith?.Tokens?.Count > 0) ret.ConsistentWith(OptionsUtil.ConvertMutationState(protoOptions.ConsistentWith));
            }

            return ret;
        }

        public static ISearchFacet[] SearchFacetToCore(MapField<string, SearchFacet> protoFacets)
        {
            var ret = new List<ISearchFacet>(protoFacets.Count);
            foreach (var kvp in protoFacets)
            {
                var protoFacet = kvp.Value;

                switch (protoFacet.FacetCase)
                {
                    case Grpc.Protocol.Sdk.Search.SearchFacet.FacetOneofCase.Term:
                    {
                        var coreTermFacet = new Search.TermFacet()
                        {
                            Field = protoFacet.Term.Field,
                            Name = kvp.Key
                        };
                        if (protoFacet.Term.HasSize) coreTermFacet.Size = (int)protoFacet.Term.Size;
                         ret.Add(coreTermFacet);
                    }
                        break;
                    case Grpc.Protocol.Sdk.Search.SearchFacet.FacetOneofCase.DateRange:
                    {
                        var coreDateRangeFacet = new DateRangeFacet
                        {
                            Field = protoFacet.DateRange.Field,
                            Name = kvp.Key
                        };
                        if (protoFacet.DateRange.HasSize) coreDateRangeFacet.Size = (int)protoFacet.DateRange.Size;
                        if (protoFacet.DateRange.DateRanges.Count > 0)
                            _ = protoFacet.DateRange.DateRanges.Select(range =>
                                coreDateRangeFacet.AddRange(new Range<DateTime>
                                {
                                    Name = range.Name, Start = range.Start.ToDateTime(), End = range.End.ToDateTime()
                                }));
                        ret.Add(coreDateRangeFacet);
                    }
                        break;
                    case SearchFacet.FacetOneofCase.NumericRange:
                    {
                        var coreNumericRangeFacet = new NumericRangeFacet()
                        {
                            Field = protoFacet.NumericRange.Field,
                            Name = kvp.Key
                        };
                        if (protoFacet.NumericRange.HasSize)
                            coreNumericRangeFacet.Size = (int)protoFacet.NumericRange.Size;
                        if (protoFacet.NumericRange.NumericRanges.Count > 0)
                            _ = protoFacet.NumericRange.NumericRanges.Select(range =>
                                coreNumericRangeFacet.AddRange(new Range<float>
                                    { Name = range.Name, Start = range.Min, End = range.Max }));

                        ret.Add(coreNumericRangeFacet);
                    }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            $"Provided Facet {protoFacet.FacetCase} could not be parsed to Core facet.");
                }
            }

            return ret.ToArray();
        }
}