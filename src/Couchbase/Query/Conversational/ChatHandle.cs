using System.IO;
using System.Text;
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
/// Apps may read <see cref="ChatId"/>. <see cref="Owner"/> is for SDK routing only.
/// </remarks>
[InterfaceStability(Level.Volatile)]
public sealed record ChatHandle
{
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
    public string? Owner { get; }

    /// <summary>
    /// Writes the handle as <c>{"chatId":"...","owner":"..."}</c>. The owner is <c>null</c> when it is unknown.
    /// </summary>
    /// <returns>The JSON text.</returns>
    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("chatId", ChatId);
            writer.WriteString("owner", Owner);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }

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
            throw Invalid("The chat handle cannot be null.");
        }

        string? chatId = null;
        string? owner = null;
        var hasOwner = false;
        try
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                throw Invalid("The chat handle must be a JSON object.");
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals("chatId"u8))
                {
                    reader.Read();
                    chatId = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                    if (string.IsNullOrEmpty(chatId))
                    {
                        throw Invalid("The chat handle field chatId must be a non-empty string.");
                    }
                }
                else if (reader.ValueTextEquals("owner"u8))
                {
                    reader.Read();
                    if (reader.TokenType is not (JsonTokenType.String or JsonTokenType.Null))
                    {
                        throw Invalid("The chat handle field owner must be a string or null.");
                    }

                    owner = reader.GetString();
                    hasOwner = true;
                }
                else
                {
                    reader.Skip();
                }
            }

            // Fails on content after the object.
            reader.Read();
        }
        catch (JsonException e)
        {
            throw new InvalidArgumentException("The chat handle is not valid JSON.", e);
        }

        if (chatId is null || !hasOwner)
        {
            throw Invalid("The chat handle must have the fields chatId and owner.");
        }

        return new ChatHandle(chatId, owner);
    }

    private static InvalidArgumentException Invalid(string message) => new(message);
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
