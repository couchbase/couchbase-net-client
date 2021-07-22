using System;
using System.Threading;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Provides unique connection IDs for <see cref="IConnection"/> implementations.
    /// </summary>
    internal static class ConnectionIdProvider
    {
        private static readonly Random Random = new();
        private static long _connectionId;

        /// <summary>
        /// Provides unique connection IDs for <see cref="IConnection"/> implementations.
        /// </summary>
        /// <returns>A unique connection ID.</returns>
        public static ulong GetNextId() => (ulong) Interlocked.Increment(ref _connectionId);

        public static ulong GetRandomLong()
        {
            var bytes = new byte[8];
            lock (Random)
            {
                Random.NextBytes(bytes);
            }

            return BitConverter.ToUInt64(bytes, 0);
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
