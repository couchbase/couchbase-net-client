using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Configuration;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;

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
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();

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
        /// <param name="interval">The interval to poll.</param>
        /// <param name="timeout">The max time to wait for the durability requirements to be met.</param>
        public KeyObserver(IConfigInfo configInfo, int interval, int timeout)
        {
            _configInfo = configInfo;
            _interval = interval;
            _timeout = timeout;
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
        public bool ObserveRemove(string key, ulong cas, ReplicateTo replicateTo, PersistTo persistTo)
        {
            return Observe(key, cas, true, replicateTo, persistTo);
        }

        /// <summary>
        ///  Performs an observe event on the durability requirements specified on a key
        /// </summary>
        /// <param name="key">The key to observe.</param>
        /// <param name="cas">The 'Check and Set' value of the key.</param>
        /// <param name="deletion">True if this is a delete operation.</param>
        /// <param name="replicateTo">The number of replicas that the key must be replicated to to satisfy the durability constraint.</param>
        /// <param name="persistTo">The number of replicas that the key must be persisted to to satisfy the durability constraint.</param>
        /// <returns></returns>
        public bool Observe(string key, ulong cas, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            var criteria = GetDurabilityCriteria(deletion);
            var keyMapper = _configInfo.GetKeyMapper();
            var vBucket = (IVBucket)keyMapper.MapKey(key);

            var observeParams = new ObserveParams
            {
                Cas = cas,
                Criteria = criteria,
                Key = key,
                PersistTo = persistTo,
                ReplicateTo = replicateTo,
                VBucket = vBucket
            };

            //Used to terminate the loop at the specific timeout
            var cancellationTokenSource = new CancellationTokenSource(_timeout);

            //perform the observe operation at the set interval and terminate if not successful by the timeout
            var task = ObserveEvery(p =>
            {
                //check the master for persistence to disk
                var master = p.VBucket.LocatePrimary();
                var result = master.Send(new Observe(key, vBucket, new AutoByteConverter()));
                Log.Debug(m => m("Master {0} - {1}", master.EndPoint, result.Value));
                var state = result.Value;
                if (state.KeyState == p.Criteria.PersistState)
                {
                    Interlocked.Increment(ref p.PersistedToCount);
                }

                //Check if durability requirements have been met
                if (p.IsDurabilityMet())
                {
                    return true;
                }

                //Key mutation detected so fail
                if (p.HasMutated(state.Cas))
                {
                    return false;
                }

                //Run the durability requirement check on each replica
                var tasks = new List<Task<bool>>();
                var replicas = GetReplicas(vBucket, replicateTo, persistTo);
                replicas.ForEach(x => tasks.Add(CheckReplica(p, x)));

                //Wait for all tasks to finish
                Task.WaitAll(tasks.ToArray());
                return tasks.All(subtask => subtask.Result);
            }, observeParams, _interval, cancellationTokenSource.Token);
            task.Wait(_timeout);

            return task.Result;
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
        /// Asynchronously checks the replications status of a key.
        /// </summary>
        /// <param name="op">The <see cref="ObserveParams"/> object.</param>
        /// <param name="replicaIndex">The index of the replica within the <see cref="IVBucket"/></param>
        /// <returns>True if the durability requirements specified by <see cref="PersistTo"/> and <see cref="ReplicateTo"/> have been satisfied.</returns>
        static async Task<bool> CheckReplica(ObserveParams op, int replicaIndex)
        {
            Log.Debug(m=>m("checking replica {0}", replicaIndex));
            if (op.IsDurabilityMet()) return true;

            var replica = op.VBucket.LocateReplica(replicaIndex);
            var result = await Task.Run(()=>replica.Send(new Observe(op.Key, op.VBucket, new AutoByteConverter())));

            Log.Debug(m=>m("Replica {0} - {1} [0]", replica.EndPoint, result.Value, replicaIndex));
            var state = result.Value;
            if (state.KeyState == op.Criteria.PersistState)
            {
                Interlocked.Increment(ref op.ReplicatedToCount);
                Interlocked.Increment(ref op.PersistedToCount);
            }
            else if (state.KeyState == op.Criteria.ReplicateState)
            {
                Interlocked.Increment(ref op.ReplicatedToCount);
            }
            return op.IsDurabilityMet() && !op.HasMutated(state.Cas);
        }

        /// <summary>
        /// Observes a set of keys at a specified interval and timeout.
        /// </summary>
        /// <param name="observe">The func to call at the specific interval</param>
        /// <param name="observeParams">The parameters to pass in.</param>
        /// <param name="interval">The interval to check.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use to terminate the observation at the specified timeout.</param>
        /// <returns>True if the durability requirements specified by <see cref="PersistTo"/> and <see cref="ReplicateTo"/> have been satisfied.</returns>
        static async Task<bool> ObserveEvery(Func<ObserveParams, bool> observe, ObserveParams observeParams, int interval, CancellationToken cancellationToken)
        {
            while (true)
            {
                if (observe(observeParams))
                {
                    return true;
                }

                var task = Task.Delay(interval, cancellationToken);
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
