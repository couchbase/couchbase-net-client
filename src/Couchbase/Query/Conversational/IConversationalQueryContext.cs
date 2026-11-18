using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core;

#nullable enable

namespace Couchbase.Query.Conversational;

internal interface IConversationalQueryContext
{
    // Applies the default query timeout and runs the request through the retry orchestrator.
    Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions options);

    IEnumerable<IClusterNode> Nodes { get; }

    bool EnableTls { get; }

    // Null until the cluster config is known.
    bool? HasConversationalQueryCapability { get; }

    void ThrowIfNotBootstrapped();
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
