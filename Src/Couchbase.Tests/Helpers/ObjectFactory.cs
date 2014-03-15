using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.IO;
using Couchbase.IO.Strategies.Awaitable;

namespace Couchbase.Tests.Helpers
{
    public static class ObjectFactory
    {
        internal static IOStrategy CreateIOStrategy(string server)
        {
            var connectionPool = new DefaultConnectionPool(new PoolConfiguration(), Server.GetEndPoint(server));
            var ioStrategy = new AwaitableIOStrategy(connectionPool, null);
            return ioStrategy;
        }

        internal static IOStrategy CreateIOStrategy(Node node)
        {
            var server = node.Hostname.Replace("8091", node.Ports.Direct.ToString());
            var connectionPool = new DefaultConnectionPool(new PoolConfiguration(), Server.GetEndPoint(server));
            var ioStrategy = new AwaitableIOStrategy(connectionPool, null);
            return ioStrategy;
        }
    }
}
