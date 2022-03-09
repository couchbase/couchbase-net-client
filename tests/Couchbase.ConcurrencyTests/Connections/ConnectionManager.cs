using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.ConcurrencyTests.Connections
{
    internal static class ConnectionManager
    {
        private static ConcurrentDictionary<string, ICluster> Connections = new();

        public static ICluster GetCluster(string connectionId) => Connections[connectionId];
        public static void AddConnection(string connectionId, ICluster cluster) => Connections.AddOrUpdate(connectionId, cluster, (k, v) => v);
    }
}
