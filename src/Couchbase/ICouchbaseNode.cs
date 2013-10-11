using System;
using System.Net;
using Couchbase.Results;
using Enyim.Caching.Memcached;

namespace Couchbase
{
    public interface ICouchbaseNode : IMemcachedNode
    {
        IHttpClient Client { get; }
        IObserveOperationResult ExecuteObserveOperation(IObserveOperation op);
    }
}