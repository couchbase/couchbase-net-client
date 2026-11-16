using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.FitPerformer.Utils;
using Couchbase.FitPerformer.Utils.Options;
using Couchbase.FitPerformer.Utils.Results;
using Couchbase.Grpc.Protocol.Shared;
using Couchbase.KeyValue;
using Couchbase.Query.Conversational;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Proto = Couchbase.Grpc.Protocol.Sdk.Cluster.ConversationalQuery;
using RunResult = Couchbase.Grpc.Protocol.Run.Result;
using SdkResult = Couchbase.Grpc.Protocol.Sdk.Result;

namespace Couchbase.FitPerformer.Workload;
#nullable enable

public static class ConversationalQueryHelper
{
    public static async Task RunCommand(Proto.Command command, ClusterConnection connection,
        ConcurrentDictionary<string, IRequestSpan> spans, RunResult result, bool returnResult)
    {
        var conversationalQuery = connection.Cluster.ConversationalQuery;
        switch (command.CommandCase)
        {
            case Proto.Command.CommandOneofCase.Query:
            {
                var request = command.Query;
                var keyspaces = await GetKeyspacesAsync(request.Keyspaces, connection).ConfigureAwait(false);
                var call = new QueryCall(conversationalQuery, request.Prompt, keyspaces, ConvertOptions(request.Options, spans));
                await ReadResultAsync(call, request.ContentAs, result, returnResult).ConfigureAwait(false);
                break;
            }
            case Proto.Command.CommandOneofCase.BeginChat:
            {
                var request = command.BeginChat;
                var keyspaces = await GetKeyspacesAsync(request.Keyspaces, connection).ConfigureAwait(false);
                var options = ConvertOptions(request.Options, spans);
                var chat = await Timed(result, () => options is null
                    ? conversationalQuery.BeginChatAsync(keyspaces)
                    : conversationalQuery.BeginChatAsync(keyspaces, options)).ConfigureAwait(false);
                connection.Chats[request.ChatRef] = chat;
                SetHandle(result, chat, returnResult);
                break;
            }
            case Proto.Command.CommandOneofCase.AttachChat:
            {
                var request = command.AttachChat;
                var options = ConvertOptions(request.Options);
                var chat = await Timed(result, () =>
                {
                    var handle = ChatHandle.FromJson(request.HandleJson);
                    return Task.FromResult(options is null
                        ? conversationalQuery.AttachChat(handle)
                        : conversationalQuery.AttachChat(handle, options));
                }).ConfigureAwait(false);
                connection.Chats[request.ChatRef] = chat;
                SetHandle(result, chat, returnResult);
                break;
            }
            case Proto.Command.CommandOneofCase.Chat:
                await RunChatCommand(command.Chat, connection, spans, result, returnResult).ConfigureAwait(false);
                break;
            default:
                throw new NotSupportedException("Unknown ConversationalQuery command: " + command.CommandCase);
        }
    }

    private static async Task RunChatCommand(Proto.ChatCommand command, ClusterConnection connection,
        ConcurrentDictionary<string, IRequestSpan> spans, RunResult result, bool returnResult)
    {
        if (!connection.Chats.TryGetValue(command.ChatRef, out var chat))
        {
            throw new InternalPerformerException("No chat stored for chat_ref " + command.ChatRef, null);
        }

        switch (command.CommandCase)
        {
            case Proto.ChatCommand.CommandOneofCase.Ask:
            {
                var request = command.Ask;
                var call = new AskCall(chat, request.Prompt, ConvertOptions(request.Options, spans));
                await ReadResultAsync(call, request.ContentAs, result, returnResult).ConfigureAwait(false);
                break;
            }
            case Proto.ChatCommand.CommandOneofCase.Pause:
            {
                var options = ConvertOptions(command.Pause.Options, spans);
                await ReadMetaDataAsync(result, returnResult,
                    () => options is null ? chat.PauseAsync() : chat.PauseAsync(options)).ConfigureAwait(false);
                break;
            }
            case Proto.ChatCommand.CommandOneofCase.Resume:
            {
                var options = ConvertOptions(command.Resume.Options, spans);
                await ReadMetaDataAsync(result, returnResult,
                    () => options is null ? chat.ResumeAsync() : chat.ResumeAsync(options)).ConfigureAwait(false);
                break;
            }
            case Proto.ChatCommand.CommandOneofCase.End:
            {
                var options = ConvertOptions(command.End.Options, spans);
                await ReadMetaDataAsync(result, returnResult,
                    () => options is null ? chat.EndAsync() : chat.EndAsync(options)).ConfigureAwait(false);
                break;
            }
            case Proto.ChatCommand.CommandOneofCase.Handle:
            {
                var handleJson = await Timed(result, () => Task.FromResult(chat.GetHandle().ToJson())).ConfigureAwait(false);
                if (returnResult) SetResult(result, new Proto.Result { HandleJson = handleJson });
                else ResultsUtil.SetSuccess(result);
                break;
            }
            default:
                throw new NotSupportedException("Unknown ChatCommand: " + command.CommandCase);
        }
    }

    // Lets one ContentAs switch pick the row type for both query and ask.
    private interface IRowsCall
    {
        Task<IConversationalQueryResult<T>> RunAsync<T>();
    }

    private sealed record QueryCall(IConversationalQuery ConversationalQuery, string Prompt,
        IReadOnlyList<ICouchbaseCollection> Keyspaces, ConversationalQueryOptions? Options) : IRowsCall
    {
        public Task<IConversationalQueryResult<T>> RunAsync<T>() => Options is null
            ? ConversationalQuery.QueryAsync<T>(Prompt, Keyspaces)
            : ConversationalQuery.QueryAsync<T>(Prompt, Keyspaces, Options);
    }

    private sealed record AskCall(IChat Chat, string Prompt, AskOptions? Options) : IRowsCall
    {
        public Task<IConversationalQueryResult<T>> RunAsync<T>() => Options is null
            ? Chat.AskAsync<T>(Prompt)
            : Chat.AskAsync<T>(Prompt, Options);
    }

    private static Task ReadResultAsync(IRowsCall call, ContentAs? contentAs, RunResult result, bool returnResult) =>
        contentAs?.AsCase switch
        {
            ContentAs.AsOneofCase.AsString => ReadResultAsync<string>(call,
                static row => new ContentTypes { ContentAsString = row }, result, returnResult),
            ContentAs.AsOneofCase.AsByteArray => ReadResultAsync<byte[]>(call,
                static row => new ContentTypes { ContentAsBytes = ByteString.CopyFrom(row) }, result, returnResult),
            ContentAs.AsOneofCase.AsJsonObject => ReadResultAsync<JObject>(call, JsonContent, result, returnResult),
            ContentAs.AsOneofCase.AsJsonArray => ReadResultAsync<JArray>(call, JsonContent, result, returnResult),
            ContentAs.AsOneofCase.AsBoolean => ReadResultAsync<bool>(call,
                static row => new ContentTypes { ContentAsBool = row }, result, returnResult),
            ContentAs.AsOneofCase.AsInteger => ReadResultAsync<long>(call,
                static row => new ContentTypes { ContentAsInt64 = row }, result, returnResult),
            ContentAs.AsOneofCase.AsFloatingPoint => ReadResultAsync<double>(call,
                static row => new ContentTypes { ContentAsDouble = row }, result, returnResult),
            _ => throw new NotSupportedException("Unknown ContentAs: " + contentAs?.AsCase),
        };

    private static async Task ReadResultAsync<T>(IRowsCall call, Func<T, ContentTypes> toContent, RunResult result,
        bool returnResult)
    {
        using var queryResult = await Timed(result, call.RunAsync<T>).ConfigureAwait(false);
        if (!returnResult)
        {
            ResultsUtil.SetSuccess(result);
            return;
        }

        var builder = new Proto.ConversationalQueryResult();
        // The protocol asks for the statement before the rows.
        if (queryResult.GeneratedStatement is { } statement) builder.GeneratedStatement = statement;
        await foreach (var row in queryResult.Rows.ConfigureAwait(false))
        {
            builder.Content.Add(toContent(row));
        }

        builder.MetaData = ConvertMetaData(queryResult.MetaData);
        SetResult(result, new Proto.Result { QueryResult = builder });
    }

