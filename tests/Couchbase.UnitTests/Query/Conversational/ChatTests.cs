using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Query;
using Couchbase.Query.Conversational;
using Xunit;
using static Couchbase.UnitTests.Query.Conversational.FakeConversationalQueryContext;

#nullable enable

namespace Couchbase.UnitTests.Query.Conversational
{
    public class ChatTests
    {
        private static readonly ModelParameters Model =
            ModelParameters.Create(new ModelOptions().Provider("openai"), ModelCredential.ApiKey("sk-test"));

        #region Ask

        [Fact]
        public async Task AskAsync_SendsChatTurnToOwnerWithModel()
        {
            var context = WithNodes("uuid-a", "uuid-b");
            var chat = new Chat(context, ChatId, "uuid-b", Model);
            using var cts = new CancellationTokenSource();

            using var result = await chat.AskAsync<dynamic>("and in Paris?", new AskOptions()
                .Hint("use city")
                .Execute(false)
                .Output(ConversationalQueryOutput.FtsSqlpp)
                .ClientContextId("ctx-1")
                .Metrics(true)
                .Timeout(TimeSpan.FromSeconds(30))
                .CancellationToken(cts.Token));

            var request = Assert.Single(context.Requests);
            Assert.Equal("""USING AI WITH {"hint":"use city","execute":false,"output":"FTSSQL"} and in Paris?""",
                request.Statement);
            Assert.Equal(new ConversationalRequest(ConversationalOperation.Ask, ChatId, "uuid-b", Model),
                request.Conversational);
            Assert.Equal(ChatId, request.Body.GetProperty("natural_chatid").GetString());
            Assert.Equal("sk-test", request.Body.GetProperty("natural_config").GetProperty("api_key").GetString());
            Assert.Equal("ctx-1", request.Body.GetProperty("client_context_id").GetString());
            Assert.True(request.Body.GetProperty("metrics").GetBoolean());
            Assert.Equal("30000ms", request.Body.GetProperty("timeout").GetString());
            Assert.Equal(cts.Token, request.Options.Token);
            Assert.False(request.ResultDisposed);
        }

        [Fact]
        public async Task AskAsync_NoOwner_ThrowsChatCurrentlyUnavailableLocally()
        {
            var context = WithNodes("uuid-a");
            var chat = new Chat(context, ChatId, owner: null, Model);

            var ex = await Assert.ThrowsAsync<ChatCurrentlyUnavailableException>(() => chat.AskAsync<dynamic>("hello"));

            Assert.Equal(ChatId, ex.ChatId);
            Assert.Null(ex.OwnerNodeUuid);
            Assert.Equal("ask", ex.Operation);
            Assert.Null(ex.Context);
            Assert.Empty(context.Requests);
        }

        [Fact]
        public async Task AskAsync_EmptyPrompt_ThrowsBeforeSending()
        {
            var context = WithNodes("uuid-a");

            await Assert.ThrowsAsync<InvalidArgumentException>(() =>
                new Chat(context, ChatId, "uuid-a", Model).AskAsync<dynamic>(" "));

            Assert.Empty(context.Requests);
        }

        #endregion

        #region Chat checks

