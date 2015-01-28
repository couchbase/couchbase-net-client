﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching.Memcached.Protocol.Binary;
using Enyim.Caching.Memcached;
using System.IO;
using System.Threading;
using Enyim.Caching;
using Enyim.Caching.Memcached.Results;
using Enyim.Caching.Memcached.Results.Helpers;
using Enyim.Caching.Memcached.Results.Extensions;
using Couchbase.Operations;

namespace Couchbase
{
    /// <summary>
    /// Couchbase requires each item operation to have a vbucket index set in the request's "reserved" field. (This is used for replicatiom and failover.) This op factory provides customized operations handling these indexes.
    /// </summary>
    internal class VBucketAwareOperationFactory : ICouchbaseOperationFactory
    {
        private VBucketNodeLocator locator;

        public VBucketAwareOperationFactory(VBucketNodeLocator locator)
        {
            this.locator = locator;
        }

        internal static bool GuessResponseState(BinaryResponse response, out OperationState state)
        {
            switch (response.StatusCode)
            {
                case 0: state = OperationState.Success;
                    return true;

                case 7: state = OperationState.InvalidVBucket;
                    break;

                default: state = OperationState.Failure;
                    break;
            }

            return false;
        }

        IGetOperation IOperationFactory.Get(string key)
        {
            return new VBGet(locator, key);
        }

        IMultiGetOperation IOperationFactory.MultiGet(IList<string> keys)
        {
            return new VBMget(locator, keys);
        }

        IStoreOperation IOperationFactory.Store(StoreMode mode, string key, CacheItem value, uint expires, ulong cas)
        {
            return new VBStore(locator, mode, key, value, expires) { Cas = cas };
        }

        IDeleteOperation IOperationFactory.Delete(string key, ulong cas)
        {
            return new VBDelete(locator, key) { Cas = cas };
        }

        IMutatorOperation IOperationFactory.Mutate(MutationMode mode, string key, ulong defaultValue, ulong delta, uint expires, ulong cas)
        {
            return new VBMutator(locator, mode, key, defaultValue, delta, expires) { Cas = cas };
        }

        IConcatOperation IOperationFactory.Concat(ConcatenationMode mode, string key, ulong cas, ArraySegment<byte> data)
        {
            return new VBConcat(locator, mode, key, data) { Cas = cas };
        }

        IStatsOperation IOperationFactory.Stats(string type)
        {
            return new StatsOperation(type);
        }

        IFlushOperation IOperationFactory.Flush()
        {
            return new FlushOperation();
        }

        ITouchOperation ICouchbaseOperationFactory.Touch(string key, uint newExpiration)
        {
            return new TouchOperation(this.locator, key, newExpiration);
        }

        IGetAndTouchOperation ICouchbaseOperationFactory.GetAndTouch(string key, uint newExpiration)
        {
            return new GetAndTouchOperation(this.locator, key, newExpiration);
        }

        IObserveOperation ICouchbaseOperationFactory.Observe(string key, int vbucket, ulong cas)
        {
            return new ObserveOperation(key, vbucket, cas);
        }

        IGetWithLockOperation ICouchbaseOperationFactory.GetWithLock(string key, uint lockExpiration)
        {
            return new GetWithLockOperation(locator, key, lockExpiration);
        }

        IUnlockOperation ICouchbaseOperationFactory.Unlock(string key, ulong cas)
        {
            return new UnlockOperation(locator, key, cas);
        }

        #region [ Custom operations            ]

        private class VBStore : StoreOperation, IOperationWithState
        {
            private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(VBStore).FullName.Replace('+', '.'));
            private VBucketNodeLocator locator;
            private OperationState state;

            public VBStore(VBucketNodeLocator locator, StoreMode mode, string key, CacheItem value, uint expires)
                : base(mode, key, value, expires)
            {
                this.locator = locator;
            }

            protected override BinaryRequest Build()
            {
                var retval = base.Build();
                retval.Reserved = (ushort)locator.GetIndex(this.Key);

                if (log.IsDebugEnabled) log.DebugFormat("Key {0} was mapped to {1}", this.Key, retval.Reserved);

                return retval;
            }

            protected override IOperationResult ProcessResponse(BinaryResponse response)
            {
                base.ProcessResponse(response);
                var result = new BinaryOperationResult();

                if (!GuessResponseState(response, out this.state))
                {
                    var message = ResultHelper.ProcessResponseData(response.Data, "Failed to process response");
                    return result.Fail(message);
                }

                return result.Pass();
            }

            #region [ IOperationWithState          ]

            OperationState IOperationWithState.State
            {
                get { return this.state; }
            }

            #endregion
        }

        private class VBDelete : DeleteOperation, IOperationWithState
        {
            private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(VBDelete).FullName.Replace('+', '.'));
            private VBucketNodeLocator locator;
            private OperationState state;

            public VBDelete(VBucketNodeLocator locator, string key)
                : base(key)
            {
                this.locator = locator;
            }

            protected override BinaryRequest Build()
            {
                var retval = base.Build();
                retval.Reserved = (ushort)locator.GetIndex(this.Key);

                if (log.IsDebugEnabled) log.DebugFormat("Key {0} was mapped to {1}", this.Key, retval.Reserved);

                return retval;
            }