    private static ContentTypes JsonContent(JToken row) =>
        new() { ContentAsBytes = ByteString.CopyFromUtf8(row.ToString(Formatting.None)) };

    private static async Task ReadMetaDataAsync(RunResult result, bool returnResult,
        Func<Task<ConversationalQueryMetaData>> operation)
    {
        var metaData = await Timed(result, operation).ConfigureAwait(false);
        if (returnResult) SetResult(result, new Proto.Result { MetaData = ConvertMetaData(metaData) });
        else ResultsUtil.SetSuccess(result);
    }

    private static async Task<T> Timed<T>(RunResult result, Func<Task<T>> operation)
    {
        result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
        var sw = Stopwatch.StartNew();
        var value = await operation().ConfigureAwait(false);
        sw.Stop();
        result.ElapsedNanos = sw.Elapsed.CalculateNanos();
        return value;
    }

    private static void SetHandle(RunResult result, IChat chat, bool returnResult)
    {
        if (returnResult) SetResult(result, new Proto.Result { HandleJson = chat.GetHandle().ToJson() });
        else ResultsUtil.SetSuccess(result);
    }

    private static void SetResult(RunResult result, Proto.Result value) =>
        result.Sdk = new SdkResult { ConversationalQueryResult = value };

    private static async Task<IReadOnlyList<ICouchbaseCollection>> GetKeyspacesAsync(
        RepeatedField<Collection> keyspaces, ClusterConnection connection)
    {
        var collections = new List<ICouchbaseCollection>(keyspaces.Count);
        foreach (var keyspace in keyspaces)
        {
            collections.Add(await connection
                .GetCollectionAsync(keyspace.BucketName, keyspace.ScopeName, keyspace.CollectionName)
                .ConfigureAwait(false));
        }

        return collections;
    }

    private static Proto.ConversationalQueryMetaData ConvertMetaData(ConversationalQueryMetaData metaData)
    {
        var ret = new Proto.ConversationalQueryMetaData
        {
            RequestId = metaData.RequestId,
            ClientContextId = metaData.ClientContextId,
            Status = QueryHelper.ConvertQueryStatus(metaData.Status)
        };
        ret.Warnings.AddRange(QueryHelper.ConvertQueryWarnings(metaData.Warnings));
        if (metaData.Metrics is not null) ret.Metrics = QueryHelper.ConvertQueryMetrics(metaData.Metrics);
        object? profile = metaData.Profile;
        if (profile is not null) ret.Profile = ByteString.CopyFromUtf8(profile.ToString());
        if (metaData.RequestTokens is not null) ret.RequestTokens = ConvertTokenUsage(metaData.RequestTokens);
        if (metaData.ChatTokens is not null) ret.ChatTokens = ConvertTokenUsage(metaData.ChatTokens);
        if (metaData.NaturalLanguageProcessingTime is { } processingTime)
        {
            ret.NaturalLanguageProcessingTime = Duration.FromTimeSpan(processingTime);
        }

        if (metaData.GeneratedStatementExecuted is { } executed) ret.GeneratedStatementExecuted = executed;
        return ret;
    }

    private static Proto.TokenUsage ConvertTokenUsage(TokenUsage usage)
    {
        var ret = new Proto.TokenUsage();
        if (usage.PromptTokens is { } promptTokens) ret.PromptTokens = promptTokens;
        if (usage.CompletionTokens is { } completionTokens) ret.CompletionTokens = completionTokens;
        if (usage.TotalTokens is { } totalTokens) ret.TotalTokens = totalTokens;
        return ret;
    }