        [Theory]
        [InlineData("ask", 3, typeof(InvalidOperationException))]
        [InlineData("ask", 2, typeof(FeatureNotAvailableException))]
        [InlineData("ask", 1, typeof(FeatureNotAvailableException))]
        [InlineData("ask", 0, typeof(ChatCurrentlyUnavailableException))]
        [InlineData("pause", 3, typeof(InvalidOperationException))]
        [InlineData("pause", 2, typeof(FeatureNotAvailableException))]
        [InlineData("pause", 1, typeof(FeatureNotAvailableException))]
        [InlineData("pause", 0, typeof(ChatCurrentlyUnavailableException))]
        [InlineData("end", 3, typeof(InvalidOperationException))]
        [InlineData("end", 2, typeof(FeatureNotAvailableException))]
        [InlineData("end", 1, typeof(FeatureNotAvailableException))]
        [InlineData("end", 0, typeof(ChatCurrentlyUnavailableException))]
        [InlineData("resume", 3, typeof(InvalidOperationException))]
        [InlineData("resume", 2, typeof(FeatureNotAvailableException))]
        [InlineData("resume", 1, typeof(FeatureNotAvailableException))]
        [InlineData("resume", 0, typeof(ServiceNotAvailableException))]
        public async Task ChatOperation_ChatChecks_RunInOrderBeforeSending(string operation, int failures, Type expected)
        {
            // No owner and no usable Query node, so the routing check fails last.
            var context = ConversationalQueryTests.FailingChatChecks(failures, queryNodes: false);
            var chat = new Chat(context, ChatId, owner: null, Model);

            var ex = await Assert.ThrowsAsync(expected, () => Run(chat, operation));

            if (failures == 1)
            {
                Assert.Contains("Capella Private Link", ex.Message);
            }

            Assert.Empty(context.Requests);
        }

        [Fact]
        public async Task ChatOperation_UnknownCapability_Sends()
        {
            var context = WithNodes("uuid-a");
            context.HasConversationalQueryCapability = null;

            using var result = await new Chat(context, ChatId, "uuid-a", Model).AskAsync<dynamic>("hello");

            Assert.Single(context.Requests);
        }

        #endregion

        #region Pause

        [Theory]
        [InlineData(null, "PAUSE CHAT", true)]
        [InlineData(true, """PAUSE CHAT WITH {"summarize":true}""", true)]
        [InlineData(false, """PAUSE CHAT WITH {"summarize":false}""", false)]
        public async Task PauseAsync_SendsModelUnlessSummarizeIsFalse(bool? summarize, string statement, bool sendsModel)
        {
            var context = WithNodes("uuid-a");
            var chat = new Chat(context, ChatId, "uuid-a", Model);
            var options = new PauseOptions();
            if (summarize is not null)
            {
                options.Summarize(summarize.Value);
            }

            await chat.PauseAsync(options);

            var request = Assert.Single(context.Requests);
            Assert.Equal(statement, request.Statement);
            Assert.Equal(ConversationalOperation.Pause, request.Conversational.Operation);
            Assert.Equal("uuid-a", request.Conversational.TargetNodeUuid);
            Assert.Equal(sendsModel ? Model : null, request.Conversational.Model);
            Assert.Equal(sendsModel, request.Body.TryGetProperty("natural_config", out _));
            Assert.True(request.ResultDisposed);
        }

        [Fact]
        public async Task PauseAsync_Success_ClearsOwner()
        {
            var context = WithNodes("uuid-a");
            var chat = new Chat(context, ChatId, "uuid-a", Model);

            var metaData = await chat.PauseAsync();

            Assert.Equal(QueryStatus.Success, metaData.Status);
            Assert.Null(chat.GetHandle().Owner);
            await Assert.ThrowsAsync<ChatCurrentlyUnavailableException>(() => chat.AskAsync<dynamic>("hello"));
            Assert.Single(context.Requests);
        }

        [Fact]
        public async Task PauseAsync_Failure_KeepsOwner()
        {
            var context = WithNodes("uuid-a").Throw(new AmbiguousTimeoutException("timed out"));
            var chat = new Chat(context, ChatId, "uuid-a", Model);

            await Assert.ThrowsAsync<AmbiguousTimeoutException>(() => chat.PauseAsync());

            Assert.Equal("uuid-a", chat.GetHandle().Owner);
        }

        #endregion

        #region Resume

