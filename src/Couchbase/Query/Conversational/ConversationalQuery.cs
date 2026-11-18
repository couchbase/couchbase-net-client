using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.KeyValue;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Query.Conversational;

internal sealed class ConversationalQuery : IConversationalQuery
{
    private readonly IConversationalQueryContext _context;

    public ConversationalQuery(IConversationalQueryContext context)
    {
        _context = context;
    }

    public async Task<IConversationalQueryResult<T>> QueryAsync<T>(string prompt,
        IEnumerable<ICouchbaseCollection> keyspaces, ConversationalQueryOptions? options = null)
    {
        options ??= new ConversationalQueryOptions();
        var model = ModelParameters.Create(options.ModelValue, options.CredentialValue);
        var statement = ConversationalStatements.UsingAi(prompt, ToKeyspaces(keyspaces), options.UseKnowledgeValue,
            options.HintValue, options.ExecuteValue, options.OutputValue);

        return await SendAsync<T>(_context, statement, options.QueryOptions,
            new ConversationalRequest(ConversationalOperation.Query, ChatId: null, TargetNodeUuid: null, model))
            .ConfigureAwait(false);
    }

    public async Task<IChat> BeginChatAsync(IEnumerable<ICouchbaseCollection> keyspaces,
        BeginChatOptions? options = null)
    {
        options ??= new BeginChatOptions();
        var model = ModelParameters.Create(options.ModelValue, options.CredentialValue);
        var statement = ConversationalStatements.BeginChat(ToKeyspaces(keyspaces), options.UseKnowledgeValue,
            options.InactivityTimeoutValue);

        ThrowIfChatsUnavailable(_context);
        var target = SelectRandomNode(_context);

        // The model is not sent. The chat keeps it for later prompts.
        using var result = await SendAsync<object>(_context, statement, options.QueryOptions,
            new ConversationalRequest(ConversationalOperation.BeginChat, ChatId: null, target)).ConfigureAwait(false);

        var chatId = result.ChatId;
        if (chatId is not { Length: > 0 })
        {
            throw new CouchbaseException("Query did not return a chat ID for BEGIN CHAT.");
        }

        return new Chat(_context, chatId, target, model);
    }

    public IChat AttachChat(ChatHandle handle, AttachChatOptions? options = null)
    {
        if (handle is null)
        {
            ThrowHelper.ThrowInvalidArgumentException("The chat handle cannot be null.");
        }

        var model = ModelParameters.Create(options?.ModelValue, options?.CredentialValue);
        return new Chat(_context, handle.ChatId, handle.Owner, model);
    }

    // Every request sends a copy of the user options, so the options can be reused.
    internal static async Task<ConversationalQueryResult<T>> SendAsync<T>(IConversationalQueryContext context,
        string statement, QueryOptions template, ConversationalRequest request)
    {
        var options = template.Clone();
        options.Conversational = request;
        return new ConversationalQueryResult<T>(await context.QueryAsync<T>(statement, options).ConfigureAwait(false));
    }

    internal static void ThrowIfChatsUnavailable(IConversationalQueryContext context)
    {
        context.ThrowIfNotBootstrapped();

        if (context.HasConversationalQueryCapability == false)
        {
            throw new FeatureNotAvailableException(
                "This cluster does not support conversational query chats. They need Couchbase Server 8.5 or later.");
        }

        if (QueryNodes.HasSharedQueryEndpoint(context.Nodes, context.EnableTls))
        {
            throw new FeatureNotAvailableException(
                "Conversational query chats need to reach each Query node directly. They are not available when " +
                "Query nodes share one address behind a load balancer, for example Capella Private Link.");
        }
    }

    internal static string SelectRandomNode(IConversationalQueryContext context)
    {
        if (!QueryNodes.TrySelectRandom(context.Nodes, out var nodeUuid, out _))
        {
            throw new ServiceNotAvailableException(ServiceType.Query);
        }

        return nodeUuid;
    }

    private static List<(string Bucket, string Scope, string Collection)> ToKeyspaces(
        IEnumerable<ICouchbaseCollection> collections)
    {
        if (collections is null)
        {
            ThrowHelper.ThrowInvalidArgumentException("The keyspaces cannot be null.");
        }

        var keyspaces = new List<(string Bucket, string Scope, string Collection)>();
        foreach (var collection in collections)
        {
            if (collection is null)
            {
                ThrowHelper.ThrowInvalidArgumentException("A keyspace cannot be null.");
            }

            keyspaces.Add((collection.Scope.Bucket.Name, collection.Scope.Name, collection.Name));
        }

        return keyspaces;
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
