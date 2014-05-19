using Couchbase.Core;
using Couchbase.IO;
using System;
using System.Collections.Generic;

namespace Couchbase.Tests.Fakes
{
    internal class FakeConnectionPool : IConnectionPool
    {
        private IEnumerable<IConnection> _connections = new List<IConnection>();

        public FakeConnectionPool()
        {
            EndPoint = Server.GetEndPoint("127.0.01:8091");
        }

        public IConnection Acquire()
        {
            throw new NotImplementedException();
        }

        public void Release(IConnection connection)
        {
            throw new NotImplementedException();
        }

        public int Count()
        {
            throw new NotImplementedException();
        }

        public Couchbase.Configuration.Client.PoolConfiguration Configuration
        {
            get { throw new NotImplementedException(); }
        }

        public System.Net.IPEndPoint EndPoint
        {
            get;
            set;
        }

        public void Initialize()
        {
        }

        public IEnumerable<IConnection> Connections
        {
            get { return _connections; }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
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