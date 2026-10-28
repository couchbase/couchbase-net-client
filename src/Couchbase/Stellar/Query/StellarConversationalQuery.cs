#if NETCOREAPP3_1_OR_GREATER
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Couchbase.Query.Conversational;
using Couchbase.Utils;

namespace Couchbase.Stellar.Query;

#nullable enable

// AttachChat throws too, because no chat operation can reach Query over couchbase2.
internal sealed class StellarConversationalQuery : IConversationalQuery
{
    public static StellarConversationalQuery Instance { get; } = new();

    private StellarConversationalQuery()
    {
    }

    public Task<IConversationalQueryResult<T>> QueryAsync<T>(string prompt, IEnumerable<ICouchbaseCollection> keyspaces,
        ConversationalQueryOptions? options = null) =>
        throw ThrowHelper.ThrowFeatureNotAvailableException("Conversational query", "Protostellar");

    public Task<IChat> BeginChatAsync(IEnumerable<ICouchbaseCollection> keyspaces, BeginChatOptions? options = null) =>
        throw ThrowHelper.ThrowFeatureNotAvailableException("Conversational query", "Protostellar");

    public IChat AttachChat(ChatHandle handle, AttachChatOptions? options = null) =>
        throw ThrowHelper.ThrowFeatureNotAvailableException("Conversational query", "Protostellar");
}
#endif


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
