using System;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Query.Conversational
{
    internal sealed class Chat : IChat
    {
        private const int ChatAlreadyLive = 19255;
        private const int ResumeFailed = 19243;

        private readonly IConversationalQueryContext _context;
        private readonly string _chatId;
        private readonly ModelParameters _model;
        private volatile string? _owner;
        private volatile bool _closed;

        public Chat(IConversationalQueryContext context, string chatId, string? owner, ModelParameters model)
        {
            _context = context;
            _chatId = chatId;
            _owner = owner;
            _model = model;
        }

        public async Task<IConversationalQueryResult<T>> AskAsync<T>(string prompt, AskOptions? options = null)
        {
            options ??= new AskOptions();
            var statement = ConversationalStatements.ChatTurn(prompt, options.HintValue, options.ExecuteValue,
                options.OutputValue);
            var owner = GetOwnerOrThrow(ConversationalOperation.Ask);

            return await ConversationalQuery.SendAsync<T>(_context, statement, options.QueryOptions,
                new ConversationalRequest(ConversationalOperation.Ask, _chatId, owner, _model)).ConfigureAwait(false);
        }

        public async Task<ConversationalQueryMetaData> PauseAsync(PauseOptions? options = null)
        {
            options ??= new PauseOptions();
            var owner = GetOwnerOrThrow(ConversationalOperation.Pause);

            // Without a summary Query does not call the model.
            var model = options.SummarizeValue == false ? null : _model;
            var metaData = await SendAsync(ConversationalStatements.PauseChat(options.SummarizeValue),
                options.QueryOptions, new ConversationalRequest(ConversationalOperation.Pause, _chatId, owner, model))
                .ConfigureAwait(false);

            _owner = null;
            return metaData;
        }

        public async Task<ConversationalQueryMetaData> ResumeAsync(ResumeOptions? options = null)
        {
            options ??= new ResumeOptions();
            ThrowIfClosed();
            ConversationalQuery.ThrowIfChatsUnavailable(_context);

            var owner = _owner;
            var toOwner = QueryNodes.TryResolve(_context.Nodes, owner, out _);
            var target = toOwner ? owner! : ConversationalQuery.SelectRandomNode(_context);
            try
            {
                var metaData = await SendAsync(ConversationalStatements.ResumeChat, options.QueryOptions,
                    new ConversationalRequest(ConversationalOperation.Resume, _chatId, target)).ConfigureAwait(false);

                _owner = target;
                return metaData;
            }
            catch (Exception e)
            {
                switch (FirstQueryErrorCode(e))
                {
                    case ChatAlreadyLive:
                        _owner = target;
                        break;
                    case ResumeFailed when toOwner:
                        _owner = null;
                        break;
                    // Unknown outcome. ServiceNotAvailableException means the SDK did not send the request.
                    case null when e is not ServiceNotAvailableException:
                        _owner = target;
                        break;
                }

                throw;
            }
        }

        public async Task<ConversationalQueryMetaData> EndAsync(EndOptions? options = null)
        {
            options ??= new EndOptions();
            var owner = GetOwnerOrThrow(ConversationalOperation.End);

            var metaData = await SendAsync(ConversationalStatements.EndChat, options.QueryOptions,
                new ConversationalRequest(ConversationalOperation.End, _chatId, owner)).ConfigureAwait(false);

            _closed = true;
            return metaData;
        }

        public ChatHandle GetHandle()
        {
            ThrowIfClosed();
            return new ChatHandle(_chatId, _owner);
        }

        // Lifecycle responses have no rows, so the result is disposed here.
        private async Task<ConversationalQueryMetaData> SendAsync(string statement, QueryOptions template,
            ConversationalRequest request)
        {
            using var result = await ConversationalQuery.SendAsync<object>(_context, statement, template, request)
                .ConfigureAwait(false);
            return result.MetaData;
        }

        private string GetOwnerOrThrow(ConversationalOperation operation)
        {
            ThrowIfClosed();
            ConversationalQuery.ThrowIfChatsUnavailable(_context);

            var owner = _owner;
            if (owner is not { Length: > 0 })
            {
                throw new ChatCurrentlyUnavailableException(
                    new ConversationalRequest(operation, _chatId, TargetNodeUuid: null), context: null);
            }

            return owner;
        }

        private void ThrowIfClosed()
        {
            if (_closed)
            {
                ThrowHelper.ThrowInvalidArgumentException($"Chat {_chatId} has ended and cannot be used.");
            }
        }

        // Null when the SDK did not parse a Query response with errors, for example on a client timeout.
        private static int? FirstQueryErrorCode(Exception e) =>
            e is CouchbaseException { Context: QueryErrorContext { Errors: { Count: > 0 } errors } }
                ? errors[0].Code
                : null;
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
