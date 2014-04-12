using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Server.Providers;
using Couchbase.IO;
using Couchbase.IO.Operations;

namespace Couchbase.Tests.IO.Strategies
{
    internal class FakeIOStrategy<K>: IOStrategy where K : class
    {
        private K _operation;

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

        public Task<IOperationResult<T>> ExecuteAsync<T>(IOperation<T> operation)
        {
            operation = (IOperation<T>)_operation;
            return Task.Run(() => operation.GetResult());
        }

        public IOperationResult<T> Execute<T>(IOperation<T> operation)
        {
            throw new NotImplementedException();
        }

        public IPEndPoint EndPoint
        {
            get { throw new NotImplementedException(); }
        }

        public IConnectionPool ConnectionPool
        {
            get { throw new NotImplementedException(); }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Task<IOperationResult<T>> ExecuteAsync<T>(IOperation<T> operation, IConnection connection)
        {
            throw new NotImplementedException();
        }
    }
}
