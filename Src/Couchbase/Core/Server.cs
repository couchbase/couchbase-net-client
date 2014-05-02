using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using Common.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Async;
using Couchbase.N1QL;
using Couchbase.Views;

namespace Couchbase.Core
{
    internal class Server : IServer
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IOStrategy _ioStrategy;
        private uint _viewPort = 8092;
        private uint _queryPort = 8093;
        private bool _disposed;
        private Node _nodeInfo;
        
        public Server(IOStrategy ioStrategy, Node node) : 
            this(ioStrategy, 
            new ViewClient(new HttpClient(), new JsonDataMapper()), 
            new QueryClient(new HttpClient(), new JsonDataMapper()),
            node)
        {
        }

        public Server(IOStrategy ioStrategy, IViewClient viewClient, IQueryClient queryClient, Node nodeInfo)
        {
            _ioStrategy = ioStrategy;
            ViewClient = viewClient;
            QueryClient = queryClient;
            _nodeInfo = nodeInfo;
        }

        public uint ViewPort
        {
            get { return _viewPort; }
            set { _viewPort = value; }
        }

        public uint QueryPort
        {
            get { return _queryPort; }
            set { _queryPort = value; }
        }

        public IPEndPoint EndPoint { get { return _ioStrategy.EndPoint; } }

        public IConnectionPool ConnectionPool { get { return _ioStrategy.ConnectionPool; } }

        public string HostName { get; set; }

        public uint DirectPort { get; private set; }

        public uint ProxyPort { get; private set; }

        public uint Replication { get; private set; }

        public bool Active { get; private set; }

        public bool Healthy { get; private set; }

        public IQueryClient QueryClient { get; private set; }

        public IViewClient ViewClient { get; private set; }

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
            var uri = new Uri(GetBaseQueryUri());
            return QueryClient.Query<T>(uri, query);
        }

        //note this should be cached
        public string GetBaseViewUri()
        {
            var uri = _nodeInfo.CouchApiBase;
            return uri.Replace("$HOST", "localhost");
        }

        public string GetBaseQueryUri()
        {
            var sb = new StringBuilder();
            sb.Append("http://");
            sb.Append(EndPoint.Address);
            sb.Append(":");
            sb.Append(QueryPort);
            sb.Append("/query");

            return sb.ToString();
        }

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
