using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.KeyValue;
using Couchbase.Query;
using Couchbase.Query.Conversational;
using Moq;
using Xunit;
using static Couchbase.UnitTests.Query.Conversational.FakeConversationalQueryContext;

#nullable enable

namespace Couchbase.UnitTests.Query.Conversational;

public class ConversationalQueryTests
{
    private static readonly ICouchbaseCollection[] Hotels = { Collection("travel-sample", "inventory", "hotel") };

    #region QueryAsync

    [Fact]
    public async Task QueryAsync_SendsUsingAiWithModel()
    {
        var context = WithNodes("uuid-a");
        var query = new ConversationalQuery(context);

        using var result = await query.QueryAsync<dynamic>("count hotels", Hotels, new ConversationalQueryOptions()
            .Model(new ModelOptions().Provider("openai").Name("gpt-4o"))
            .Credential(ModelCredential.ApiKey("sk-test"))
            .Hint("use country")
            .Execute(false)
            .Output(ConversationalQueryOutput.Sqlpp)
            .UseKnowledge(true));

        var request = Assert.Single(context.Requests);
        Assert.Equal(
            """USING AI WITH {"keyspaces":["`travel-sample`.`inventory`.`hotel`"],"knowledge":true,"hint":"use country","execute":false,"output":"SQL"} count hotels""",
            request.Statement);
        Assert.Equal(ConversationalOperation.Query, request.Conversational.Operation);
        Assert.Null(request.Conversational.ChatId);
        Assert.Null(request.Conversational.TargetNodeUuid);

        var config = request.Body.GetProperty("natural_config");
        Assert.Equal("openai", config.GetProperty("provider").GetString());
        Assert.Equal("gpt-4o", config.GetProperty("model").GetString());
        Assert.Equal("sk-test", config.GetProperty("api_key").GetString());
    }

    [Fact]
    public async Task QueryAsync_UnsetOptions_SendsNoKnowledgeAndNoMetrics()
    {
        var context = WithNodes("uuid-a");

        using var result = await new ConversationalQuery(context).QueryAsync<dynamic>("count hotels", Hotels);

        var request = Assert.Single(context.Requests);
        Assert.Equal("""USING AI WITH {"keyspaces":["`travel-sample`.`inventory`.`hotel`"]} count hotels""", request.Statement);
        Assert.False(request.Body.GetProperty("metrics").GetBoolean());
        Assert.False(request.Body.TryGetProperty("natural_config", out _));
    }

    [Fact]
    public async Task QueryAsync_SharedOptions_AreForwarded()
    {
        var context = WithNodes("uuid-a");
        using var cts = new CancellationTokenSource();
        var span = Mock.Of<IRequestSpan>();

        using var result = await new ConversationalQuery(context).QueryAsync<dynamic>("count hotels", Hotels,
            new ConversationalQueryOptions()
                .ScanConsistency(QueryScanConsistency.RequestPlus)
                .ScanWait(TimeSpan.FromSeconds(2))
                .UseReplica(true)
                .ClientContextId("ctx-1")
                .Metrics(true)
                .Profile(QueryProfile.Timings)
                .Timeout(TimeSpan.FromSeconds(30))
                .Raw("max_parallelism", 3)
                .RequestSpan(span)
                .CancellationToken(cts.Token));

        var request = Assert.Single(context.Requests);
        var body = request.Body;
        Assert.Equal("request_plus", body.GetProperty("scan_consistency").GetString());
        Assert.Equal("2000ms", body.GetProperty("scan_wait").GetString());
        Assert.Equal("on", body.GetProperty("use_replica").GetString());
        Assert.Equal("ctx-1", body.GetProperty("client_context_id").GetString());
        Assert.True(body.GetProperty("metrics").GetBoolean());
        Assert.Equal("timings", body.GetProperty("profile").GetString());
        Assert.Equal("30000ms", body.GetProperty("timeout").GetString());
        Assert.Equal(3, body.GetProperty("max_parallelism").GetInt32());
        Assert.Equal(cts.Token, request.Options.Token);
        Assert.Same(span, request.Options.RequestSpanValue);
        Assert.True(request.Options.IsAdHoc);
        Assert.False(body.TryGetProperty("readonly", out _));
    }

