using System;
using System.Collections.Generic;
using System.Net;
using Couchbase.Configuration.Client;

namespace Couchbase.IO
{
    interface IConnectionPool : IDisposable
    {
        IConnection Acquire();

        void Release(IConnection connection);

        void Initialize();

        int Count();

        PoolConfiguration Configuration { get; }

        IPEndPoint EndPoint { get; set; }

        IEnumerable<IConnection> Connections { get; }
    }
}
