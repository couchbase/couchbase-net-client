using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching.Memcached;

namespace Couchbase
{
    public interface ICouchbaseOperationFactory : IOperationFactory
    {
        ITouchOperation Touch(string key, uint newExpiration);

        IGetAndTouchOperation GetAndTouch(string key, uint newExpiration);

        IObserveOperation Observe(string key, int vbucket, ulong cas);

        IGetWithLockOperation GetWithLock(string key, uint lockExpiration);

        IUnlockOperation Unlock(string key, ulong cas);

        ISyncOperation Sync(SyncMode mode, IList<KeyValuePair<string, ulong>> keys, int replicationCount);
    }
}