using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Exceptions;
using Couchbase.KeyValue;

#nullable enable

namespace Couchbase.Query.Conversational;

/// <summary>
/// Turns natural language prompts into SQL++, as one-shot queries or as chats that keep history.
/// </summary>
[InterfaceStability(Level.Volatile)]
public interface IConversationalQuery
{
    /// <summary>
    /// Sends one prompt. Query generates a statement and runs it if it is eligible.
    /// </summary>
    /// <typeparam name="T">The type of each row.</typeparam>
    /// <param name="prompt">The natural language prompt.</param>
    /// <param name="keyspaces">The collections the prompt is about. At least one is required.</param>
    /// <param name="options">Optional arguments.</param>
    /// <returns>The generated statement and the rows, if the statement ran.</returns>
    /// <exception cref="InvalidArgumentException">The prompt or the keyspace list is empty.</exception>
    Task<IConversationalQueryResult<T>> QueryAsync<T>(string prompt, IEnumerable<ICouchbaseCollection> keyspaces,
        ConversationalQueryOptions? options = null);

    /// <summary>
    /// Starts a chat on one Query node. The returned <see cref="IChat"/> keeps the model and credential from the options.
    /// </summary>
    /// <param name="keyspaces">The collections the chat is about. At least one is required.</param>
    /// <param name="options">Optional arguments.</param>
    /// <returns>The new chat.</returns>
    /// <exception cref="InvalidArgumentException">The keyspace list is empty.</exception>
    /// <exception cref="FeatureNotAvailableException">
    /// The cluster does not support chats, or Query nodes share one address, for example with Capella Private Link.
    /// </exception>
    Task<IChat> BeginChatAsync(IEnumerable<ICouchbaseCollection> keyspaces, BeginChatOptions? options = null);

    /// <summary>
    /// Creates an <see cref="IChat"/> for an existing chat. It does no network I/O.
    /// </summary>
    /// <param name="handle">The handle of the chat, from <see cref="IChat.GetHandle"/>.</param>
    /// <param name="options">The model and credential for the new instance.</param>
    /// <returns>A chat that uses the routing state of the handle.</returns>
    IChat AttachChat(ChatHandle handle, AttachChatOptions? options = null);
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
