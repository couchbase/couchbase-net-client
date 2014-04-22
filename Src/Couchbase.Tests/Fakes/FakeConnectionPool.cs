using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.IO;
using NUnit.Framework;

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
            get; set; }

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
