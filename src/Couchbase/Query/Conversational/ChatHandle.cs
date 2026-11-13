using System.Text.Json;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Exceptions;

#nullable enable

namespace Couchbase.Query.Conversational;

/// <summary>
/// The state needed to continue a chat from another <see cref="IChat"/> instance or another process.
/// It never contains credentials or model settings.
/// </summary>
/// <remarks>
/// Apps may read <see cref="ChatId"/>. The handle also keeps the owner node, which the SDK uses for routing.
/// </remarks>
[InterfaceStability(Level.Volatile)]
public sealed record ChatHandle
{
    private const string InvalidHandleMessage =
        "The chat handle must be a JSON object with a string chatId and a string or null owner.";

    internal ChatHandle(string chatId, string? owner)
    {
        ChatId = chatId;
        Owner = owner;
    }

    /// <summary>
    /// The chat ID that Query assigned.
    /// </summary>
    public string ChatId { get; }

    /// <summary>
    /// The node UUID of the Query node that owns the chat, or null when no owner is known.
    /// </summary>
    internal string? Owner { get; }

    /// <summary>
    /// Writes the handle as <c>{"chatId":"...","owner":"..."}</c>. The owner is <c>null</c> when it is unknown.
    /// </summary>
    /// <returns>The JSON text.</returns>
    public string ToJson() =>
        JsonSerializer.Serialize(new ChatHandleDto { ChatId = ChatId, Owner = Owner },
            QuerySerializerContext.Default.ChatHandleDto);

    /// <summary>
    /// Reads a handle written by <see cref="ToJson"/>. Unknown fields are ignored.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The handle.</returns>
    /// <exception cref="InvalidArgumentException">
    /// The text is not a JSON object, or <c>chatId</c> is not a non-empty string, or <c>owner</c> is not a string or null.
    /// </exception>
    public static ChatHandle FromJson(string json)
    {
        if (json is null)
        {
            throw new InvalidArgumentException("The chat handle cannot be null.");
        }

        ChatHandleDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize(json, QuerySerializerContext.Default.ChatHandleDto);
        }
        catch (JsonException e)
        {
            throw new InvalidArgumentException(InvalidHandleMessage, e);
        }

        if (dto is null)
        {
            throw new InvalidArgumentException(InvalidHandleMessage);
        }

        if (dto.ChatId is not { Length: > 0 } chatId)
        {
            throw new InvalidArgumentException("The chat handle field chatId must be a non-empty string.");
        }

        return new ChatHandle(chatId, dto.Owner);
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
