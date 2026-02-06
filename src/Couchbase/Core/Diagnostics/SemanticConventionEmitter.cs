using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif

namespace Couchbase.Core.Diagnostics;

internal static class SemanticConventionEmitter
{
    // Literal mapping: legacy attribute key -> modern attribute key
#if NET8_0_OR_GREATER
    private static readonly FrozenDictionary<string, string> LegacyToModern =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
#else
    private static readonly Dictionary<string, string> LegacyToModern =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
#endif
            ["db.system"] = "db.system.name",
            ["db.couchbase.cluster_name"] = "couchbase.cluster.name",
            ["db.couchbase.cluster_uuid"] = "couchbase.cluster.uuid",
            ["db.name"] = "db.namespace",
            ["db.couchbase.scope"] = "couchbase.scope.name",
            ["db.couchbase.collection"] = "couchbase.collection.name",
            ["db.couchbase.retries"] = "couchbase.retries",
            ["db.couchbase.durability"] = "couchbase.durability",
            ["db.statement"] = "db.query.text",
            ["db.operation"] = "db.operation.name",
            ["outcome"] = "error.type",
            ["net.transport"] = "network.transport",
            ["net.host.name"] = "",  // an empty string will not emit anything.
            ["net.host.port"] = "",
            ["net.peer.name"] = "server.address",
            ["net.peer.port"] = "server.port",
            ["db.couchbase.local_id"] = "couchbase.local_id",
            ["db.couchbase.operation_id"] = "couchbase.operation_id",
            ["db.couchbase.service"] = "couchbase.service",
#if NET8_0_OR_GREATER
        }.ToFrozenDictionary(StringComparer.Ordinal);
#else
        };
#endif

    /// <summary>
    /// Emits an attribute using the appropriate semantic convention key(s).
    /// The <paramref name="state"/> parameter is forwarded to the callback, allowing
    /// callers to use a <c>static</c> lambda for zero per-call allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void EmitAttribute<TState, T>(
        ObservabilitySemanticConvention mode,
        string key,
        T value,
        TState state,
        Action<TState, string, T> setAttribute)
    {
        // Fast path: Legacy mode always emits the original key as-is,
        // regardless of whether it's in the mapping or not.
        if (mode == ObservabilitySemanticConvention.Legacy)
        {
            setAttribute(state, key, value);
            return;
        }

        // If the key isn't mapped, treat it as "neutral": emit as-is in all modes.
        if (!LegacyToModern.TryGetValue(key, out var modernKey))
        {
            setAttribute(state, key, value);
            return;
        }

        switch (mode)
        {
            case ObservabilitySemanticConvention.Modern:
                if (modernKey.Length == 0) return;
                setAttribute(state, modernKey, value);
                return;

            case ObservabilitySemanticConvention.Both:
                setAttribute(state, key, value);
                if (modernKey.Length == 0) return;
                setAttribute(state, modernKey, value);
                return;

            default:
                // In case we grow our enum, default to legacy to preserve compatibility.
                setAttribute(state, key, value);
                return;
        }
    }
}
/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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
