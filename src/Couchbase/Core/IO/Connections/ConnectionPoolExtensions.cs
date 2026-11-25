using System;
using System.Collections.Generic;
using Couchbase.Core.Exceptions.KeyValue;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    internal static class ConnectionPoolExtensions
    {
        /// <summary>
        /// Reads the connections of the pool. Returns false when the pool is disposed.
        /// </summary>
        /// <remarks>
        /// A reader that runs while a reconcile disposes a node uses this. The channel and data flow pools throw
        /// <see cref="SocketNotAvailableException"/> when disposed, so the send path can retry on another node.
        /// </remarks>
        public static bool TryGetConnections(this IConnectionPool pool, out IEnumerable<IConnection> connections)
        {
            try
            {
                connections = pool.GetConnections();
                return true;
            }
            catch (Exception e) when (e is SocketNotAvailableException or ObjectDisposedException)
            {
                connections = [];
                return false;
            }
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
