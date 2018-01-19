using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Logging;
using Couchbase.Configuration;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.EnhancedDurability;
using Couchbase.Utils;

namespace Couchbase.Core.Buckets
{
    internal class KeySeqnoObserver
    {
        private readonly IConfigInfo _configInfo;
        private readonly int _interval;
        private readonly uint _timeout;
        private static readonly ILog Log = LogManager.GetLogger<KeyObserver>();
        private readonly IClusterController _clusterController;
        private readonly ConcurrentDictionary<uint, IOperation> _pending;
        private readonly string _key;

        //for log redaction
        private Func<object, string> User = RedactableArgument.UserAction;

        /// <summary>
        /// Ctor for <see cref="KeyObserver"/>.
        /// </summary>
        /// <param name="pending">A queue for operations in-flight.</param>
        /// <param name="configInfo">The <see cref="IConfigInfo"/> object which represents the current cluster and client configuration.</param>
        /// <param name="clusterController">The <see cref="IClusterController"/> representing the cluster's state.</param>
        /// <param name="interval">The interval to poll.</param>
        /// <param name="timeout">The max time to wait for the durability requirements to be met.</param>
        public KeySeqnoObserver(string key, ConcurrentDictionary<uint, IOperation> pending, IConfigInfo configInfo, IClusterController clusterController, int interval, uint timeout)
        {
            _configInfo = configInfo;
            _interval = interval;
            _timeout = timeout;
            _clusterController = clusterController;
            _pending = pending;
            _key = key;
        }

        /// <summary>
        /// Data structure for holding and passing arguments
        /// </summary>
        private sealed class ObserveParams
        {
            private volatile object _syncObj = new object();
            public ReplicateTo ReplicateTo { get; set; }
            public PersistTo PersistTo { get; set; }
            public IVBucket VBucket { get; set; }
            public MutationToken Token { get; set; }
            public IOperation Operation { get; set; }
            private int _replicatedToCount;
            private int _persistedToCount;

            public bool TimedOut { get; set; }

            /// <summary>
            /// Check to see if the durability constraint is met or exceeded
            /// </summary>
            /// <returns>True if the durability constraints specified by <see cref="ReplicateTo"/> and <see cref="PersistTo"/> have been met or exceeded.</returns>
            public bool IsDurabilityMet()
            {
                lock (_syncObj)
                {
                    return _replicatedToCount >= (int)ReplicateTo && _persistedToCount >= (int)PersistTo;
                }
            }

            /// <summary>
            /// Determines whether the specified response has persisted and if it has,
            /// increases the <see cref="_persistedToCount"/> by one.
            /// </summary>
            /// <param name="response">The response.</param>
            /// <returns></returns>
            public void CheckPersisted(IOperationResult<ObserveSeqnoResponse> response)
            {
                var persisted = response.Value.LastPersistedSeqno >= Token.SequenceNumber;
                if (persisted)
                {
                    Interlocked.Increment(ref _persistedToCount);
                }
            }

            /// <summary>
            /// Determines whether the specified response has replicated,
            /// increases the <see cref="_replicatedToCount"/> by one.
            /// </summary>
            /// <param name="response">The response.</param>
            /// <returns></returns>
            public void CheckReplicated(IOperationResult<ObserveSeqnoResponse> response)
            {
                var replicated = response.Value.CurrentSeqno >= Token.SequenceNumber;
                if (replicated)
                {
                    Interlocked.Increment(ref _replicatedToCount);
                }
            }

            /// <summary>
            /// Checks to see if The observed document was lost during a hard failover, because the document did not reach the replica in time.
            /// </summary>
            /// <param name="response">The <see cref="ObserveSeqno"/>response.</param>
            /// <exception cref="DocumentMutationLostException">Thrown if the observed document was lost during
            /// a hard failover because the document did not reach the replica in time.</exception>
            public void CheckMutationLost(IOperationResult<ObserveSeqnoResponse> response)
            {
                if (response.Value.IsHardFailover && response.Value.LastSeqnoReceived < Token.SequenceNumber)
                {
                    throw new DocumentMutationLostException(ExceptionUtil.DocumentMutationLostMsg);
                }
            }

            /// <summary>
            ///Gets the maximum number of replicas to check.
            /// </summary>
            /// <returns></returns>
            private int MaxReplicas()
            {
                return (int) ReplicateTo;
            }

            /// <summary>
            /// Gets the replica vBucket indexes.
            /// </summary>
            /// <returns></returns>
            public List<int> GetReplicas()
            {
                var maxReplicas = MaxReplicas();

                return maxReplicas > VBucket.Replicas.Length ?
                    VBucket.Replicas.Where(x => x > -1).ToList() :
                    VBucket.Replicas.Where(x => x > -1).Take(maxReplicas).ToList();
            }