            protected override IOperationResult ProcessResponse(BinaryResponse response)
            {
                base.ProcessResponse(response);
                var result = new BinaryOperationResult();

                if (!GuessResponseState(response, out this.state))
                {
                    var message = ResultHelper.ProcessResponseData(response.Data, "Failed to process response");
                    return result.Fail(message);
                }

                return result.Pass();
            }

            #region [ IOperationWithState          ]

            OperationState IOperationWithState.State
            {
                get { return this.state; }
            }

            #endregion
        }

        private class VBMutator : MutatorOperation, IOperationWithState
        {
            private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(VBMutator).FullName.Replace('+', '.'));
            private VBucketNodeLocator locator;
            private OperationState state;

            public VBMutator(VBucketNodeLocator locator, MutationMode mode, string key, ulong defaultValue, ulong delta, uint expires)
                : base(mode, key, defaultValue, delta, expires)
            {
                this.locator = locator;
            }

            protected override BinaryRequest Build()
            {
                var retval = base.Build();
                retval.Reserved = (ushort)locator.GetIndex(this.Key);

                if (log.IsDebugEnabled) log.DebugFormat("Key {0} was mapped to {1}", this.Key, retval.Reserved);

                return retval;
            }

            protected override IOperationResult ProcessResponse(BinaryResponse response)
            {
                base.ProcessResponse(response);
                var result = new BinaryOperationResult();

                if (!GuessResponseState(response, out this.state))
                {
                    var message = ResultHelper.ProcessResponseData(response.Data, "Failed to process response");
                    return result.Fail(message);
                }
                return result.Pass();
            }

            #region [ IOperationWithState          ]

            OperationState IOperationWithState.State
            {
                get { return this.state; }
            }

            #endregion
        }

        private class VBMget : MultiGetOperation
        {
            private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(VBMget).FullName.Replace('+', '.'));
            private VBucketNodeLocator locator;

            public VBMget(VBucketNodeLocator locator, IList<string> keys)
                : base(keys)
            {
                this.locator = locator;
            }

            protected override BinaryRequest Build(string key)
            {
                var retval = base.Build(key);
                retval.Reserved = (ushort)locator.GetIndex(key);

                if (log.IsDebugEnabled) log.DebugFormat("Key {0} was mapped to {1}", key, retval.Reserved);

                return retval;
            }
        }

        private class VBConcat : ConcatOperation, IOperationWithState
        {
            private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(VBConcat).FullName.Replace('+', '.'));
            private VBucketNodeLocator locator;
            private OperationState state;

            public VBConcat(VBucketNodeLocator locator, ConcatenationMode mode, string key, ArraySegment<byte> data)
                : base(mode, key, data)
            {
                this.locator = locator;
            }

            protected override BinaryRequest Build()
            {
                var retval = base.Build();
                retval.Reserved = (ushort)locator.GetIndex(this.Key);

                if (log.IsDebugEnabled) log.DebugFormat("Key {0} was mapped to {1}", this.Key, retval.Reserved);

                return retval;
            }

            protected override IOperationResult ProcessResponse(BinaryResponse response)
            {
                base.ProcessResponse(response);
                var result = new BinaryOperationResult();

                if (!GuessResponseState(response, out this.state))
                {
                    var message = ResultHelper.ProcessResponseData(response.Data, "Failed to process response");
                    return result.Fail(message);
                }

                return result.Pass();
            }

            #region [ IOperationWithState          ]

            OperationState IOperationWithState.State
            {
                get { return this.state; }
            }

            #endregion
        }

        private class VBGet : GetOperation, IOperationWithState
        {
            private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(VBGet).FullName.Replace('+', '.'));
            private VBucketNodeLocator locator;
            private OperationState state;

            public VBGet(VBucketNodeLocator locator, string key)
                : base(key)
            {
                this.locator = locator;
                this.state = OperationState.Unspecified;
            }

            protected override BinaryRequest Build()
            {
                var retval = base.Build();
                retval.Reserved = (ushort)locator.GetIndex(this.Key);

                if (log.IsDebugEnabled) log.DebugFormat("Key {0} was mapped to {1}", this.Key, retval.Reserved);

                return retval;
            }

            protected override IOperationResult ProcessResponse(BinaryResponse response)
            {
                base.ProcessResponse(response);
                var result = new BinaryOperationResult();

                if (!GuessResponseState(response, out this.state))
                {
                    var message = ResultHelper.ProcessResponseData(response.Data, "Failed to process response");
                    return result.Fail(message);
                }

                return result.Pass();
            }

            #region [ IOperationWithState          ]

            OperationState IOperationWithState.State
            {
                get { return this.state; }
            }

            #endregion
        }

        #endregion

        ISyncOperation ICouchbaseOperationFactory.Sync(SyncMode mode, IList<KeyValuePair<string, ulong>> keys, int replicationCount)
        {
            return new SyncOperation(this.locator, keys, mode, replicationCount);
        }
    }

    internal interface IOperationWithState
    {
        OperationState State { get; }
    }

    internal enum OperationState { Unspecified, Success, Failure, InvalidVBucket }
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