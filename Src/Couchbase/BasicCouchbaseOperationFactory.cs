using System;
using System.Collections.Generic;
using Couchbase.Operations;

namespace Couchbase
{
    internal class BasicCouchbaseOperationFactory : Enyim.Caching.Memcached.Protocol.Binary.BinaryOperationFactory, ICouchbaseOperationFactory
    {
        internal static readonly BasicCouchbaseOperationFactory Instance = new BasicCouchbaseOperationFactory();

        ITouchOperation ICouchbaseOperationFactory.Touch(string key, uint newExpiration)
        {
            return new TouchOperation(null, key, newExpiration);
        }

        IGetAndTouchOperation ICouchbaseOperationFactory.GetAndTouch(string key, uint newExpiration)
        {
            return new GetAndTouchOperation(null, key, newExpiration);
        }

        IGetWithLockOperation ICouchbaseOperationFactory.GetWithLock(string key, uint lockExpiration)
        {
            return new GetWithLockOperation(null, key, lockExpiration);
        }

        IUnlockOperation ICouchbaseOperationFactory.Unlock(string key, ulong cas)
        {
            return new UnlockOperation(null, key, cas);
        }

        ISyncOperation ICouchbaseOperationFactory.Sync(SyncMode mode, IList<KeyValuePair<string, ulong>> keys, int replicationCount)
        {
            throw new NotSupportedException("Sync is not supported on memcached buckets.");
        }

        IObserveOperation ICouchbaseOperationFactory.Observe(string key, int vbucket, ulong cas)
        {
            return new ObserveOperation(key, vbucket, cas);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
 *    @copyright 2010 Attila Kiskó, enyim.com
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion