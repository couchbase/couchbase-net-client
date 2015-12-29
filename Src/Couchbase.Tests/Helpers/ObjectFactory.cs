using System.Globalization;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.IO;
using Couchbase.IO.Services;
using Couchbase.Utils;

namespace Couchbase.Tests.Helpers
{
    public static class ObjectFactory
    {
        internal static IIOService CreateIOService(string server)
        {
            var connectionPool = new ConnectionPool<Connection>(new PoolConfiguration(), UriExtensions.GetEndPoint(server));
            var ioService = new PooledIOService(connectionPool);
            return ioService;
        }

        internal static IIOService CreateIOService(Node node)
        {
            var server = node.Hostname.Replace("8091", node.Ports.Direct.ToString(CultureInfo.InvariantCulture));
            var connectionPool = new ConnectionPool<Connection>(new PoolConfiguration(), UriExtensions.GetEndPoint(server));
            var ioService = new PooledIOService(connectionPool);
            return ioService;
        }

        internal static IIOService CreateIOService(INodeAdapter node)
        {
            var server = node.Hostname.Replace("8091", node.KeyValue.ToString(CultureInfo.InvariantCulture));
            var connectionPool = new ConnectionPool<Connection>(new PoolConfiguration(), UriExtensions.GetEndPoint(server));
            var ioService = new PooledIOService(connectionPool);
            return ioService;
        }
    }
}

#region [ License information          ]

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

#endregion
