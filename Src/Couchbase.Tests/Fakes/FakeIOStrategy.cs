using System;
using System.Net;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.IO;
using Couchbase.IO.Operations;

namespace Couchbase.Tests.Fakes
{
    internal class FakeIOStrategy : IOStrategy
    {
        public FakeIOStrategy(IPEndPoint endPoint, IConnectionPool connectionPool, bool isSecure)
        {
            EndPoint = endPoint;
            ConnectionPool = connectionPool;
            IsSecure = isSecure;
        }

        public IPEndPoint EndPoint { get; private set; }

        public IConnectionPool ConnectionPool { get; private set; }

        public ISaslMechanism SaslMechanism { get; set; }

        public bool IsSecure { get; private set; }

        public IOperationResult<T> Execute<T>(IOperation<T> operation, IConnection connection)
        {
            throw new NotImplementedException();
        }

        public IOperationResult<T> Execute<T>(IOperation<T> operation)
        {
            throw new NotImplementedException();
        }

        public Task ExecuteAsync<T>(IOperation<T> operation, IConnection connection)
        {
            throw new NotImplementedException();
        }

        public async Task ExecuteAsync<T>(IOperation<T> operation)
        {
            var buffer = await operation.WriteAsync();
            var connection = ConnectionPool.Acquire();
            connection.SendAsync(buffer, operation.Completed);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}