using System;
using System.Net;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.N1QL;
using Couchbase.Views;

namespace Couchbase.Tests.Fakes
{
    internal class FakeServer : IServer
    {
        public FakeServer(IConnectionPool connectionPool, IViewClient viewClient, IQueryClient queryClient, IPEndPoint endPoint, IOStrategy strategy)
        {
            ConnectionPool = connectionPool;
            ViewClient = viewClient;
            QueryClient = queryClient;
            EndPoint = endPoint;
            Strategy = strategy;
        }

        public IConnectionPool ConnectionPool { get; private set; }

        public IViewClient ViewClient { get; private set; }

        public IQueryClient QueryClient { get; private set; }

        public IPEndPoint EndPoint { get; private set; }

        public IOStrategy Strategy { get; set; }

        public bool IsSecure { get; private set; }

        public bool IsDead { get; private set; }

        public Task SendAsync<T>(IOperation<T> operation)
        {
            return Strategy.ExecuteAsync(operation);
        }

        public IOperationResult<T> Send<T>(IOperation<T> operation)
        {
            throw new NotImplementedException();
        }

        public IViewResult<T> Send<T>(IViewQuery query)
        {
            throw new NotImplementedException();
        }

        public Task<IViewResult<T>> SendAsync<T>(IViewQuery query)
        {
            throw new NotImplementedException();
        }

        public IQueryResult<T> Send<T>(IQueryRequest queryRequest)
        {
            throw new NotImplementedException();
        }

        public Task<IQueryResult<T>> SendAsync<T>(IQueryRequest queryRequest)
        {
            throw new NotImplementedException();
        }

        public IQueryResult<T> Send<T>(string query)
        {
            throw new NotImplementedException();
        }

        public Task<IQueryResult<T>> SendAsync<T>(string query)
        {
            throw new NotImplementedException();
        }

        public string GetBaseViewUri(string name)
        {
            throw new NotImplementedException();
        }

        public string GetBaseQueryUri()
        {
            throw new NotImplementedException();
        }

        void IServer.MarkDead()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public IQueryResult<QueryPlan> Prepare(IQueryRequest toPrepare)
        {
            throw new NotImplementedException();
        }

        public IQueryResult<QueryPlan> Prepare(string statementToPrepare)
        {
            throw new NotImplementedException();
        }

        public IOperationResult Send(IOperation operation)
        {
            throw new NotImplementedException();
        }

        public Task SendAsync(IOperation operation)
        {
            return Strategy.ExecuteAsync(operation);
        }

        public bool IsDown
        {
            get { throw new NotImplementedException(); }
        }

        public void TakeOffline(bool isDown)
        {
            throw new NotImplementedException();
        }

        public Func<string, string, IOStrategy, ITypeTranscoder, ISaslMechanism> SaslFactory { get; set; }

        public void CreateSaslMechanismIfNotExists()
        {
            throw new NotImplementedException();
        }

        public bool IsMgmtNode
        {
            get { throw new NotImplementedException(); }
        }

        public bool IsQueryNode
        {
            get { throw new NotImplementedException(); }
        }

        public bool IsDataNode
        {
            get { throw new NotImplementedException(); }
        }

        public bool IsIndexNode
        {
            get { throw new NotImplementedException(); }
        }

        public bool IsViewNode
        {
            get { throw new NotImplementedException(); }
        }


        public bool SupportEnhancedDurability
        {
            get { throw new NotImplementedException(); }
        }

        public int InvalidateQueryCache()
        {
            throw new NotImplementedException();
        }
    }
}