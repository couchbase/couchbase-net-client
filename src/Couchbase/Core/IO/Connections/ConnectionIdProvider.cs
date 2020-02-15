using System.Threading;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Provides unique connection IDs for <see cref="IConnection"/> implementations.
    /// </summary>
    internal static class ConnectionIdProvider
    {
        private static long _connectionId;

        /// <summary>
        /// Provides unique connection IDs for <see cref="IConnection"/> implementations.
        /// </summary>
        /// <returns>A unique connection ID.</returns>
        public static ulong GetNextId() => (ulong) Interlocked.Increment(ref _connectionId);
    }
}
