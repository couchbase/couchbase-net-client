using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.FitPerformer.Utils.Options;
using Couchbase.FitPerformer.Utils.Results;
using Couchbase.Grpc.Protocol.Shared;
using Couchbase.KeyValue;
using Couchbase.Query;
using Couchbase.FitPerformer.Utils;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.FitPerformer.Workload;

public static class QueryHelper
{
    public static async Task PerformClusterQuery(Couchbase.Grpc.Protocol.Sdk.Query.Command request,
        ConcurrentDictionary<string, IRequestSpan> spans, Couchbase.Grpc.Protocol.Run.Result result,
        ICluster cluster, bool returnResults)
    {
        await QueryTypeDeterminator(request.ContentAs, request.Statement, OptionsUtil.ConvertQueryOptions(request.Options, spans), result, returnResults, cluster).ConfigureAwait(false);
    }

    public static async Task PerformScopeQuery(Couchbase.Grpc.Protocol.Sdk.Query.Command request,
        ConcurrentDictionary<string, IRequestSpan> spans, Couchbase.Grpc.Protocol.Run.Result result, IScope scope, bool returnResults)
    {
        await QueryTypeDeterminator(request.ContentAs, request.Statement, OptionsUtil.ConvertQueryOptions(request.Options, spans), result, returnResults, null, scope).ConfigureAwait(false);
    }

    private static async Task QueryTypeDeterminator(ContentAs contentAs, string statement, QueryOptions options, Couchbase.Grpc.Protocol.Run.Result result, bool returnResults, ICluster cluster = null, IScope scope = null)
    {
        if (!returnResults)
        {
            result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
            var sw = Stopwatch.StartNew();
            _ = cluster != null
                ? await cluster.QueryAsync<dynamic>(statement, options).ConfigureAwait(false)
                : await scope.QueryAsync<dynamic>(statement, options).ConfigureAwait(false);
            sw.Stop();
            result.ElapsedNanos = sw.Elapsed.CalculateNanos();
            ResultsUtil.SetSuccess(result);
        }
        else
        {
            var builder = new Couchbase.Grpc.Protocol.Sdk.Query.QueryResult();

            switch (contentAs?.AsCase)
            {
                case ContentAs.AsOneofCase.AsBoolean:
                {
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    var queryStream = cluster != null
                        ? await cluster.QueryAsync<bool>(statement, options).ConfigureAwait(false)
                        : await scope.QueryAsync<bool>(statement, options).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();

                    await foreach (var row in queryStream.Rows)
                    {
                        var content = new ContentTypes();
                        content.ContentAsBool = row;
                        builder.Content.Add(content);
                    }

                    ConvertMetadata(queryStream.MetaData, result, builder);
                }
                    break;
                case ContentAs.AsOneofCase.AsInteger:
                {
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    var queryStream = cluster != null
                        ? await cluster.QueryAsync<long>(statement, options).ConfigureAwait(false)
                        : await scope.QueryAsync<long>(statement, options).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();

                    await foreach (var row in queryStream.Rows)
                    {
                        var content = new ContentTypes();
                        content.ContentAsDouble = row;
                        builder.Content.Add(content);
                    }

                    ConvertMetadata(queryStream.MetaData, result, builder);
                }
                    break;
                case ContentAs.AsOneofCase.AsString:
                {
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);

                    var sw = Stopwatch.StartNew();
                    var queryStream = cluster != null
                        ? await cluster.QueryAsync<string>(statement, options).ConfigureAwait(false)
                        : await scope.QueryAsync<string>(statement, options).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();

                    await foreach (var row in queryStream.Rows)
                    {
                        var content = new ContentTypes();
                        content.ContentAsString = row;
                        builder.Content.Add(content);
                    }

                    ConvertMetadata(queryStream.MetaData, result, builder);
                }
                    break;
                case ContentAs.AsOneofCase.AsByteArray:
                {
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);

                    var sw = Stopwatch.StartNew();
                    var queryStream = cluster != null
                        ? await cluster.QueryAsync<byte[]>(statement, options).ConfigureAwait(false)
                        : await scope.QueryAsync<byte[]>(statement, options).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();

                    await foreach (var row in queryStream.Rows)
                    {
                        var content = new ContentTypes();
                        content.ContentAsBytes = ByteString.CopyFrom(row);
                        builder.Content.Add(content);
                    }

                    ConvertMetadata(queryStream.MetaData, result, builder);
                }
                    break;
                case ContentAs.AsOneofCase.AsFloatingPoint:
                {
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);

                    var sw = Stopwatch.StartNew();
                    var queryStream = cluster != null
                        ? await cluster.QueryAsync<double>(statement, options).ConfigureAwait(false)
                        : await scope.QueryAsync<double>(statement, options).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();

                    await foreach (var row in queryStream.Rows)
                    {
                        var content = new ContentTypes();
                        content.ContentAsDouble = row;
                        builder.Content.Add(content);
                    }

                    ConvertMetadata(queryStream.MetaData, result, builder);
                }
                    break;
                case ContentAs.AsOneofCase.AsJsonArray:
                {
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);

                    var sw = Stopwatch.StartNew();
                    var queryStream = cluster != null
                        ? await cluster.QueryAsync<JArray>(statement, options).ConfigureAwait(false)
                        : await scope.QueryAsync<JArray>(statement, options).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();

