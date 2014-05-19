using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Server.Providers;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.Tests.Fakes;

namespace Couchbase.Tests.IO.Strategies
{
    internal class FakeIOStrategy<K>: IOStrategy where K : class
    {
        private K _operation;
        private IConnectionPool _connectionPool = new FakeConnectionPool();

        public FakeIOStrategy(K operation)
        {
            _operation = operation;
        }

        public void RegisterListener(IConfigObserver observer)
        {
            throw new NotImplementedException();
        }

        public void UnRegisterListener(IConfigObserver observer)
        {
            throw new NotImplementedException();
        }

        public IOperationResult<T> Execute<T>(IOperation<T> operation)
        {
            operation = (IOperation<T>)_operation;
            return Task.Run(() => operation.GetResult()).Result;
        }

        public IPEndPoint EndPoint
        {
            get { return _connectionPool.EndPoint; }
        }

        public IConnectionPool ConnectionPool
        {
            get { return _connectionPool; }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public IOperationResult<T> Execute<T>(IOperation<T> operation, IConnection connection)
        {
            throw new NotImplementedException();
        }

        public Couchbase.Authentication.SASL.ISaslMechanism SaslMechanism
        {
            set { throw new NotImplementedException(); }
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