            /// <summary>
            /// Checks that the number of configured replicas matches the <see cref="ReplicateTo"/> value.
            /// </summary>
            /// <exception cref="ReplicaNotConfiguredException">Thrown if the number of replicas requested
            /// in the ReplicateTo parameter does not match the # of replicas configured on the server.</exception>
            public void CheckConfiguredReplicas()
            {
                var replicas = GetReplicas();
                if (replicas.Count < (int)ReplicateTo)
                {
                    throw new ReplicaNotConfiguredException(ExceptionUtil.NotEnoughReplicasConfigured);
                }
            }

            /// <summary>
            /// Resets the internal persistence and replication counters to zero.
            /// </summary>
            public void Reset()
            {
                lock (_syncObj)
                {
                    _persistedToCount = 0;
                    _replicatedToCount = 0;
                }
            }
        }

       private async Task<IServer> GetServerAsync(ObserveParams p)
        {
            IServer master;
            var attempts = 0;
            while ((master = p.VBucket.LocatePrimary()) == null)
            {
                if (attempts++ > 10)
                {
                    throw new TimeoutException("Could not acquire a server.");
                }
                await Task.Delay((int)Math.Pow(2, attempts)).ContinueOnAnyContext();
            }
            return master;
        }

        private async Task<bool> CheckPersistToAsync(ObserveParams observeParams)
        {
            if (observeParams.PersistTo == PersistTo.Zero) return true;
            var op = new ObserveSeqno(observeParams.Token, _clusterController.Transcoder, _timeout);
            observeParams.Operation = op;

            var tcs = new TaskCompletionSource<IOperationResult<ObserveSeqnoResponse>>();
            op.Completed = CallbackFactory.CompletedFuncForRetry(_pending, _clusterController, tcs);
            _pending.TryAdd(op.Opaque, op);

            var server = await GetServerAsync(observeParams);
            await server.SendAsync(op).ContinueOnAnyContext();
            var response = await tcs.Task.ContinueOnAnyContext();

            observeParams.CheckMutationLost(response);
            observeParams.CheckPersisted(response);

            return observeParams.IsDurabilityMet();
        }

        /// <summary>
        ///  Performs an observe event on the durability requirements specified on a key asynchronously
        /// </summary>
        /// <param name="token">The <see cref="MutationToken"/> to compare against.</param>
        /// <param name="replicateTo">The number of replicas that the key must be replicated to to satisfy the durability constraint.</param>
        /// <param name="persistTo">The number of replicas that the key must be persisted to to satisfy the durability constraint.</param>
        /// <param name="cts"></param>
        /// <returns> A <see cref="Task{boolean}"/> representing the aynchronous operation.</returns>
        /// <exception cref="ReplicaNotConfiguredException">Thrown if the number of replicas requested
        /// in the ReplicateTo parameter does not match the # of replicas configured on the server.</exception>
        public async Task<bool> ObserveAsync(MutationToken token, ReplicateTo replicateTo, PersistTo persistTo, CancellationTokenSource cts)
        {
            var keyMapper = (VBucketKeyMapper) _configInfo.GetKeyMapper();
            var obParams = new ObserveParams
            {
                ReplicateTo = replicateTo,
                PersistTo = persistTo,
                Token = token,
                VBucket = keyMapper[token.VBucketId]
            };
            obParams.CheckConfiguredReplicas();

            var persisted = await CheckPersistToAsync(obParams).ContinueOnAnyContext();
            var replicated = await CheckReplicasAsync(obParams).ContinueOnAnyContext();
            if (persisted && replicated)
            {
                Log.Debug("Persisted and replicated on first try: {0}", User(_key));
                return true;
            }
            return await ObserveEveryAsync(async p =>
            {
                Log.Debug("trying again: {0}", User(_key));
                persisted = await CheckPersistToAsync(obParams).ContinueOnAnyContext();
                replicated = await CheckReplicasAsync(obParams).ContinueOnAnyContext();
                return persisted & replicated;
            }, obParams, _interval, cts.Token);
        }

