using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Utils;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Query.Conversational;

// Builds the SQL++ text of conversational statements. The chat ID is a request parameter, not part of the text.
internal static class ConversationalStatements
{
    public const string ResumeChat = "RESUME CHAT";

    public const string EndChat = "END CHAT";

    // The default encoder writes backticks as `, which makes keyspace paths hard to read.
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // Knowledge is sent only when set, because Query 8.0 rejects unknown WITH keys.
    public static string UsingAi(string prompt, IEnumerable<(string Bucket, string Scope, string Collection)> keyspaces,
        bool? knowledge = null, string? hint = null, bool? execute = null, ConversationalQueryOutput? output = null)
    {
        ThrowIfNoPrompt(prompt);
        return $"USING AI WITH {WriteWith(keyspaces, knowledge, hint, execute, output)} {prompt}";
    }

    public static string ChatTurn(string prompt, string? hint = null, bool? execute = null,
        ConversationalQueryOutput? output = null)
    {
        ThrowIfNoPrompt(prompt);
        return $"USING AI WITH {WriteWith(hint: hint, execute: execute, output: output)} {prompt}";
    }

    public static string BeginChat(IEnumerable<(string Bucket, string Scope, string Collection)> keyspaces,
        bool knowledge = false, TimeSpan? timeout = null)
    {
        long? timeoutSeconds = null;
        if (timeout is { } value)
        {
            if (value < TimeSpan.Zero)
            {
                ThrowHelper.ThrowInvalidArgumentException("The chat timeout cannot be negative.");
            }

            // Round up to whole seconds. Zero leaves the server default.
            var seconds = value.Ticks / TimeSpan.TicksPerSecond + (value.Ticks % TimeSpan.TicksPerSecond == 0 ? 0 : 1);
            timeoutSeconds = seconds == 0 ? null : seconds;
        }

        return "BEGIN CHAT WITH " + WriteWith(keyspaces, knowledge, timeoutSeconds: timeoutSeconds);
    }

    public static string PauseChat(bool? summarize = null) =>
        summarize is null ? "PAUSE CHAT" : "PAUSE CHAT WITH " + WriteWith(summarize: summarize);

    private static string WriteWith(IEnumerable<(string Bucket, string Scope, string Collection)>? keyspaces = null,
        bool? knowledge = null, string? hint = null, bool? execute = null, ConversationalQueryOutput? output = null,
        long? timeoutSeconds = null, bool? summarize = null)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            if (keyspaces is not null)
            {
                writer.WriteStartArray("keyspaces");
                var count = 0;
                foreach (var (bucket, scope, collection) in keyspaces)
                {
                    if (string.IsNullOrEmpty(bucket) || string.IsNullOrEmpty(scope) || string.IsNullOrEmpty(collection))
                    {
                        ThrowHelper.ThrowInvalidArgumentException("Keyspace bucket, scope and collection names cannot be null or empty.");
                    }

                    writer.WriteStringValue(
                        $"{bucket.EscapeIfRequired()}.{scope.EscapeIfRequired()}.{collection.EscapeIfRequired()}");
                    count++;
                }

                if (count == 0)
                {
                    ThrowHelper.ThrowInvalidArgumentException("At least one keyspace is required.");
                }

                writer.WriteEndArray();
            }

            if (knowledge is not null)
            {
                writer.WriteBoolean("knowledge", knowledge.Value);
            }

            if (!string.IsNullOrEmpty(hint))
            {
                writer.WriteString("hint", hint);
            }

            if (execute is not null)
            {
                writer.WriteBoolean("execute", execute.Value);
            }

            if (output is not null)
            {
                writer.WriteString("output", ToWireValue(output.Value));
            }

            if (timeoutSeconds is not null)
            {
                writer.WriteNumber("timeout", timeoutSeconds.Value);
            }

            if (summarize is not null)
            {
                writer.WriteBoolean("summarize", summarize.Value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }

    private static string ToWireValue(ConversationalQueryOutput output) => output switch
    {
        ConversationalQueryOutput.Sqlpp => "SQL",
        ConversationalQueryOutput.FtsSqlpp => "FTSSQL",
        ConversationalQueryOutput.JsUdf => "JSUDF",
        _ => throw new InvalidArgumentException($"Unknown conversational query output {output}.")
    };

    private static void ThrowIfNoPrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            ThrowHelper.ThrowInvalidArgumentException("The prompt cannot be null or empty.");
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