    private static ConversationalQueryOptions? ConvertOptions(Proto.ConversationalQueryOptions? proto,
        ConcurrentDictionary<string, IRequestSpan> spans)
    {
        if (proto is null) return null;
        var options = new ConversationalQueryOptions();
        if (proto.Model is not null) options.Model(ConvertModel(proto.Model));
        if (proto.Credential is not null) options.Credential(ConvertCredential(proto.Credential));
        if (proto.HasHint) options.Hint(proto.Hint);
        if (proto.HasExecute) options.Execute(proto.Execute);
        if (proto.HasOutput) options.Output(ConvertOutput(proto.Output));
        if (proto.HasUseKnowledge) options.UseKnowledge(proto.UseKnowledge);
        if (proto.HasScanConsistency) options.ScanConsistency(OptionsUtil.ConvertScanConsistency(proto.ScanConsistency));
        if (proto.ConsistentWith is not null) options.ConsistentWith(OptionsUtil.ConvertMutationState(proto.ConsistentWith));
        if (proto.HasScanWaitMillis) options.ScanWait(TimeSpan.FromMilliseconds(proto.ScanWaitMillis));
        if (proto.HasUseReplica) options.UseReplica(proto.UseReplica);
        if (proto.HasClientContextId) options.ClientContextId(proto.ClientContextId);
        if (proto.HasMetrics) options.Metrics(proto.Metrics);
        if (proto.HasProfile) options.Profile(OptionsUtil.ConvertQueryProfile(proto.Profile));
        if (proto.HasTimeoutMillis) options.Timeout(TimeSpan.FromMilliseconds(proto.TimeoutMillis));
        foreach (var (name, json) in proto.RawParameters) options.Raw(name, JToken.Parse(json));
        if (proto.HasParentSpanId) options.RequestSpan(spans[proto.ParentSpanId]);
        return options;
    }

    private static AskOptions? ConvertOptions(Proto.AskOptions? proto, ConcurrentDictionary<string, IRequestSpan> spans)
    {
        if (proto is null) return null;
        var options = new AskOptions();
        if (proto.HasHint) options.Hint(proto.Hint);
        if (proto.HasExecute) options.Execute(proto.Execute);
        if (proto.HasOutput) options.Output(ConvertOutput(proto.Output));
        if (proto.HasScanConsistency) options.ScanConsistency(OptionsUtil.ConvertScanConsistency(proto.ScanConsistency));
        if (proto.ConsistentWith is not null) options.ConsistentWith(OptionsUtil.ConvertMutationState(proto.ConsistentWith));
        if (proto.HasScanWaitMillis) options.ScanWait(TimeSpan.FromMilliseconds(proto.ScanWaitMillis));
        if (proto.HasUseReplica) options.UseReplica(proto.UseReplica);
        if (proto.HasClientContextId) options.ClientContextId(proto.ClientContextId);
        if (proto.HasMetrics) options.Metrics(proto.Metrics);
        if (proto.HasProfile) options.Profile(OptionsUtil.ConvertQueryProfile(proto.Profile));
        if (proto.HasTimeoutMillis) options.Timeout(TimeSpan.FromMilliseconds(proto.TimeoutMillis));
        foreach (var (name, json) in proto.RawParameters) options.Raw(name, JToken.Parse(json));
        if (proto.HasParentSpanId) options.RequestSpan(spans[proto.ParentSpanId]);
        return options;
    }

    private static BeginChatOptions? ConvertOptions(Proto.BeginChatOptions? proto,
        ConcurrentDictionary<string, IRequestSpan> spans)
    {
        if (proto is null) return null;
        var options = new BeginChatOptions();
        if (proto.Model is not null) options.Model(ConvertModel(proto.Model));
        if (proto.Credential is not null) options.Credential(ConvertCredential(proto.Credential));
        if (proto.HasInactivityTimeoutMillis)
        {
            options.InactivityTimeout(TimeSpan.FromMilliseconds(proto.InactivityTimeoutMillis));
        }

        if (proto.HasUseKnowledge) options.UseKnowledge(proto.UseKnowledge);
        if (proto.HasTimeoutMillis) options.Timeout(TimeSpan.FromMilliseconds(proto.TimeoutMillis));
        if (proto.HasParentSpanId) options.RequestSpan(spans[proto.ParentSpanId]);
        return options;
    }

