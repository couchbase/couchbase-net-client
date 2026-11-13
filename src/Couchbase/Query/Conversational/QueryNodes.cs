#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Couchbase.Core;
using Couchbase.Utils;

namespace Couchbase.Query.Conversational;

internal static class QueryNodes
{
    public static bool TryResolve(IEnumerable<IClusterNode> nodes, string? nodeUuid,
        [NotNullWhen(true)] out IClusterNode? queryNode)
    {
        if (!string.IsNullOrEmpty(nodeUuid))
        {
            foreach (var node in nodes)
            {
                if (node.HasQuery && string.Equals(node.NodeUuid, nodeUuid, StringComparison.Ordinal)
                    && node.QueryUri is not null)
                {
                    queryNode = node;
                    return true;
                }
            }
        }

        queryNode = null;
        return false;
    }

    public static bool TrySelectRandom(IEnumerable<IClusterNode> nodes,
        [NotNullWhen(true)] out string? nodeUuid, [NotNullWhen(true)] out Uri? queryUri)
    {
        // Reading QueryUri marks the node active for diagnostics, so only read it on the chosen node.
        var node = nodes.RandomOrDefault(static x => x.HasQuery && !string.IsNullOrEmpty(x.NodeUuid));
        var uri = node?.QueryUri;
        if (node is null || uri is null)
        {
            nodeUuid = null;
            queryUri = null;
            return false;
        }

        nodeUuid = node.NodeUuid;
        queryUri = uri;
        return true;
    }

    // Query nodes behind one load balancer share a host and port, so a chat cannot target its owner.
    public static bool HasSharedQueryEndpoint(IEnumerable<IClusterNode> nodes, bool enableTls)
    {
        var uuidByEndpoint = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            var uuid = node.NodeUuid;
            var adapter = node.NodesAdapter;
            if (!node.HasQuery || string.IsNullOrEmpty(uuid) || adapter is null)
            {
                continue;
            }

            var endpoint = $"{adapter.Hostname}:{(enableTls ? adapter.N1QlSsl : adapter.N1Ql)}";
            if (uuidByEndpoint.TryGetValue(endpoint, out var owner))
            {
                if (!string.Equals(owner, uuid, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            else
            {
                uuidByEndpoint.Add(endpoint, uuid);
            }
        }

        return false;
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
