using System.Threading.Tasks;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.Query;

#nullable enable

namespace Couchbase.Query.Conversational;

/// <summary>
/// A chat that lives on one Query node. Each prompt uses the history of the earlier prompts.
/// </summary>
/// <remarks>
/// The chat keeps the node that owns it. Ask, pause and end fail with <see cref="ChatCurrentlyUnavailableException"/>
/// when no owner is known. After <see cref="EndAsync"/> succeeds, every method throws <see cref="InvalidArgumentException"/>.
/// Ask, pause, resume and end throw <see cref="FeatureNotAvailableException"/> when the cluster does not support chats,
/// or when Query nodes share one address, for example with Capella Private Link.
/// </remarks>
[InterfaceStability(Level.Volatile)]
public interface IChat
{
    /// <summary>
    /// Sends a prompt to the chat.
    /// </summary>
    /// <typeparam name="T">The type of each row.</typeparam>
    /// <param name="prompt">The natural language prompt.</param>
    /// <param name="options">Optional arguments.</param>
    /// <returns>The generated statement and the rows, if the statement ran.</returns>
    /// <exception cref="InvalidArgumentException">The prompt is empty.</exception>
    Task<IConversationalQueryResult<T>> AskAsync<T>(string prompt, AskOptions? options = null);

    /// <summary>
    /// Saves the chat and removes it from its Query node. <see cref="ResumeAsync"/> can restore it on any Query node.
    /// </summary>
    /// <param name="options">Optional arguments.</param>
    /// <returns>The metadata of the response.</returns>
    Task<ConversationalQueryMetaData> PauseAsync(PauseOptions? options = null);

    /// <summary>
    /// Restores a paused chat. It goes to the known owner if it is in the cluster map, otherwise to a random Query node.
    /// </summary>
    /// <param name="options">Optional arguments.</param>
    /// <returns>The metadata of the response.</returns>
    Task<ConversationalQueryMetaData> ResumeAsync(ResumeOptions? options = null);

    /// <summary>
    /// Ends the chat. After it succeeds, this instance cannot be used.
    /// It does not remove a paused chat, so call <see cref="ResumeAsync"/> first.
    /// </summary>
    /// <param name="options">Optional arguments.</param>
    /// <returns>The metadata of the response.</returns>
    Task<ConversationalQueryMetaData> EndAsync(EndOptions? options = null);

    /// <summary>
    /// Returns a snapshot of the chat ID and the owner node, to continue the chat with <see cref="IConversationalQuery.AttachChat"/>.
    /// </summary>
    /// <returns>A new <see cref="ChatHandle"/>.</returns>
    ChatHandle GetHandle();
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
