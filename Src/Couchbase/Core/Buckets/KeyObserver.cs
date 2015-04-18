using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Configuration;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.Utils;

namespace Couchbase.Core.Buckets
{
    /// <summary>
    /// Performs and observe event on a key, terminating when the durability requirements are satisfied or the specified timeout has expired.
    /// </summary>
    internal sealed class KeyObserver
    {
        private readonly IConfigInfo _configInfo;
        private readonly int _interval;
        private readonly int _timeout;
        private readonly static ILog Log = LogManager.GetLogger<KeyObserver>();
        private const uint ObserveOperationTimeout = 2500; //2.5sec
        private readonly ITypeTranscoder _transcoder;

        /// <summary>
        /// The durability requirements that must be met.
        /// </summary>
        private struct DurabiltyCriteria
        {
            public KeyState PersistState { get; set; }
            public KeyState ReplicateState { get; set; }
        }

        /// <summary>
        /// Data structure for holding and passing arguments
        /// </summary>
        private sealed class ObserveParams
        {
            private volatile object _syncObj = new object();
            public string Key { get; set; }
            public ReplicateTo ReplicateTo { get; set; }
            public PersistTo PersistTo { get; set; }
            public ulong Cas { get; set; }
            public DurabiltyCriteria Criteria { get; set; }
            public IVBucket VBucket { get; set; }
            public int ReplicatedToCount;
            public int PersistedToCount;

            public bool HasMutated(ulong cas)
            {
                return Cas != cas;
            }

