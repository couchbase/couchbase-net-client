using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Async;
using Couchbase.IO.Strategies.Awaitable;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Strategies.EAP
{
// ReSharper disable once InconsistentNaming
    internal class EAPIOStrategy2 : IOStrategy
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IConnectionPool _connectionPool;
        private Func<IConnectionPool, IConnection> _factory;
        private volatile bool _disposed;
        private static readonly AutoResetEvent SendEvent = new AutoResetEvent(false);
        private static readonly AutoResetEvent ReceiveEvent = new AutoResetEvent(false);
        private ISaslMechanism _saslMechanism;

        public EAPIOStrategy2(IConnectionPool connectionPool)
            : this(connectionPool,
            new PlainTextMechanism("default", string.Empty))
        {
        }

        public EAPIOStrategy2(IConnectionPool connectionPool, ISaslMechanism saslMechanism)
        {
            _connectionPool = connectionPool;
            _saslMechanism = saslMechanism;
            _saslMechanism.IOStrategy = this;
        }

        public IOperationResult<T> Execute<T>(IOperation<T> operation, IConnection connection)
        {
            var buffer = operation.GetBuffer();
            var state = new OperationAsyncState();

            connection.Send(buffer, 0, buffer.Length, state);
            connection.Receive(state.Buffer, 0, buffer.Length, state);

            operation.Header = state.Header;
            operation.Body = state.Body;

            return operation.GetResult();
        }

        public IOperationResult<T> Execute<T>(IOperation<T> operation)
        {
            var connection = _connectionPool.Acquire();
            if (!connection.IsAuthenticated)
            {
               Authenticate(connection);
            }

            var buffer = operation.GetBuffer();
           // var state = new OperationAsyncState();

            var state = connection.State;
            connection.Send(buffer, 0, buffer.Length, state);
            connection.Receive(state.Buffer, 0, buffer.Length, state);

            operation.Header = state.Header;
            operation.Body = state.Body;

            _connectionPool.Release(connection);
            return operation.GetResult();
        }

        public IPEndPoint EndPoint
        {
            get { return _connectionPool.EndPoint; }
        }

        public IConnectionPool ConnectionPool
        {
            get { return _connectionPool; }
        }

        public ISaslMechanism SaslMechanism
        {
            set { _saslMechanism = value; }
        }

        private void Authenticate(IConnection connection)
        {
            if (_saslMechanism != null)
            {
                var result = _saslMechanism.Authenticate(connection);
                if (result)
                {
                    connection.IsAuthenticated = true;
                }
                else
                {
                    throw new AuthenticationException(_saslMechanism.Username);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        void Dispose(bool disposing)
        {
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
            if (_connectionPool != null)
            {
                _connectionPool.Dispose();
            }
        }

        ~EAPIOStrategy2()
        {
            Dispose(false);
        }
    }
}