    [Fact]
    public async Task QueryAsync_ConsistentWith_SendsScanVectors()
    {
        var context = WithNodes("uuid-a");
        var state = new MutationState();
        state.Add(new MutationResult(0, TimeSpan.FromSeconds(10), new MutationToken("travel-sample", 1, 2, 3)));

        using var result = await new ConversationalQuery(context).QueryAsync<dynamic>("count hotels", Hotels,
            new ConversationalQueryOptions().ConsistentWith(state));

        var body = Assert.Single(context.Requests).Body;
        Assert.Equal("at_plus", body.GetProperty("scan_consistency").GetString());
        Assert.True(body.GetProperty("scan_vectors").TryGetProperty("travel-sample", out _));
    }

    [Fact]
    public async Task QueryAsync_ReusedOptions_EachRequestSendsACopy()
    {
        var context = WithNodes("uuid-a");
        var query = new ConversationalQuery(context);
        var options = new ConversationalQueryOptions().Timeout(TimeSpan.FromSeconds(5));

        (await query.QueryAsync<dynamic>("one", Hotels, options)).Dispose();
        (await query.QueryAsync<dynamic>("two", Hotels, options)).Dispose();

        Assert.Equal(2, context.Requests.Count);
        Assert.NotSame(context.Requests[0].Options, context.Requests[1].Options);
        Assert.NotSame(options.QueryOptions, context.Requests[0].Options);
        Assert.Null(options.QueryOptions.Conversational);
        Assert.All(context.Requests, r => Assert.Equal(TimeSpan.FromSeconds(5), r.Options.TimeoutValue));
    }

    [Fact]
    public async Task QueryAsync_DoesNoChatChecks()
    {
        var context = new FakeConversationalQueryContext
        {
            BootstrapException = new InvalidOperationException("not bootstrapped"),
            HasConversationalQueryCapability = false
        };
        context.QueryNodes.Add(Node("uuid-a", "http://lb:8093/query/service"));
        context.QueryNodes.Add(Node("uuid-b", "http://lb:8093/query/service"));

        using var result = await new ConversationalQuery(context).QueryAsync<dynamic>("count hotels", Hotels);

        Assert.Single(context.Requests);
    }

    [Fact]
    public async Task Raw_NaturalParameter_IsSent()
    {
        var context = WithNodes("uuid-a");

        (await new ConversationalQuery(context).QueryAsync<dynamic>("count hotels", Hotels,
            new ConversationalQueryOptions().Raw("natural_new_option", "x"))).Dispose();
        (await new Chat(context, ChatId, "uuid-a", ModelParameters.Create(null, null)).AskAsync<dynamic>("hello",
            new AskOptions().Raw("natural_new_option", "x"))).Dispose();

        Assert.Equal(2, context.Requests.Count);
        Assert.All(context.Requests,
            r => Assert.Equal("x", r.Body.GetProperty("natural_new_option").GetString()));
    }

    [Fact]
    public async Task QueryAsync_EmptyKeyspaces_ThrowsBeforeSending()
    {
        var context = WithNodes("uuid-a");

        await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            new ConversationalQuery(context).QueryAsync<dynamic>("count hotels", Array.Empty<ICouchbaseCollection>()));

