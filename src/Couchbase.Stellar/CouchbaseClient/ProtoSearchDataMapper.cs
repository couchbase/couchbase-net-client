using System.Collections.ObjectModel;
using Couchbase.Protostellar.Search.V1;
using Couchbase.Search;
using Google.Protobuf.Collections;
using Grpc.Core;
using DateRange = Couchbase.Search.DateRange;
using NumericRange = Couchbase.Search.NumericRange;

namespace Couchbase.Stellar.CouchbaseClient;

public class ProtoSearchDataMapper
{
    public async ValueTask<ISearchResult> MapAsync(IAsyncStreamReader<SearchQueryResponse> stream, CancellationToken cancellationToken = default)
    {
        var response = new ProtoSearchResult();
        response.Hits = new List<ISearchQueryRow>();

        while (await stream.MoveNext(cancellationToken).ConfigureAwait(false))
        {
            if (stream.Current.MetaData != null && stream.Current.MetaData.Metrics != null) response.MetaData = ParseMetaData(stream.Current.MetaData);
            if (stream.Current.Facets != null) response.Facets = stream.Current.Facets.ToDictionary(kvp => kvp.Key, kvp => ParseFacetResult(kvp.Value));

            foreach (var searchHit in stream.Current.Hits)
            {
                if (cancellationToken.IsCancellationRequested || searchHit == null) break;

                //NOT streaming. Return 1 SearchResult object, containing multiple rows (hits).
                response.Hits.Add(ParseSearchQueryRow(searchHit));
            }
        }
        return response;
    }

    public static IFacetResult ParseFacetResult(SearchQueryResponse.Types.FacetResult protoFacet)
    {
        switch (protoFacet.SearchFacetCase)
        {
            case SearchQueryResponse.Types.FacetResult.SearchFacetOneofCase.TermFacet:
            {
                return new TermFacetResult
                {
                    // Name = ??, protoFacet doesn't have a Name field
                    Field = protoFacet.TermFacet.Field,
                    Total = protoFacet.TermFacet.Total,
                    Missing = protoFacet.TermFacet.Missing,
                    Other = protoFacet.TermFacet.Other,
                    Terms = ParseTerms(protoFacet.TermFacet.Terms)
                };
            }
            case SearchQueryResponse.Types.FacetResult.SearchFacetOneofCase.DateRangeFacet:
            {
                return new DateRangeFacetResult
                {
                    // Name = ??,
                    Field = protoFacet.TermFacet.Field,
                    Total = protoFacet.TermFacet.Total,
                    Missing = protoFacet.TermFacet.Missing,
                    Other = protoFacet.TermFacet.Other,
                    DateRanges = ParseDateRanges(protoFacet.DateRangeFacet.DateRanges)
                };
            }
            case SearchQueryResponse.Types.FacetResult.SearchFacetOneofCase.NumericRangeFacet:
            {
                return new NumericRangeFacetResult
                {
                    // Name = ??,
                    Field = protoFacet.TermFacet.Field,
                    Total = protoFacet.TermFacet.Total,
                    Missing = protoFacet.TermFacet.Missing,
                    Other = protoFacet.TermFacet.Other,
                    NumericRanges = ParseNumericRanges(protoFacet.NumericRangeFacet.NumericRanges)
                };
            }
        }
        throw new ArgumentOutOfRangeException($"Provided FacetResult {protoFacet.SearchFacetCase} could not be parsed.");
    }

    private static ReadOnlyCollection<Term> ParseTerms(RepeatedField<SearchQueryResponse.Types.TermResult> protoTerms)
    {
        //TODO: SDK doesn't have "Field" property for Term, probably because the terms come back ordered?
        //If this is the case we should try and sort them according to how they were sent with the request
        IList<Term> coreTerms = new List<Term>();
        foreach (var protoTerm in protoTerms)
        {
            // protoTerm.Field - what is this?
            coreTerms.Add(new Term
            {
                Name = protoTerm.Name,
                Count = (long)protoTerm.Size
            });
        }
        return new ReadOnlyCollection<Term>(coreTerms);
    }

    private static ReadOnlyCollection<DateRange> ParseDateRanges(RepeatedField<SearchQueryResponse.Types.DateRangeResult> protoDateRanges)
    {
        IList<DateRange> coreTerms = new List<DateRange>();
        foreach (var protoTerm in protoDateRanges)
        {
            coreTerms.Add(new DateRange
            {
                Name = protoTerm.Name,
                Count = (long)protoTerm.Size,
                End = protoTerm.End.ToDateTime(),
                Start = protoTerm.Start.ToDateTime()
            });
        }
        return new ReadOnlyCollection<DateRange>(coreTerms);
    }

    private static ReadOnlyCollection<NumericRange> ParseNumericRanges(
        RepeatedField<SearchQueryResponse.Types.NumericRangeResult> protoNumericRanges)
    {
        IList<NumericRange> coreTerms = new List<NumericRange>();
        foreach (var protoTerm in protoNumericRanges)
        {
            coreTerms.Add(new NumericRange
            {
                Name = protoTerm.Name,
                Min = protoTerm.Min,
                Max = protoTerm.Max,
                Count = (long)protoTerm.Size
            });
        }
        return new ReadOnlyCollection<NumericRange>(coreTerms);
    }

    private static Couchbase.Search.MetaData ParseMetaData(SearchQueryResponse.Types.MetaData metadata)
    {
        var coreMetadata = new Couchbase.Search.MetaData
        {
            SuccessCount = (long)metadata.Metrics.SuccessPartitionCount,
            ErrorCount = (long)metadata.Metrics.ErrorPartitionCount,
            TotalHits = (long)metadata.Metrics.TotalRows,
            MaxScore = metadata.Metrics.MaxScore,
            TotalCount = (long)metadata.Metrics.TotalPartitionCount
        };
        if (metadata.Metrics.ExecutionTime != null) coreMetadata.TimeTook = metadata.Metrics.ExecutionTime.ToTimeSpan();
        if (metadata.Errors != null) coreMetadata.Errors = metadata.Errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        return coreMetadata;
    }

    private static ISearchQueryRow ParseSearchQueryRow(SearchQueryResponse.Types.SearchQueryRow protoSearchRow)
    {
        var coreRow = new ProtoSearchQueryRow();
        coreRow.Score = protoSearchRow.Score;
        if (protoSearchRow.Explanation != null) coreRow.Explanation = protoSearchRow.Explanation.ToStringUtf8();
        if (protoSearchRow.Id != null) coreRow.Id = protoSearchRow.Id;
        if (protoSearchRow.Index != null) coreRow.Index = protoSearchRow.Index;
        if (protoSearchRow.Fields != null) coreRow.Fields = protoSearchRow.Fields.ToDictionary(kvp => kvp.Key, kvp => (dynamic)kvp.Value.ToStringUtf8()); //TODO: Verify this cast
        if (protoSearchRow.Fragments != null) coreRow.Fragments = protoSearchRow.Fragments.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Content.ToList());
        if (protoSearchRow.Locations != null) coreRow.Locations = protoSearchRow.Locations.ToList(); //SDK does not have a "Location" object, should we return the grpc class?
        return coreRow;
    }
}