                    await foreach (var row in queryStream.Rows)
                    {
                        var content = new ContentTypes();
                        content.ContentAsBytes = ByteString.CopyFromUtf8(row.ToString(Formatting.None));
                        builder.Content.Add(content);
                    }

                    ConvertMetadata(queryStream.MetaData, result, builder);
                }
                    break;
                case ContentAs.AsOneofCase.AsJsonObject:
                {
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);

                    var sw = Stopwatch.StartNew();
                    var queryStream = cluster != null
                        ? await cluster.QueryAsync<JObject>(statement, options).ConfigureAwait(false)
                        : await scope.QueryAsync<JObject>(statement, options).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();

                    await foreach (var row in queryStream.Rows)
                    {
                        var content = new ContentTypes();
                        content.ContentAsBytes = ByteString.CopyFromUtf8(row.ToString(Formatting.None));
                        builder.Content.Add(content);
                    }

                    ConvertMetadata(queryStream.MetaData, result, builder);
                }
                    break;
                default:
                    throw new NotSupportedException("Could not determine ContentAs.");
            }

            result.Sdk = new Couchbase.Grpc.Protocol.Sdk.Result();
            result.Sdk.QueryResult = builder;
        }
    }

    private static void ConvertMetadata(QueryMetaData metadata, Couchbase.Grpc.Protocol.Run.Result result, Couchbase.Grpc.Protocol.Sdk.Query.QueryResult builder)
        {
            if (metadata != null)
            {
                builder.MetaData = new Couchbase.Grpc.Protocol.Sdk.Query.QueryMetaData();

                if (metadata.Metrics != null) builder.MetaData.Metrics = ConvertQueryMetrics(metadata.Metrics);
                builder.MetaData.Status = ConvertQueryStatus(metadata.Status);
                if (metadata.RequestId != null) builder.MetaData.RequestId = metadata.RequestId;
                if (metadata.ClientContextId != null) builder.MetaData.ClientContextId = metadata.ClientContextId;
                if (metadata.Warnings != null) builder.MetaData.Warnings.AddRange(ConvertQueryWarnings(metadata.Warnings));
                if (metadata.Profile != null) builder.MetaData.Profile = ByteString.CopyFromUtf8(metadata.Profile.ToString());
                if (metadata.Signature != null) builder.MetaData.Signature = ByteString.CopyFromUtf8(metadata.Signature.ToString());
            }
        }

        private static RepeatedField<Couchbase.Grpc.Protocol.Sdk.Query.QueryWarning> ConvertQueryWarnings(List<Couchbase.Query.QueryWarning> warnings)
        {
            var protoWarnings = new RepeatedField<Couchbase.Grpc.Protocol.Sdk.Query.QueryWarning>();
            protoWarnings.AddRange(warnings.Select(w => new Grpc.Protocol.Sdk.Query.QueryWarning { Code = w.Code, Message = w.Message }));
            return protoWarnings;
        }

        private static Couchbase.Grpc.Protocol.Sdk.Query.QueryStatus ConvertQueryStatus(QueryStatus status)
        {
            switch (status)
            {
                case QueryStatus.Completed:
                    return Grpc.Protocol.Sdk.Query.QueryStatus.Completed;
                case QueryStatus.Errors:
                    return Grpc.Protocol.Sdk.Query.QueryStatus.Errors;
                case QueryStatus.Failed:
                    throw new NotImplementedException("Failed QueryStatus is not in Proto definition.");
                case QueryStatus.Fatal:
                    return Grpc.Protocol.Sdk.Query.QueryStatus.Fatal;
                case QueryStatus.Running:
                    return Grpc.Protocol.Sdk.Query.QueryStatus.Running;
                case QueryStatus.Stopped:
                    return Grpc.Protocol.Sdk.Query.QueryStatus.Stopped;
                case QueryStatus.Success:
                    return Grpc.Protocol.Sdk.Query.QueryStatus.Success;
                case QueryStatus.Timeout:
                    return Grpc.Protocol.Sdk.Query.QueryStatus.Timeout;
                default:
                    throw new NotImplementedException("Couldn't convert QueryStatus.");
            }
        }

        private static Couchbase.Grpc.Protocol.Sdk.Query.QueryMetrics ConvertQueryMetrics(Couchbase.Query.QueryMetrics metrics)
        {
            var protoMetrics = new Couchbase.Grpc.Protocol.Sdk.Query.QueryMetrics
            {
                SortCount = metrics.SortCount,
                ResultCount = metrics.ResultCount,
                ResultSize = metrics.ResultSize,
                MutationCount = metrics.MutationCount,
                ErrorCount = metrics.ErrorCount,
                WarningCount = metrics.WarningCount
            };

            if (metrics.ElapsedTime != null)
            {
                var elapsed = TimeSpan.FromMilliseconds(double.Parse(metrics.ElapsedTime.Substring(0, metrics.ElapsedTime.Length - 2)));
                protoMetrics.ElapsedTime = Duration.FromTimeSpan(elapsed);
            }
            if (metrics.ExecutionTime != null)
            {
                var execution = TimeSpan.FromMilliseconds(double.Parse(metrics.ExecutionTime.Substring(0, metrics.ExecutionTime.Length - 2)));
                protoMetrics.ExecutionTime = Duration.FromTimeSpan(execution);
            }

            return protoMetrics;
        }
}