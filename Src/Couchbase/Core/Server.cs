using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using Common.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Async;
using Couchbase.N1QL;
using Couchbase.Views;

namespace Couchbase.Core
{
    internal class Server : IServer
    {
        //todo review this as a best practice
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IOStrategy _ioStrategy;
        private readonly ISaslMechanism _saslMechanism;
        private bool _disposed;
        
        public Server(IOStrategy ioStrategy) : 
            this(ioStrategy, 
            new ViewClient(new HttpClient(), new JsonDataMapper()), 
            new QueryClient(new HttpClient(), new JsonDataMapper()),
            new PlainTextMechanism(ioStrategy))
        {
        }

        public Server(IOStrategy ioStrategy, IViewClient viewClient)
        {
            _ioStrategy = ioStrategy;
            ViewClient = viewClient;
        }

        public Server(IOStrategy ioStrategy, IViewClient viewClient, IQueryClient queryClient)
        {
            _ioStrategy = ioStrategy;
            ViewClient = viewClient;
            QueryClient = queryClient;
        }

        public Server(IOStrategy ioStrategy, IViewClient viewClient, IQueryClient queryClient, ISaslMechanism saslMechanism)
        {
            _ioStrategy = ioStrategy;
            ViewClient = viewClient;
            QueryClient = queryClient;
            _saslMechanism = saslMechanism;
        }

        public void Authenticate(string username, string password)
        {
            ConnectionPool.Initialize();

            var isAuthenticated = _saslMechanism.Authenticate(username, password);
            if (isAuthenticated) return;
            var message = string.Format("Could not authenticate: {0}. See logs for details.", username);
            throw new AuthenticationException(message);
        }

        public IPEndPoint EndPoint { get { return _ioStrategy.EndPoint; } }

        public IConnectionPool ConnectionPool { get { return _ioStrategy.ConnectionPool; } }

        public uint DirectPort { get; private set; }

        public uint ProxyPort { get; private set; }

        public uint Replication { get; private set; }

        public bool Active { get; private set; }

        public bool Healthy { get; private set; }

        public IOperationResult<T> Send<T>(IOperation<T> operation)
        {
            return _ioStrategy.Execute(operation);
        }

        public IViewResult<T> Send<T>(IViewQuery query)
        {
            return ViewClient.Execute<T>(query);
        }

        IQueryResult<T> IServer.Send<T>(string query)
        {
            //TODO make right - this isn't.
            var uri = new Uri(string.Concat("http://", EndPoint.Address, ":", 8093, "/query"));
            return QueryClient.Query<T>(uri, query);
        }

        public IQueryClient QueryClient { get; private set; }

        public IViewClient ViewClient { get; private set; }

        public static IPEndPoint GetEndPoint(string server)
        {
            const int maxSplits = 2;
            var address = server.Split(':');
            if (address.Count() != maxSplits)
            {
                throw new ArgumentException("server");
            }
            IPAddress ipAddress;
            if (!IPAddress.TryParse(address[0], out ipAddress))
            {
                throw new ArgumentException("ipAddress");
            }
            int port;
            if (!int.TryParse(address[1], out port))
            {
                throw new ArgumentException("port");
            }
            return new IPEndPoint(ipAddress, port);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        void Dispose(bool disposing)
        {
            if (_disposed)
            {
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
                
                _ioStrategy.Dispose();
                _disposed = true;
            }
        }

        ~Server()
        {
            Dispose(false);
        }
    }
}
