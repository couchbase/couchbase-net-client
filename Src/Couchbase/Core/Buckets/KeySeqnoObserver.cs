using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Couchbase.Configuration;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Operations.EnhancedDurability;
using Couchbase.Utils;

namespace Couchbase.Core.Buckets
{
    internal class KeySeqnoObserver
    {
        private readonly IConfigInfo _configInfo;
        private readonly int _interval;
        private readonly uint _timeout = 2500; //2.5sec;
        private static readonly ILogger Log = new LoggerFactory().CreateLogger<KeyObserver>();
        private readonly ITypeTranscoder _transcoder;

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
            private int _replicatedToCount;
            private int _persistedToCount;

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
            public void CheckPersisted(ObserveSeqnoResponse response)
            {
                var persisted = response.LastPersistedSeqno >= Token.SequenceNumber;
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
            public void CheckReplicated(ObserveSeqnoResponse response)
            {
                var replicated = response.CurrentSeqno >= Token.SequenceNumber;
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
            public void CheckMutationLost(ObserveSeqnoResponse response)
            {
                if (response.IsHardFailover && response.LastSeqnoReceived < Token.SequenceNumber)
                {
                    throw new DocumentMutationLostException(ExceptionUtil.DocumentMutationLostMsg);
                }
            }

            /// <summary>
            ///Gets the maximum number of replicas to check.
            /// </summary>
            /// <returns></returns>
            public int MaxReplicas()
            {
                return (int) ReplicateTo > (int) PersistTo
                    ? (int) ReplicateTo
                    : (int) PersistTo;
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

        /// <summary>
        /// Ctor for <see cref="KeyObserver"/>.
        /// </summary>
        /// <param name="configInfo">The <see cref="IConfigInfo"/> object which represents the current cluster and client configuration.</param>
        /// <param name="transcoder"></param>
        /// <param name="interval">The interval to poll.</param>
        /// <param name="timeout">The max time to wait for the durability requirements to be met.</param>
        public KeySeqnoObserver(IConfigInfo configInfo, ITypeTranscoder transcoder, int interval, uint timeout)
        {
            _configInfo = configInfo;
            _interval = interval;
            _timeout = timeout;
            _transcoder = transcoder;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyObserver"/> class.
        /// </summary>
        /// <param name="configInfo">The <see cref="IConfigInfo"/> object which represents the current cluster and client configuration.</param>
        /// <param name="interval">The interval to poll.</param>
        /// <param name="timeout">The max time to wait for the durability requirements to be met.</param>
        public KeySeqnoObserver(IConfigInfo configInfo, int interval, uint timeout)
            : this(configInfo, new DefaultTranscoder(), interval, timeout)
        {
        }

        /// <summary>
        ///  Performs an observe event on the durability requirements specified on a key asynchronously
        /// </summary>
        /// <param name="token">The <see cref="MutationToken"/> to compare against.</param>
        /// <param name="replicateTo">The number of replicas that the key must be replicated to to satisfy the durability constraint.</param>
        /// <param name="persistTo">The number of replicas that the key must be persisted to to satisfy the durability constraint.</param>
        /// <returns> A <see cref="Task{bool}"/> representing the aynchronous operation.</returns>
        /// <exception cref="ReplicaNotConfiguredException">Thrown if the number of replicas requested
        /// in the ReplicateTo parameter does not match the # of replicas configured on the server.</exception>
        public async Task<bool> ObserveAsync(MutationToken token, ReplicateTo replicateTo, PersistTo persistTo)
        {
            var keyMapper = (VBucketKeyMapper)_configInfo.GetKeyMapper();
            var obParams = new ObserveParams
            {
                ReplicateTo = replicateTo,
                PersistTo = persistTo,
                Token = token,
                VBucket = keyMapper[token.VBucketId]
            };
            obParams.CheckConfiguredReplicas();
            var op = new ObserveSeqno(obParams.Token, _transcoder, _timeout);

            using (var cts = new CancellationTokenSource((int)_timeout))
            {
                //perform the observe operation at the set interval and terminate if not successful by the timeout
                var task = await ObserveEvery(async p =>
                {
                    IServer master;
                    var attempts = 0;
                    while ((master = p.VBucket.LocatePrimary()) == null)
                    {
                        if (attempts++ > 10) { throw new TimeoutException("Could not acquire a server."); }
                        await Task.Delay((int)Math.Pow(2, attempts)).ContinueOnAnyContext();
                    }

                    var result = master.Send(op);
                    var osr = result.Value;

                    p.CheckMutationLost(osr);
                    p.CheckPersisted(osr);

                    if (p.IsDurabilityMet())
                    {
                        return true;
                    }
                    return await CheckReplicasAsync(p, op).ContinueOnAnyContext();
                }, obParams, _interval, op, cts.Token).ContinueOnAnyContext();
                return task;
            }
        }

        /// <summary>
        /// Observes a set of keys at a specified interval and timeout.
        /// </summary>
        /// <param name="observe">The func to call at the specific interval</param>
        /// <param name="observeParams">The parameters to pass in.</param>
        /// <param name="interval">The interval to check.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use to terminate the observation at the specified timeout.</param>
        /// <returns>True if the durability requirements specified by <see cref="PersistTo"/> and <see cref="ReplicateTo"/> have been satisfied.</returns>
        static async Task<bool> ObserveEvery(Func<ObserveParams, Task<bool>> observe, ObserveParams observeParams, int interval, ObserveSeqno op, CancellationToken cancellationToken)
        {
            while (true)
            {
                var result = await observe(observeParams).ContinueOnAnyContext();
                if (result)
                {
                    return true;
                }

                var task = Task.Delay(interval, cancellationToken).ContinueOnAnyContext();
                try
                {
                    await task;
                }
                catch (TaskCanceledException)
                {
                    return false;
                }

                //prepare for a second attempt
                op = (ObserveSeqno)op.Clone();
                observeParams.Reset();
            }
        }

        /// <summary>
        /// Observes the specified key using the Seqno.
        /// </summary>
        /// <param name="key">The key.</param>
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

            var op = new ObserveSeqno(p.Token, _transcoder, _timeout);
            do
            {
                var master = p.VBucket.LocatePrimary();
                var result = master.Send(op);
                var osr = result.Value;

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
        bool CheckReplicas(ObserveParams observeParams, ObserveSeqno op)
        {
            var replicas = observeParams.GetReplicas();
            return replicas.Any(replicaId => CheckReplica(observeParams, op, replicaId));
        }

        async Task<bool> CheckReplicasAsync(ObserveParams observeParams, ObserveSeqno op)
        {
            var replicas = observeParams.GetReplicas();

            var tasks = new List<Task<bool>>();
            replicas.ForEach(x => tasks.Add(CheckReplicaAsync(observeParams, op, x)));
            await Task.WhenAll(tasks);
            return observeParams.IsDurabilityMet();
        }

        /// <summary>
        /// Checks a replica for durability constraints.
        /// </summary>
        /// <param name="observeParams">The observe parameters.</param>
        /// <param name="op">The op.</param>
        /// <param name="replicaId">The replica identifier.</param>
        /// <returns></returns>
        bool CheckReplica(ObserveParams observeParams, ObserveSeqno op, int replicaId)
        {
            var cloned = (ObserveSeqno)op.Clone();
            var replica = observeParams.VBucket.LocateReplica(replicaId);
            var result = replica.Send(cloned);

            observeParams.CheckMutationLost(result.Value);
            observeParams.CheckPersisted(result.Value);
            observeParams.CheckReplicated(result.Value);
            return observeParams.IsDurabilityMet();
        }

        static Task<bool> CheckReplicaAsync(ObserveParams observeParams, ObserveSeqno op, int replicaId)
        {
            var cloned = (ObserveSeqno)op.Clone();
            var replica = observeParams.VBucket.LocateReplica(replicaId);
            var result = replica.Send(cloned);

            observeParams.CheckMutationLost(result.Value);
            observeParams.CheckPersisted(result.Value);
            observeParams.CheckReplicated(result.Value);
            return Task.FromResult(observeParams.IsDurabilityMet());
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