    private static AttachChatOptions? ConvertOptions(Proto.AttachChatOptions? proto)
    {
        if (proto is null) return null;
        var options = new AttachChatOptions();
        if (proto.Model is not null) options.Model(ConvertModel(proto.Model));
        if (proto.Credential is not null) options.Credential(ConvertCredential(proto.Credential));
        return options;
    }

    private static PauseOptions? ConvertOptions(Proto.PauseOptions? proto, ConcurrentDictionary<string, IRequestSpan> spans)
    {
        if (proto is null) return null;
        var options = new PauseOptions();
        if (proto.HasSummarize) options.Summarize(proto.Summarize);
        if (proto.HasTimeoutMillis) options.Timeout(TimeSpan.FromMilliseconds(proto.TimeoutMillis));
        if (proto.HasParentSpanId) options.RequestSpan(spans[proto.ParentSpanId]);
        return options;
    }

    private static ResumeOptions? ConvertOptions(Proto.ResumeOptions? proto, ConcurrentDictionary<string, IRequestSpan> spans)
    {
        if (proto is null) return null;
        var options = new ResumeOptions();
        if (proto.HasTimeoutMillis) options.Timeout(TimeSpan.FromMilliseconds(proto.TimeoutMillis));
        if (proto.HasParentSpanId) options.RequestSpan(spans[proto.ParentSpanId]);
        return options;
    }

    private static EndOptions? ConvertOptions(Proto.EndOptions? proto, ConcurrentDictionary<string, IRequestSpan> spans)
    {
        if (proto is null) return null;
        var options = new EndOptions();
        if (proto.HasTimeoutMillis) options.Timeout(TimeSpan.FromMilliseconds(proto.TimeoutMillis));
        if (proto.HasParentSpanId) options.RequestSpan(spans[proto.ParentSpanId]);
        return options;
    }

    private static ModelOptions ConvertModel(Proto.ModelOptions proto)
    {
        var model = new ModelOptions();
        if (proto.HasProvider) model.Provider(proto.Provider);
        if (proto.HasName) model.Name(proto.Name);
        if (proto.HasEndpoint) model.Endpoint(proto.Endpoint);
        if (proto.HasOutputTokenLimit) model.OutputTokenLimit(checked((int)proto.OutputTokenLimit));
        if (proto.HasModeration) model.Moderation(proto.Moderation);
        foreach (var (key, json) in proto.Raw) model.Raw(key, JsonNode.Parse(json));
        return model;
    }

    private static ModelCredential ConvertCredential(Proto.ModelCredential proto) => proto.CredentialCase switch
    {
        Proto.ModelCredential.CredentialOneofCase.Capella => ModelCredential.Capella(
            proto.Capella.Email, proto.Capella.Password, proto.Capella.OrganizationId),
        Proto.ModelCredential.CredentialOneofCase.CredentialStore => ModelCredential.CredentialStore(proto.CredentialStore),
        Proto.ModelCredential.CredentialOneofCase.ApiKey => ModelCredential.ApiKey(proto.ApiKey),
        _ => throw new NotSupportedException("Unknown ModelCredential: " + proto.CredentialCase),
    };

    private static ConversationalQueryOutput ConvertOutput(Proto.ConversationalQueryOutput output) => output switch
    {
        Proto.ConversationalQueryOutput.Sqlpp => ConversationalQueryOutput.Sqlpp,
        Proto.ConversationalQueryOutput.FtsSqlpp => ConversationalQueryOutput.FtsSqlpp,
        Proto.ConversationalQueryOutput.JsUdf => ConversationalQueryOutput.JsUdf,
        _ => throw new NotSupportedException("Unknown ConversationalQueryOutput: " + output),
    };
}