        [Fact]
        public async Task ResumeAsync_ResolvableOwner_SendsToOwnerWithoutModel()
        {
            var context = WithNodes("uuid-a", "uuid-b");
            var chat = new Chat(context, ChatId, "uuid-b", Model);

            var metaData = await chat.ResumeAsync(new ResumeOptions().Timeout(TimeSpan.FromSeconds(10)));

            var request = Assert.Single(context.Requests);
            Assert.Equal("RESUME CHAT", request.Statement);
            Assert.Equal(new ConversationalRequest(ConversationalOperation.Resume, ChatId, "uuid-b"), request.Conversational);
            Assert.Equal(TimeSpan.FromSeconds(10), request.Options.TimeoutValue);
            Assert.True(request.ResultDisposed);
            Assert.Equal("d4c7cdd0-8f6b-4a0e-9b1d-3f2a6c5e7b90", metaData.RequestId);
            Assert.Equal("uuid-b", chat.GetHandle().Owner);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("uuid-gone")]
        public async Task ResumeAsync_NoResolvableOwner_SendsToRandomNodeAndSetsOwner(string? owner)
        {
            var context = WithNodes("uuid-a");
            var chat = new Chat(context, ChatId, owner, Model);

            await chat.ResumeAsync();

            Assert.Equal("uuid-a", Assert.Single(context.Requests).Conversational.TargetNodeUuid);
            Assert.Equal("uuid-a", chat.GetHandle().Owner);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("uuid-a")]
        [InlineData("uuid-gone")]
        public async Task ResumeAsync_ChatAlreadyLive_SetsOwnerToTarget(string? owner)
        {
            var context = WithNodes("uuid-a").Throw(new CouchbaseException(ErrorContext(19255)));
            var chat = new Chat(context, ChatId, owner, Model);

            await Assert.ThrowsAsync<CouchbaseException>(() => chat.ResumeAsync());

            Assert.Equal("uuid-a", chat.GetHandle().Owner);
        }

        [Fact]
        public async Task ResumeAsync_ResumeFailedOnOwner_ClearsOwner()
        {
            var context = WithNodes("uuid-a").Throw(new CouchbaseException(ErrorContext(19243)));
            var chat = new Chat(context, ChatId, "uuid-a", Model);

            await Assert.ThrowsAsync<CouchbaseException>(() => chat.ResumeAsync());

            Assert.Null(chat.GetHandle().Owner);
        }

        [Fact]
        public async Task ResumeAsync_ResumeFailedOnRandomNode_KeepsOwner()
        {
            var context = WithNodes("uuid-a").Throw(new TemporaryFailureException(ErrorContext(19243)));
            var chat = new Chat(context, ChatId, "uuid-gone", Model);

            await Assert.ThrowsAsync<TemporaryFailureException>(() => chat.ResumeAsync());

            Assert.Equal("uuid-a", Assert.Single(context.Requests).Conversational.TargetNodeUuid);
            Assert.Equal("uuid-gone", chat.GetHandle().Owner);
        }

        [Fact]
        public async Task ResumeAsync_OtherQueryError_KeepsOwner()
        {
            var context = WithNodes("uuid-a").Throw(new AuthenticationFailureException(ErrorContext(19241)));
            var chat = new Chat(context, ChatId, owner: null, Model);

            await Assert.ThrowsAsync<AuthenticationFailureException>(() => chat.ResumeAsync());

            Assert.Null(chat.GetHandle().Owner);
        }

        [Fact]
        public async Task ResumeAsync_QueryTimeoutResponse_KeepsOwner()
        {
            // A parsed response counts as a server answer, even when it maps to a timeout.
            var context = WithNodes("uuid-a").Throw(new AmbiguousTimeoutException("timed out", ErrorContext(1080)));
            var chat = new Chat(context, ChatId, owner: null, Model);

            await Assert.ThrowsAsync<AmbiguousTimeoutException>(() => chat.ResumeAsync());

            Assert.Null(chat.GetHandle().Owner);
        }

