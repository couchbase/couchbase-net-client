using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.IO;
using Couchbase.IO.Strategies.Async;
using Couchbase.IO.Strategies.Awaitable;

namespace Couchbase.Tests.Helpers
{
    public static class ObjectFactory
    {
        internal static IOStrategy CreateIOStrategy(string server)
        {
            var connectionPool = new DefaultConnectionPool(new PoolConfiguration(), Server.GetEndPoint(server));
            var ioStrategy = new SocketAsyncStrategy(connectionPool);
            return ioStrategy;
        }

        internal static IOStrategy CreateIOStrategy(Node node)
        {
            var server = node.Hostname.Replace("8091", node.Ports.Direct.ToString());
            var connectionPool = new DefaultConnectionPool(new PoolConfiguration(), Server.GetEndPoint(server));
            var ioStrategy = new SocketAsyncStrategy(connectionPool);
            return ioStrategy;
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
