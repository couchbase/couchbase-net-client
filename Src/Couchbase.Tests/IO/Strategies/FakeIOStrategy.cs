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
    internal class FakeIOStrategy<T> : IOStrategy
    {
        private Func<Task<IOperationResult<T>>> _operation;

        void LoadFakeOperation(Func<Task<IOperationResult<T>>> operation)
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

        public async Task<IOperationResult<T>> ExecuteAsync<T>(IOperation<T> operation)
        {
            var result = (IOperationResult<T>) await _operation();
            return result;
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
    }
}