        Assert.Empty(context.Requests);
    }

    [Fact]
    public async Task QueryAsync_NullKeyspace_ThrowsBeforeSending()
    {
        var context = WithNodes("uuid-a");
        var query = new ConversationalQuery(context);

        await Assert.ThrowsAsync<InvalidArgumentException>(() => query.QueryAsync<dynamic>("count hotels", null!));
        await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            query.QueryAsync<dynamic>("count hotels", new ICouchbaseCollection[] { null! }));

        Assert.Empty(context.Requests);
    }

    [Fact]
    public async Task QueryAsync_InvalidModel_ThrowsBeforeSending()
    {
        var context = WithNodes("uuid-a");

        await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            new ConversationalQuery(context).QueryAsync<dynamic>("count hotels", Hotels,
                new ConversationalQueryOptions()
                    .Model(new ModelOptions().Endpoint("https://model.example.com"))
                    .Credential(ModelCredential.Capella("user@example.com", "pw", "org-1"))));

        Assert.Empty(context.Requests);
    }

    [Fact]
    public async Task QueryAsync_Result_ExposesStatementRowsAndMetaData()
    {
        var context = WithNodes("uuid-a").Respond("""
            {
              "requestID": "b29381db-1382-4729-b1fa-d18e0e8e5355",
              "clientContextID": "ctx-1",
              "generated_statement": "SELECT COUNT(*) AS `count` FROM hotel",
              "requestTokens": { "promptTokens": 812, "completionTokens": 31, "totalTokens": 843 },
              "chatTokens": { "promptTokens": 1630 },
              "signature": { "count": "number" },
              "results": [ { "count": 361 } ],
              "status": "success",
              "warnings": [ { "code": 1, "msg": "careful" } ],
              "metrics": {
                "elapsedTime": "1.990032281s",
                "executionTime": "6.282322ms",
                "resultCount": 1,
                "naturalLanguageProcessingTime": "1.5s",
                "generatedStatementExecuted": true
              },
              "profile": { "phaseTimes": {} }
            }
            """);

        var result = await new ConversationalQuery(context).QueryAsync<CountRow>("count hotels", Hotels);

        // The generated statement and token counts come before the rows, the metrics after them.
        Assert.Equal("SELECT COUNT(*) AS `count` FROM hotel", result.GeneratedStatement);
        var metaData = result.MetaData;
        Assert.Equal(843, metaData.RequestTokens?.TotalTokens);
        Assert.Null(metaData.GeneratedStatementExecuted);

        var rows = await result.Rows.ToListAsync();

        Assert.Equal(361, Assert.Single(rows).Count);
        Assert.Equal("b29381db-1382-4729-b1fa-d18e0e8e5355", metaData.RequestId);
        Assert.Equal("ctx-1", metaData.ClientContextId);
        Assert.Equal(QueryStatus.Success, metaData.Status);
        Assert.Equal(1, Assert.Single(metaData.Warnings).Code);
        Assert.Equal("1.990032281s", metaData.Metrics?.ElapsedTime);
        Assert.NotNull(metaData.Profile);
        Assert.Equal(812, metaData.RequestTokens?.PromptTokens);
        Assert.Equal(31, metaData.RequestTokens?.CompletionTokens);
        Assert.Equal(1630, metaData.ChatTokens?.PromptTokens);
        Assert.Null(metaData.ChatTokens?.TotalTokens);
        Assert.Equal(TimeSpan.FromSeconds(1.5), metaData.NaturalLanguageProcessingTime);
        Assert.True(metaData.GeneratedStatementExecuted);

        Assert.False(context.Requests[0].ResultDisposed);
        result.Dispose();
        Assert.True(context.Requests[0].ResultDisposed);
    }

    [Fact]
    public async Task QueryAsync_Result_CanBeEnumeratedDirectly()
    {
        var context = WithNodes("uuid-a").Respond(
            """{"requestID":"b29381db-1382-4729-b1fa-d18e0e8e5355","results":[1,2],"status":"success"}""");

        using var result = await new ConversationalQuery(context).QueryAsync<int>("numbers", Hotels);

        Assert.Equal(new[] { 1, 2 }, await result.ToListAsync());
        Assert.Empty(result.MetaData.Warnings);
        Assert.Null(result.GeneratedStatement);
    }

    #endregion

    #region MetaData

    [Fact]
    public void MetaData_NewInstance_IsEmpty()
    {
        var metaData = new ConversationalQueryMetaData();

        Assert.Equal(string.Empty, metaData.RequestId);
        Assert.Equal(string.Empty, metaData.ClientContextId);
        Assert.Equal(default(QueryStatus), metaData.Status);
        Assert.Empty(metaData.Warnings);
        Assert.Null(metaData.Metrics);
        Assert.Null(metaData.Profile);
        Assert.Null(metaData.RequestTokens);
        Assert.Null(metaData.ChatTokens);
        Assert.Null(metaData.NaturalLanguageProcessingTime);
        Assert.Null(metaData.GeneratedStatementExecuted);
    }

    [Fact]
    public void MetaData_ObjectInitializer_SetsEveryValue()
    {
        var metrics = new QueryMetrics { ResultCount = 1 };
        var requestTokens = new TokenUsage { TotalTokens = 843 };
        var chatTokens = new TokenUsage { TotalTokens = 1630 };

        var metaData = new ConversationalQueryMetaData
        {
            RequestId = "r-1",
            ClientContextId = "ctx-1",
            Status = QueryStatus.Success,
            Warnings = new[] { new QueryWarning { Code = 1 } },
            Metrics = metrics,
            Profile = "timings",
            RequestTokens = requestTokens,
            ChatTokens = chatTokens,
            NaturalLanguageProcessingTime = TimeSpan.FromSeconds(1.5),
            GeneratedStatementExecuted = true
        };

        Assert.Equal("r-1", metaData.RequestId);
        Assert.Equal("ctx-1", metaData.ClientContextId);
        Assert.Equal(QueryStatus.Success, metaData.Status);
        Assert.Equal(1, Assert.Single(metaData.Warnings).Code);
        Assert.Same(metrics, metaData.Metrics);
        Assert.Equal("timings", (string)metaData.Profile!);
        Assert.Same(requestTokens, metaData.RequestTokens);
        Assert.Same(chatTokens, metaData.ChatTokens);
        Assert.Equal(TimeSpan.FromSeconds(1.5), metaData.NaturalLanguageProcessingTime);
        Assert.True(metaData.GeneratedStatementExecuted);
    }

    #endregion

    #region BeginChatAsync

    [Fact]
    public async Task BeginChatAsync_PinsToTheSelectedNodeAndReadsChatId()
    {
        var context = WithNodes("uuid-a").Respond(BeginChatSuccess);
        var span = Mock.Of<IRequestSpan>();

        var chat = await new ConversationalQuery(context).BeginChatAsync(Hotels, new BeginChatOptions()
            .Model(new ModelOptions().Provider("openai"))
            .Credential(ModelCredential.ApiKey("sk-test"))
            .InactivityTimeout(TimeSpan.FromMinutes(5))
            .UseKnowledge(true)
            .Timeout(TimeSpan.FromSeconds(10))
            .RequestSpan(span));

        var request = Assert.Single(context.Requests);
        Assert.Equal("""BEGIN CHAT WITH {"keyspaces":["`travel-sample`.`inventory`.`hotel`"],"knowledge":true,"timeout":300}""",
            request.Statement);
        Assert.Equal(ConversationalOperation.BeginChat, request.Conversational.Operation);
        Assert.Equal("uuid-a", request.Conversational.TargetNodeUuid);
        Assert.Null(request.Conversational.ChatId);
        Assert.Null(request.Conversational.Model);
        Assert.Equal(TimeSpan.FromSeconds(10), request.Options.TimeoutValue);
        Assert.Same(span, request.Options.RequestSpanValue);
        Assert.True(request.ResultDisposed);
        Assert.Equal(new ChatHandle(ChatId, "uuid-a"), chat.GetHandle());
    }

    [Fact]
    public async Task BeginChatAsync_KeepsTheModelForLaterPrompts()
    {
        var context = WithNodes("uuid-a").Respond(BeginChatSuccess);
        var model = new ModelOptions().Provider("openai");

        var chat = await new ConversationalQuery(context).BeginChatAsync(Hotels,
            new BeginChatOptions().Model(model).Credential(ModelCredential.ApiKey("sk-test")));
        model.Provider("changed");
        using var result = await chat.AskAsync<dynamic>("and in Paris?");

        var config = context.Requests[1].Body.GetProperty("natural_config");
        Assert.Equal("openai", config.GetProperty("provider").GetString());
        Assert.Equal("sk-test", config.GetProperty("api_key").GetString());
    }

    [Fact]
    public async Task BeginChatAsync_DefaultOptions_SendsKnowledgeFalse()
    {
        var context = WithNodes("uuid-a").Respond(BeginChatSuccess);

        await new ConversationalQuery(context).BeginChatAsync(Hotels);

        Assert.Equal("""BEGIN CHAT WITH {"keyspaces":["`travel-sample`.`inventory`.`hotel`"],"knowledge":false}""",
            Assert.Single(context.Requests).Statement);
    }

    [Fact]
    public async Task BeginChatAsync_NoChatId_ThrowsCouchbaseException()
    {
        var context = WithNodes("uuid-a").Respond(Success);

        var ex = await Assert.ThrowsAsync<CouchbaseException>(() =>
            new ConversationalQuery(context).BeginChatAsync(Hotels));

        Assert.Contains("chat ID", ex.Message);
        Assert.True(Assert.Single(context.Requests).ResultDisposed);
    }

    [Fact]
    public async Task BeginChatAsync_NoQueryNode_ThrowsServiceNotAvailableBeforeSending()
    {
        var context = new FakeConversationalQueryContext();

        await Assert.ThrowsAsync<ServiceNotAvailableException>(() =>
            new ConversationalQuery(context).BeginChatAsync(Hotels));

        Assert.Empty(context.Requests);
    }

    [Fact]
    public async Task BeginChatAsync_InvalidModel_ThrowsBeforeSending()
    {
        var context = WithNodes("uuid-a");

        await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            new ConversationalQuery(context).BeginChatAsync(Hotels, new BeginChatOptions()
                .Model(new ModelOptions().OutputTokenLimit(10))
                .Credential(ModelCredential.Capella("user@example.com", "pw", "org-1"))));

        Assert.Empty(context.Requests);
    }

    [Fact]
    public async Task BeginChatAsync_EmptyKeyspaces_ThrowsBeforeSending()
    {
        var context = WithNodes("uuid-a");

        await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            new ConversationalQuery(context).BeginChatAsync(Array.Empty<ICouchbaseCollection>()));

        Assert.Empty(context.Requests);
    }

    [Fact]
    public async Task BeginChatAsync_UnknownCapability_Sends()
    {
        var context = WithNodes("uuid-a").Respond(BeginChatSuccess);
        context.HasConversationalQueryCapability = null;

        await new ConversationalQuery(context).BeginChatAsync(Hotels);

        Assert.Single(context.Requests);
    }

    [Theory]
    [InlineData(3, typeof(InvalidOperationException), "not bootstrapped")]
    [InlineData(2, typeof(FeatureNotAvailableException), "8.5")]
    [InlineData(1, typeof(FeatureNotAvailableException), "load balancer")]
    [InlineData(0, typeof(ServiceNotAvailableException), "n1ql")]
    public async Task BeginChatAsync_ChatChecks_RunInOrderBeforeSending(int failures, Type expected, string message)
    {
        var context = FailingChatChecks(failures, queryNodes: false);

        var ex = await Assert.ThrowsAsync(expected, () => new ConversationalQuery(context).BeginChatAsync(Hotels));

        Assert.Contains(message, ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(context.Requests);
    }

    // Fails the last checks first: 3 fails bootstrap, capability and endpoint, 1 fails only the shared endpoint check.
    internal static FakeConversationalQueryContext FailingChatChecks(int failures, bool queryNodes)
    {
        var context = new FakeConversationalQueryContext();
        if (failures >= 3)
        {
            context.BootstrapException = new InvalidOperationException("not bootstrapped");
        }

        if (failures >= 2)
        {
            context.HasConversationalQueryCapability = false;
        }

        if (failures >= 1)
        {
            context.QueryNodes.Add(Node("uuid-a", "http://lb:8093/query/service"));
            context.QueryNodes.Add(Node("uuid-b", "http://lb:8093/query/service"));
        }
        else if (queryNodes)
        {
            context.QueryNodes.Add(Node("uuid-a", "http://a:8093/query/service"));
        }

        return context;
    }

    #endregion

    #region AttachChat

    [Fact]
    public void AttachChat_DoesNoIoAndNoChatChecks()
    {
        var context = FailingChatChecks(3, queryNodes: false);

        var chat = new ConversationalQuery(context).AttachChat(new ChatHandle(ChatId, "uuid-a"));

        Assert.Equal(new ChatHandle(ChatId, "uuid-a"), chat.GetHandle());
        Assert.Empty(context.Requests);
    }

    [Fact]
    public void AttachChat_InvalidModel_Throws()
    {
        var query = new ConversationalQuery(WithNodes("uuid-a"));

        Assert.Throws<InvalidArgumentException>(() => query.AttachChat(new ChatHandle(ChatId, "uuid-a"),
            new AttachChatOptions()
                .Model(new ModelOptions().Moderation(true))
                .Credential(ModelCredential.Capella("user@example.com", "pw", "org-1"))));
    }

    [Fact]
    public void AttachChat_NullHandle_Throws()
    {
        var query = new ConversationalQuery(WithNodes("uuid-a"));

        Assert.Throws<InvalidArgumentException>(() => query.AttachChat(null!));
    }

    [Fact]
    public async Task AttachChat_UsesTheHandleOwnerAndOnlyTheAttachModel()
    {
        var context = WithNodes("uuid-a", "uuid-b");

        var chat = new ConversationalQuery(context).AttachChat(new ChatHandle(ChatId, "uuid-b"),
            new AttachChatOptions().Model(new ModelOptions().Name("gpt-4o")));
        using var result = await chat.AskAsync<dynamic>("and in Paris?");

        var request = Assert.Single(context.Requests);
        Assert.Equal("uuid-b", request.Conversational.TargetNodeUuid);
        Assert.Equal(ChatId, request.Conversational.ChatId);
        var config = request.Body.GetProperty("natural_config");
        Assert.Equal("gpt-4o", config.GetProperty("model").GetString());
        Assert.False(config.TryGetProperty("api_key", out _));
    }

    #endregion

    private sealed class CountRow
    {
        public int Count { get; set; }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
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
