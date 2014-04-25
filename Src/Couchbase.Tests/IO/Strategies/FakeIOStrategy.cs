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

        public void RegisterListener(IConfigListener listener)
        {
            throw new NotImplementedException();
        }

        public void UnRegisterListener(IConfigListener listener)
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
