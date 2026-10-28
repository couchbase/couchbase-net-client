using System;
using Couchbase.Core.Compatibility;

#nullable enable

namespace Couchbase.Query.Conversational
{
    /// <summary>
    /// Gives access to conversational queries, which turn natural language prompts into SQL++.
    /// </summary>
    [InterfaceStability(Level.Volatile)]
    public interface IConversationalQueryRequester
    {
        /// <summary>
        /// Runs one-shot conversational queries and chats.
        /// </summary>
        IConversationalQuery ConversationalQuery
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
            => throw new NotImplementedException(nameof(ConversationalQuery));
#else
        // legacy platforms don't support default interface implementations
        { get; }
#endif
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
