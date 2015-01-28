using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching.Memcached;
using Enyim.Caching.Configuration;

namespace Couchbase
{
    public interface ICouchbaseServerPool : IServerPool
    {
        new ICouchbaseOperationFactory OperationFactory { get; }

        VBucket GetVBucket(string key);
    }
}