        [Theory]
        [InlineData("client timeout")]
        [InlineData("transport error")]
        [InlineData("other")]
        public async Task ResumeAsync_NoQueryResponse_SetsOwnerToTarget(string outcome)
        {
            Exception failure = outcome switch
            {
                "client timeout" => new AmbiguousTimeoutException("The query was timed out via the Token.",
                    new OperationCanceledException()) { Context = new QueryErrorContext() },
                "transport error" => new RequestCanceledException("The query was canceled.",
                    new HttpRequestException("reset")) { Context = new QueryErrorContext() },
                _ => new InvalidOperationException("unexpected")
            };
            var context = WithNodes("uuid-a").Throw(failure);
            var chat = new Chat(context, ChatId, owner: null, Model);

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => chat.ResumeAsync());

            Assert.Same(failure, ex);
            Assert.Equal("uuid-a", chat.GetHandle().Owner);
        }

        [Fact]
        public async Task ResumeAsync_ServiceNotAvailableBeforeDispatch_KeepsOwner()
        {
            var context = WithNodes("uuid-a").Throw(new ServiceNotAvailableException(ServiceType.Query));
            var chat = new Chat(context, ChatId, "uuid-gone", Model);

            await Assert.ThrowsAsync<ServiceNotAvailableException>(() => chat.ResumeAsync());

            Assert.Equal("uuid-gone", chat.GetHandle().Owner);
        }

        #endregion

        #region End

        [Fact]
        public async Task EndAsync_SendsToOwnerWithoutModel()
        {
            var context = WithNodes("uuid-a");
            var chat = new Chat(context, ChatId, "uuid-a", Model);

            await chat.EndAsync();

            var request = Assert.Single(context.Requests);
            Assert.Equal("END CHAT", request.Statement);
            Assert.Equal(new ConversationalRequest(ConversationalOperation.End, ChatId, "uuid-a"), request.Conversational);
            Assert.False(request.Body.TryGetProperty("natural_config", out _));
            Assert.True(request.ResultDisposed);
        }

        [Theory]
        [InlineData("ask")]
        [InlineData("pause")]
        [InlineData("resume")]
        [InlineData("end")]
        [InlineData("handle")]
        public async Task EndAsync_Success_ClosesTheChat(string operation)
        {
            var context = WithNodes("uuid-a");
            var chat = new Chat(context, ChatId, "uuid-a", Model);
            await chat.EndAsync();

            await Assert.ThrowsAsync<InvalidArgumentException>(() => Run(chat, operation));

            Assert.Single(context.Requests);
        }

        [Fact]
        public async Task EndAsync_Failure_KeepsTheChatOpen()
        {
            var context = WithNodes("uuid-a").Throw(new AmbiguousTimeoutException("timed out"));
            var chat = new Chat(context, ChatId, "uuid-a", Model);

            await Assert.ThrowsAsync<AmbiguousTimeoutException>(() => chat.EndAsync());

            Assert.Equal(new ChatHandle(ChatId, "uuid-a"), chat.GetHandle());
        }

        #endregion

        #region GetHandle

        [Fact]
        public async Task GetHandle_ReturnsASnapshot()
        {
            var context = WithNodes("uuid-a");
            var chat = new Chat(context, ChatId, "uuid-a", Model);

            var before = chat.GetHandle();
            await chat.PauseAsync();
            var after = chat.GetHandle();

            Assert.Equal(new ChatHandle(ChatId, "uuid-a"), before);
            Assert.Equal(new ChatHandle(ChatId, null), after);
            Assert.NotSame(chat.GetHandle(), after);
        }

        #endregion

        private static async Task Run(IChat chat, string operation)
        {
            switch (operation)
            {
                case "ask":
                    (await chat.AskAsync<dynamic>("hello")).Dispose();
                    break;
                case "pause":
                    await chat.PauseAsync();
                    break;
                case "resume":
                    await chat.ResumeAsync();
                    break;
                case "end":
                    await chat.EndAsync();
                    break;
                case "handle":
                    chat.GetHandle();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation));
            }
        }
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
