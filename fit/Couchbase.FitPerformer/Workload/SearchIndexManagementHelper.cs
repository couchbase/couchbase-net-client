using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Couchbase.FitPerformer.Utils.Options;
using Couchbase.FitPerformer.Utils.Results;
using Couchbase.KeyValue;
using Couchbase.Management.Search;
using Couchbase.FitPerformer.Utils;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.FitPerformer.Workload;

public static class SearchIndexManagementHelper
{
    public static async Task RunSharedSearchIndexManagementCommand(Couchbase.Grpc.Protocol.Sdk.Search.IndexManager.Command opSearchIndex, ISearchIndexManager indexManager, Couchbase.Grpc.Protocol.Run.Result result, IScope? scope = null)
    {
        switch (opSearchIndex.CommandCase)
        {
            case Grpc.Protocol.Sdk.Search.IndexManager.Command.CommandOneofCase.AllowQuerying:
            {
                var request = opSearchIndex.AllowQuerying;
                var options = OptionsUtil.ConvertSearchIndexManagementOptions(request.Options);
                result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                var sw = Stopwatch.StartNew();
                if (scope is null) await indexManager.AllowQueryingAsync(request.IndexName, options).ConfigureAwait(false);
                else await indexManager.AllowQueryingAsync(request.IndexName, options, scope).ConfigureAwait(false);
                sw.Stop();
                result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                ResultsUtil.SetSuccess(result);
            }
                break;
            case Grpc.Protocol.Sdk.Search.IndexManager.Command.CommandOneofCase.AnalyzeDocument:
            {
                throw new NotSupportedException("The .NET SDK has not implemented this API");
            }
                break;
            case Grpc.Protocol.Sdk.Search.IndexManager.Command.CommandOneofCase.DisallowQuerying:
            {
                var request = opSearchIndex.DisallowQuerying;
                var options = OptionsUtil.ConvertSearchIndexManagementOptions(request.Options);
                result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                var sw = Stopwatch.StartNew();
                if (scope is null) await indexManager.DisallowQueryingAsync(request.IndexName, options).ConfigureAwait(false);
                else await indexManager.DisallowQueryingAsync(request.IndexName, options, scope).ConfigureAwait(false);
                sw.Stop();
                result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                ResultsUtil.SetSuccess(result);
            }
                break;
            case Grpc.Protocol.Sdk.Search.IndexManager.Command.CommandOneofCase.DropIndex:
            {
                var request = opSearchIndex.DropIndex;
                var options = OptionsUtil.ConvertSearchIndexManagementOptions(request.Options);
                result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                var sw = Stopwatch.StartNew();
                if (scope is null) await indexManager.DropIndexAsync(request.IndexName, options).ConfigureAwait(false);
                else await indexManager.DropIndexAsync(request.IndexName, options, scope).ConfigureAwait(false);
                sw.Stop();
                result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                ResultsUtil.SetSuccess(result);
            }
                break;
            case Grpc.Protocol.Sdk.Search.IndexManager.Command.CommandOneofCase.FreezePlan:
            {
                var request = opSearchIndex.FreezePlan;
                var options = OptionsUtil.ConvertSearchIndexManagementOptions(request.Options);
                result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                var sw = Stopwatch.StartNew();
                if (scope is null) await indexManager.FreezePlanAsync(request.IndexName, options).ConfigureAwait(false);
                else await indexManager.FreezePlanAsync(request.IndexName, options, scope).ConfigureAwait(false);
                sw.Stop();
                result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                ResultsUtil.SetSuccess(result);
            }
                break;
            case Grpc.Protocol.Sdk.Search.IndexManager.Command.CommandOneofCase.UnfreezePlan:
            {
                var request = opSearchIndex.UnfreezePlan;
                var options = OptionsUtil.ConvertSearchIndexManagementOptions(request.Options);
                result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                var sw = Stopwatch.StartNew();
                if (scope is null) await indexManager.UnfreezePlanAsync(request.IndexName, options).ConfigureAwait(false);
                else await indexManager.UnfreezePlanAsync(request.IndexName, options, scope).ConfigureAwait(false);
                sw.Stop();
                result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                ResultsUtil.SetSuccess(result);
            }
                break;
            case Grpc.Protocol.Sdk.Search.IndexManager.Command.CommandOneofCase.GetIndex:
            {
                var request = opSearchIndex.GetIndex;
                var options = OptionsUtil.ConvertSearchIndexManagementOptions(request.Options);
                result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                var sw = Stopwatch.StartNew();
                SearchIndex response = null;
                if (scope is null)
                    response = await indexManager.GetIndexAsync(request.IndexName, options).ConfigureAwait(false);
                else response = await indexManager.GetIndexAsync(request.IndexName, options, scope).ConfigureAwait(false);
                sw.Stop();
                result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                ResultsUtil.PopulateResult(response, result);
            }
                break;
            case Grpc.Protocol.Sdk.Search.IndexManager.Command.CommandOneofCase.PauseIngest:
            {
                var request = opSearchIndex.PauseIngest;
                var options = OptionsUtil.ConvertSearchIndexManagementOptions(request.Options);
                result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                var sw = Stopwatch.StartNew();
                if (scope is null) await indexManager.PauseIngestAsync(request.IndexName, options).ConfigureAwait(false);
                else await indexManager.PauseIngestAsync(request.IndexName, options, scope).ConfigureAwait(false);
                sw.Stop();
                result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                ResultsUtil.SetSuccess(result);
            }
                break;
            case Grpc.Protocol.Sdk.Search.IndexManager.Command.CommandOneofCase.ResumeIngest:
            {
                var request = opSearchIndex.ResumeIngest;
                var options = OptionsUtil.ConvertSearchIndexManagementOptions(request.Options);
                result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                var sw = Stopwatch.StartNew();
                if (scope is null) await indexManager.ResumeIngestAsync(request.IndexName, options).ConfigureAwait(false);
                else await indexManager.ResumeIngestAsync(request.IndexName, options, scope).ConfigureAwait(false);
                sw.Stop();
                result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                ResultsUtil.SetSuccess(result);
            }
                break;
            case Grpc.Protocol.Sdk.Search.IndexManager.Command.CommandOneofCase.UpsertIndex:
            {
                var request = opSearchIndex.UpsertIndex;
                var options = OptionsUtil.ConvertSearchIndexManagementOptions(request.Options);
                var searchIndex =
                    JsonConvert.DeserializeObject<SearchIndex>(
                        Encoding.UTF8.GetString(request.IndexDefinition.ToByteArray()));
                result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                var sw = Stopwatch.StartNew();
                if (scope is null) await indexManager.UpsertIndexAsync(searchIndex, options).ConfigureAwait(false);
                else await indexManager.UpsertIndexAsync(searchIndex, options, scope).ConfigureAwait(false);
                sw.Stop();
                result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                ResultsUtil.SetSuccess(result);
            }
                break;
            case Grpc.Protocol.Sdk.Search.IndexManager.Command.CommandOneofCase.GetAllIndexes:
            {
                var request = opSearchIndex.GetAllIndexes;
                var options = OptionsUtil.ConvertSearchIndexManagementOptions(request.Options);
                result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                var sw = Stopwatch.StartNew();
                IEnumerable<SearchIndex> response = null;
                if (scope is null) response = await indexManager.GetAllIndexesAsync(options).ConfigureAwait(false);
                else response = await indexManager.GetAllIndexesAsync(options, scope).ConfigureAwait(false);
                sw.Stop();
                result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                ResultsUtil.PopulateResult(response, result);
            }
                break;
            case Grpc.Protocol.Sdk.Search.IndexManager.Command.CommandOneofCase.GetIndexedDocumentsCount:
            {
                var request = opSearchIndex.GetIndexedDocumentsCount;
                var options = OptionsUtil.ConvertSearchIndexManagementOptions(request.Options);
                result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                var sw = Stopwatch.StartNew();
                int response = 0;
                if (scope is null)
                    response = await indexManager.GetIndexedDocumentsCountAsync(request.IndexName, options)
                        .ConfigureAwait(false);
                else response = await indexManager.GetIndexedDocumentsCountAsync(request.IndexName, options, scope).ConfigureAwait(false);
                sw.Stop();
                result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                ResultsUtil.PopulateResult(response, result);
            }
                break;
            default:
                throw new NotSupportedException(
                    "Could not determine Search index Management command.");
        }
    }
}