            /// <summary>
            /// Check to see if the durability constraint is met or exceeded
            /// </summary>
            /// <returns>True if the durability constraints specified by <see cref="ReplicateTo"/> and <see cref="PersistTo"/> have been met or exceeded.</returns>
            public bool IsDurabilityMet()
            {
                lock (_syncObj)
                {
                    return ReplicatedToCount >= (int)ReplicateTo && PersistedToCount >= (int)PersistTo;
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
        public KeyObserver(IConfigInfo configInfo, ITypeTranscoder transcoder, int interval, int timeout)
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
        public KeyObserver(IConfigInfo configInfo, int interval, int timeout)
            : this(configInfo, new DefaultTranscoder(), interval, timeout)
        {
        }


        /// <summary>
        /// Performs an observe event on the durability requirements specified on a key stored by an Add operation.
        /// </summary>
        /// <param name="key">The key to observe.</param>
        /// <param name="cas">The 'Check and Set' value of the key.</param>
        /// <param name="replicateTo">The number of replicas that the key must be replicated to to satisfy the durability constraint.</param>
        /// <param name="persistTo">The number of replicas that the key must be persisted to to satisfy the durability constraint.</param>
        /// <returns>True if the durability constraints have been satisfied.</returns>
        public bool ObserveAdd(string key, ulong cas, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return Observe(key, cas, false, replicateTo, persistTo);
        }

        /// <summary>
        /// Performs an observe event on the durability requirements specified on a key stored by an delete operation.
        /// </summary>
        /// <param name="key">The key to observe.</param>
        /// <param name="cas">The 'Check and Set' value of the key.</param>
        /// <param name="replicateTo">The number of replicas that the key must be replicated (deleted) to to satisfy the durability constraint.</param>
        /// <param name="persistTo">The number of replicas that the key must be persisted (deleted) to to satisfy the durability constraint.</param>
        /// <returns>True if the durability constraints have been satisfied.</returns>
        public Task<bool> ObserveRemoveAsync(string key, ulong cas, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return ObserveAsync(key, cas, true, replicateTo, persistTo);
        }

        /// <summary>
        /// Performs an observe event on the durability requirements specified on a key stored by an Add operation.
        /// </summary>
        /// <param name="key">The key to observe.</param>
        /// <param name="cas">The 'Check and Set' value of the key.</param>
        /// <param name="replicateTo">The number of replicas that the key must be replicated to to satisfy the durability constraint.</param>
        /// <param name="persistTo">The number of replicas that the key must be persisted to to satisfy the durability constraint.</param>
        /// <returns>True if the durability constraints have been satisfied.</returns>
        public Task<bool> ObserveAddAsync(string key, ulong cas, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return ObserveAsync(key, cas, false, replicateTo, persistTo);
        }

        /// <summary>
        /// Performs an observe event on the durability requirements specified on a key stored by an delete operation.
        /// </summary>
        /// <param name="key">The key to observe.</param>
        /// <param name="cas">The 'Check and Set' value of the key.</param>
        /// <param name="replicateTo">The number of replicas that the key must be replicated (deleted) to to satisfy the durability constraint.</param>
        /// <param name="persistTo">The number of replicas that the key must be persisted (deleted) to to satisfy the durability constraint.</param>
        /// <returns>True if the durability constraints have been satisfied.</returns>
        public bool ObserveRemove(string key, ulong cas, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return Observe(key, cas, true, replicateTo, persistTo);
        }

        /// <summary>
        ///  Performs an observe event on the durability requirements specified on a key asynchronously
        /// </summary>
        /// <param name="key">The key to observe.</param>
        /// <param name="cas">The 'Check and Set' value of the key.</param>
        /// <param name="deletion">True if this is a delete operation.</param>
        /// <param name="replicateTo">The number of replicas that the key must be replicated to to satisfy the durability constraint.</param>
        /// <param name="persistTo">The number of replicas that the key must be persisted to to satisfy the durability constraint.</param>
        /// <returns> A <see cref="Task{bool}"/> representing the aynchronous operation.</returns>
        public async Task<bool> ObserveAsync(string key, ulong cas, bool deletion, ReplicateTo replicateTo,
            PersistTo persistTo)
        {
            var criteria = GetDurabilityCriteria(deletion);
            var keyMapper = _configInfo.GetKeyMapper();
            var vBucket = (IVBucket) keyMapper.MapKey(key);

            var observeParams = new ObserveParams
            {
                Cas = cas,
                Criteria = criteria,
                Key = key,
                PersistTo = persistTo,
                ReplicateTo = replicateTo,
                VBucket = vBucket
            };

            var operation = new Observe(key, vBucket, _transcoder, ObserveOperationTimeout);
             //Used to terminate the loop at the specific timeout
            using (var cts = new CancellationTokenSource(_timeout))
            {
                //perform the observe operation at the set interval and terminate if not successful by the timeout
                var task = await ObserveEvery(async p =>
                {
                    //check the master for persistence to disk
                    var master = p.VBucket.LocatePrimary();
                    var result = master.Send(operation);
                    Log.Debug(m => m("Master {0} - {1}", master.EndPoint, result.Value));
                    var state = result.Value;
                    if (state.KeyState == p.Criteria.PersistState)
                    {
                        Interlocked.Increment(ref p.PersistedToCount);
                    }

                    //Key mutation detected so fail
                    if (p.HasMutated(state.Cas))
                    {
                        return false;
                    }

                    //Check if durability requirements have been met
                    if (p.IsDurabilityMet())
                    {
                        return true;
                    }

                    //Run the durability requirement check on each replica
                    var tasks = new List<Task<bool>>();
                    var replicas = GetReplicas(vBucket, replicateTo, persistTo);
                    replicas.ForEach(x => tasks.Add(CheckReplicaAsync(p, operation, x)));

                    //Wait for all tasks to finish
                    await Task.WhenAll(tasks.ToArray()).ContinueOnAnyContext();
                    var mutated = tasks.All(subtask => subtask.Result);

                    return p.IsDurabilityMet() && !mutated;
                }, observeParams, _interval, cts.Token).ContinueOnAnyContext();
                return task;
            }
        }

        /// <summary>
        ///  Performs an observe event on the durability requirements specified on a key.
        /// </summary>
        /// <param name="key">The key to observe.</param>
        /// <param name="cas">The 'Check and Set' value of the key.</param>
        /// <param name="deletion">True if this is a delete operation.</param>
        /// <param name="replicateTo">The number of replicas that the key must be replicated to to satisfy the durability constraint.</param>
        /// <param name="persistTo">The number of replicas that the key must be persisted to to satisfy the durability constraint.</param>
        /// <returns>True if the durability constraints have been met.</returns>
        public bool Observe(string key, ulong cas, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            var criteria = GetDurabilityCriteria(deletion);
            var keyMapper = _configInfo.GetKeyMapper();
            var vBucket = (IVBucket)keyMapper.MapKey(key);

            var p = new ObserveParams
            {
                Cas = cas,
                Criteria = criteria,
                Key = key,
                PersistTo = persistTo,
                ReplicateTo = replicateTo,
                VBucket = vBucket
            };

            var operation = new Observe(key, vBucket, _transcoder, ObserveOperationTimeout);
            do
            {
                var master = p.VBucket.LocatePrimary();
                var result = master.Send(operation);
                var state = result.Value;
                if (state.KeyState == p.Criteria.PersistState)
                {
                    Interlocked.Increment(ref p.PersistedToCount);
                }
                if (!deletion && p.HasMutated(state.Cas))
                {
                    return false;
                }

                //First check if durability has already been met
                if (p.IsDurabilityMet())
                {
                    return true;
                }

                //If not check each replica
                if (CheckReplicas(p, operation, replicateTo, persistTo))
                {
                    return true;
                }
                operation = (Observe)operation.Clone();

            } while (!operation.TimedOut());
            return false;
        }

        /// <summary>
        /// Gets a list of replica indexes that is the larger of either the <see cref="PersistTo"/> or the <see cref="ReplicateTo"/> value.
        /// </summary>
        /// <param name="vBucket">The <see cref="VBucket"/> containing the replica indexes.</param>
        /// <param name="replicateTo">The <see cref="ReplicateTo"/> value.</param>
        /// <param name="persistTo">The <see cref="PersistTo"/> value.</param>
        /// <returns>A list of replica indexes which is the larger of either the <see cref="PersistTo"/> or the <see cref="ReplicateTo"/> value</returns>
        internal List<int> GetReplicas(IVBucket vBucket, ReplicateTo replicateTo, PersistTo persistTo)
        {
            var maxReplicas = (int) replicateTo > (int) persistTo ?
                (int) replicateTo :
                (int)persistTo;

            return maxReplicas > vBucket.Replicas.Length ?
                vBucket.Replicas.Where(x => x > -1).ToList() :
                vBucket.Replicas.Where(x => x > -1).Take(maxReplicas).ToList();
        }

        /// <summary>
        /// Checks the replicas to see if the key has met the durability constraints defined by the caller.
        /// </summary>
        /// <param name="observeParams">The observe parameters.</param>
        /// <param name="operation">The operation observe operation reference; will be cloned if reused.</param>
        /// <param name="replicateTo">The replication durability that must be met.</param>
        /// <param name="persistTo">The persistence durbaility that must be met.</param>
        /// <returns></returns>
        bool CheckReplicas(ObserveParams observeParams, Observe operation, ReplicateTo replicateTo, PersistTo persistTo)
        {
            //Get the candidate replicas, if none are defined that match the specified durability return false.
            var replicas = GetReplicas(observeParams.VBucket, replicateTo, persistTo);
            if (replicas.Count < (int)replicateTo)
            {
                return false;
            }

            //Check each replica to see if has met the durability constraints specified. A mutation means we failed.
            var mutated = replicas.All(index => CheckReplica(observeParams, operation, index));
            return observeParams.IsDurabilityMet() && !mutated;
        }

        /// <summary>
        /// Checks the replica at a given replicaIndex for the durability constraints.
        /// </summary>
        /// <param name="observeParams">The observe parameters - stateful - gather info with each request.</param>
        /// <param name="operation">The observe operation.</param>
        /// <param name="replicaIndex">Index of the replica.</param>
        /// <returns>True if the key has not mutated.</returns>
        static bool CheckReplica(ObserveParams observeParams, Observe operation, int replicaIndex)
        {
            //clone the operation since we already checked the primary and we want to maintain internal state (opaque, timer, etc)
            operation = (Observe)operation.Clone();
            var replica = observeParams.VBucket.LocateReplica(replicaIndex);
            var result = replica.Send(operation);

            //Check the result and update the counters
            var state = result.Value;
            if (state.KeyState == observeParams.Criteria.PersistState)
            {
                Interlocked.Increment(ref observeParams.ReplicatedToCount);
                Interlocked.Increment(ref observeParams.PersistedToCount);
            }
            else if (state.KeyState == observeParams.Criteria.ReplicateState)
            {
                Interlocked.Increment(ref observeParams.ReplicatedToCount);
            }
            return !observeParams.HasMutated(state.Cas);
        }

        /// <summary>
        /// Asynchronously checks the replications status of a key.
        /// </summary>
        /// <param name="observeParams">The <see cref="ObserveParams"/> object.</param>
        /// <param name="operation">The Observe operation.</param>
        /// <param name="replicaIndex">The replicaIndex of the replica within the <see cref="IVBucket"/></param>
        /// <returns>True if the durability requirements specified by <see cref="PersistTo"/> and <see cref="ReplicateTo"/> have been satisfied.</returns>
        static async Task<bool> CheckReplicaAsync(ObserveParams observeParams, Observe operation, int replicaIndex)
        {
            Log.Debug(m=>m("checking replica {0}", replicaIndex));
            if (observeParams.IsDurabilityMet()) return true;

             //clone the operation since we already checked the primary and we want to maintain internal state (opaque, timer, etc)
            operation = (Observe)operation.Clone();

            var replica = observeParams.VBucket.LocateReplica(replicaIndex);
            var result = await Task.Run(()=>replica.Send(operation)).ContinueOnAnyContext();

            Log.Debug(m=>m("Replica {0} - {1} [0]", replica.EndPoint, result.Value, replicaIndex));
            var state = result.Value;
            if (state.KeyState == observeParams.Criteria.PersistState)
            {
                Interlocked.Increment(ref observeParams.ReplicatedToCount);
                Interlocked.Increment(ref observeParams.PersistedToCount);
            }
            else if (state.KeyState == observeParams.Criteria.ReplicateState)
            {
                Interlocked.Increment(ref observeParams.ReplicatedToCount);
            }
            return !observeParams.HasMutated(state.Cas);
        }

        /// <summary>
        /// Observes a set of keys at a specified interval and timeout.
        /// </summary>
        /// <param name="observe">The func to call at the specific interval</param>
        /// <param name="observeParams">The parameters to pass in.</param>
        /// <param name="interval">The interval to check.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use to terminate the observation at the specified timeout.</param>
        /// <returns>True if the durability requirements specified by <see cref="PersistTo"/> and <see cref="ReplicateTo"/> have been satisfied.</returns>
        static async Task<bool> ObserveEvery(Func<ObserveParams, Task<bool>> observe, ObserveParams observeParams, int interval, CancellationToken cancellationToken)
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
            }
        }

        /// <summary>
        /// Gets the specified durability requirements for the key.
        /// </summary>
        /// <param name="remove">If true the durability requirements will be set as a deletion operation, otherwise as an Add operation.</param>
        /// <returns>The durability requirements that must be statisfied.</returns>
        private static DurabiltyCriteria GetDurabilityCriteria(bool remove)
        {
            return new DurabiltyCriteria
            {
                PersistState = remove ? KeyState.NotFound : KeyState.FoundPersisted,
                ReplicateState = remove ? KeyState.LogicalDeleted : KeyState.FoundNotPersisted
            };
        }
    }
}
