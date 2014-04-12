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

        int Count();

        PoolConfiguration Configuration { get; }

        IPEndPoint EndPoint { get; set; }

        void Initialize();

        IEnumerable<IConnection> Connections { get; }
    }
}