        /// <summary>
        /// Observes a set of keys at a specified interval and timeout.
        /// </summary>
        /// <param name="observe">The func to call at the specific interval</param>
        /// <param name="observeParams">The parameters to pass in.</param>
        /// <param name="interval">The interval to check.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use to terminate the observation at the specified timeout.</param>
        /// <returns>True if the durability requirements specified by <see cref="PersistTo"/> and <see cref="ReplicateTo"/> have been satisfied.</returns>
        private async Task<bool> ObserveEveryAsync(Func<ObserveParams, Task<bool>> observe, ObserveParams observeParams, int interval, CancellationToken cancellationToken)
        {
            while (true)
            {
                var result = await observe(observeParams).ContinueOnAnyContext();
                if (result)
                {
                    return true;
                }

                //delay for the interval - will throw TaskCancellationException if the token times out
                await Task.Delay(interval, cancellationToken).ContinueOnAnyContext();
            }
        }

        /// <summary>
        /// Observes the specified key using the Seqno.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="replicateTo">The replicate to.</param>
        /// <param name="persistTo">The persist to.</param>
        /// <returns>True if durability constraints were matched.</returns>
        /// <exception cref="DocumentMutationLostException">Thrown if the observed document was lost during
        /// a hard failover because the document did not reach the replica in time.</exception>
        /// <exception cref="ReplicaNotConfiguredException">Thrown if the number of replicas requested
        /// in the ReplicateTo parameter does not match the # of replicas configured on the server.</exception>
        public bool Observe(MutationToken token, ReplicateTo replicateTo, PersistTo persistTo)
        {
            var keyMapper = (VBucketKeyMapper)_configInfo.GetKeyMapper();

            var p = new ObserveParams
            {
                ReplicateTo = replicateTo,
                PersistTo = persistTo,
                Token = token,
                VBucket = keyMapper[token.VBucketId]
            };
            p.CheckConfiguredReplicas();

            var op = new ObserveSeqno(p.Token, _clusterController.Transcoder, _timeout);
            do
            {
                var master = p.VBucket.LocatePrimary();
                var osr = master.Send(op);

                p.CheckMutationLost(osr);
                p.CheckPersisted(osr);

                if (p.IsDurabilityMet())
                {
                    return true;
                }

                if (CheckReplicas(p, op))
                {
                    return true;
                }

                //prepare for another attempt
                op = (ObserveSeqno)op.Clone();
                p.Reset();
            } while (!op.TimedOut());

            return false;
        }

        /// <summary>
        /// Checks the replicas for durability constraints.
        /// </summary>
        /// <param name="observeParams">The observe parameters.</param>
        /// <param name="op">The op.</param>
        /// <returns></returns>
        private bool CheckReplicas(ObserveParams observeParams, ObserveSeqno op)
        {
            var replicas = observeParams.GetReplicas();
            return replicas.Any(replicaId => CheckReplica(observeParams, op, replicaId));
        }

        private async Task<bool> CheckReplicasAsync(ObserveParams observeParams)
        {
            if (observeParams.ReplicateTo == ReplicateTo.Zero) return true;
            var replicas = observeParams.GetReplicas().Select(x=>CheckReplicaAsync(observeParams, x));
            await Task.WhenAll(replicas);
            return observeParams.IsDurabilityMet();
        }

        /// <summary>
        /// Checks a replica for durability constraints.
        /// </summary>
        /// <param name="observeParams">The observe parameters.</param>
        /// <param name="op">The op.</param>
        /// <param name="replicaId">The replica identifier.</param>
        /// <returns></returns>
        private bool CheckReplica(ObserveParams observeParams, ObserveSeqno op, int replicaId)
        {
            var cloned = (ObserveSeqno)op.Clone();
            var replica = observeParams.VBucket.LocateReplica(replicaId);
            var result = replica.Send(cloned);

            observeParams.CheckMutationLost(result);
            observeParams.CheckPersisted(result);
            observeParams.CheckReplicated(result);
            return observeParams.IsDurabilityMet();
        }

        private async Task<bool> CheckReplicaAsync(ObserveParams observeParams, int replicaId)
        {
            var op = new ObserveSeqno(observeParams.Token, _clusterController.Transcoder, _timeout);
            observeParams.Operation = op;

            var tcs = new TaskCompletionSource<IOperationResult<ObserveSeqnoResponse>>();
            op.Completed = CallbackFactory.CompletedFuncForRetry(_pending, _clusterController, tcs);
            _pending.TryAdd(op.Opaque, op);

            Log.Debug("checking replica {0} - opaque: {1}", replicaId, op.Opaque);
            var replica = observeParams.VBucket.LocateReplica(replicaId);
            await replica.SendAsync(op).ContinueOnAnyContext();
            var response = await tcs.Task.ContinueOnAnyContext();

            observeParams.CheckMutationLost(response);
            observeParams.CheckPersisted(response);
            observeParams.CheckReplicated(response);
            return observeParams.IsDurabilityMet();
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
