using Couchbase.Core.Compatibility;
using Couchbase.Query.Conversational;

#nullable enable

namespace Couchbase.Core.Exceptions.Query
{
    /// <summary>
    /// A chat operation could not reach the chat. The chat may still exist, for example paused or on another Query node.
    /// </summary>
    /// <remarks>
    /// <see cref="CouchbaseException.Context"/> is the <see cref="QueryErrorContext"/> when the Query service returned the error.
    /// It is null when the SDK failed before it sent the request.
    /// </remarks>
    [InterfaceStability(Level.Volatile)]
    public class ChatCurrentlyUnavailableException : CouchbaseException
    {
        internal ChatCurrentlyUnavailableException(ConversationalRequest request, QueryErrorContext? context)
            : this(request.ChatId ?? string.Empty, request.TargetNodeUuid,
                request.Operation.ToString().ToLowerInvariant(), context)
        {
        }

        private ChatCurrentlyUnavailableException(string chatId, string? ownerNodeUuid, string operation,
            QueryErrorContext? context)
            : base($"Chat {chatId} is currently unavailable for {operation}. " +
                   "The chat may still exist, for example paused or on another Query node. ResumeAsync may recover it.")
        {
            ChatId = chatId;
            OwnerNodeUuid = ownerNodeUuid;
            Operation = operation;
            Context = context;
        }

        /// <summary>
        /// The ID of the chat.
        /// </summary>
        public string ChatId { get; }

        /// <summary>
        /// The node UUID of the Query node that owns the chat, if known.
        /// </summary>
        public string? OwnerNodeUuid { get; }

        /// <summary>
        /// The chat operation that failed, for example <c>ask</c>, <c>pause</c> or <c>end</c>.
        /// </summary>
        public string Operation { get; }

        // These operations act on a chat that lives on one Query node.
        internal static bool AppliesTo(ConversationalOperation operation) =>
            operation is ConversationalOperation.Ask or ConversationalOperation.Pause or ConversationalOperation.End;
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
