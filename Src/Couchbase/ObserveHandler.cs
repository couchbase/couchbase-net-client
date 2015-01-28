﻿using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Settings;
using Couchbase.Operations;
using System.Threading;
using Enyim.Caching.Configuration;
using Couchbase.Results;
using Couchbase.Operations.Constants;
using Enyim.Caching.Memcached.Results.Extensions;

namespace Couchbase
{
    internal class ObserveExpectationException : Exception
    {
        public ObserveExpectationException(string msg)
            : base(msg)
        {
        }
    }

    internal class ObserveHandler
    {
        private static readonly Enyim.Caching.ILog Log = Enyim.Caching.LogManager.GetLogger("ObserveHandler");

        private readonly ObserveSettings _settings;

        public ObserveHandler(ObserveSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Handle the scenario when PersistTo == 0 && ReplicateTo == 0
        /// Primary use case is to check whether key exists without
        /// having to perform a Get + null check
        /// </summary>
        public IObserveOperationResult HandleMasterOnlyInCache(ICouchbaseServerPool pool)
        {
            try
            {
                var commandConfig = SetupObserveOperation(pool);
                var node = commandConfig.CouchbaseNodes[0];
                var result = node.ExecuteObserveOperation(commandConfig.Operation);
                if (Log.IsDebugEnabled) Log.Debug("Node: " + node.EndPoint + ", Result: " + result.KeyState + ", Cas: " + result.Cas + ", Key: " + _settings.Key);

                if ((_settings.Cas == 0 || result.Cas == _settings.Cas) &&
                        (result.KeyState == ObserveKeyState.FoundNotPersisted || result.KeyState == ObserveKeyState.FoundPersisted))
                {
                    result.Pass();
                }
                else
                {
                    result.Fail("Key not found");
                }

                return result;
            }
            catch (ObserveExpectationException ex)
            {
                return new ObserveOperationResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                return new ObserveOperationResult { Success = false, Exception = ex };
            }
        }

        public IObserveOperationResult HandleMasterPersistence(ICouchbaseServerPool pool, ObserveKeyState passingState = ObserveKeyState.FoundPersisted)
        {
            try
            {
                var commandConfig = SetupObserveOperation(pool);
                var node = commandConfig.CouchbaseNodes[0];
                IObserveOperationResult result = new ObserveOperationResult();

                do
                {
                    var are = new AutoResetEvent(false);
                    var timer = new Timer(state =>
                    {
                        result = node.ExecuteObserveOperation(commandConfig.Operation);
                        if (Log.IsDebugEnabled) Log.Debug("Node: " + node.EndPoint + ", Result: " + result.KeyState + ", Cas: " + result.Cas + ", Key: " + _settings.Key);

                        if (result.Success && result.Cas != _settings.Cas && result.Cas > 0 && passingState == ObserveKeyState.FoundPersisted) //don't check CAS for deleted items
                        {
                            result.Fail(ObserveOperationConstants.MESSAGE_MODIFIED);
                            are.Set();
                        }
                        else if (result.KeyState == passingState ||
                                  (result.KeyState == ObserveKeyState.FoundPersisted &&
                                    passingState == ObserveKeyState.FoundNotPersisted)) //if checking in memory, on disk should pass too
                        {
                            //though in memory checks are supported in this condition
                            //a miss will require a timeout
                            result.Pass();
                            are.Set();
                        }
                    }, are, 0, 500);

                    if (!are.WaitOne(_settings.Timeout))
                    {
                        timer.Change(-1, -1);
                        result.Fail(ObserveOperationConstants.MESSAGE_TIMEOUT, new TimeoutException());
                        break;
                    }

                    timer.Change(-1, -1);
                } while (result.Message == string.Empty && result.KeyState != passingState);

                return result;
            }
            catch (ObserveExpectationException ex)
            {
                return new ObserveOperationResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                return new ObserveOperationResult { Success = false, Exception = ex };
            }
        }

        public IObserveOperationResult HandleMasterPersistenceWithReplication(ICouchbaseServerPool pool, ObserveKeyState persistedKeyState, ObserveKeyState replicatedKeyState)
        {
            try
            {
                return PerformParallelObserve(pool, persistedKeyState, replicatedKeyState);
            }
            catch (ObserveExpectationException ex)
            {
                return new ObserveOperationResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                return new ObserveOperationResult { Success = false, Exception = ex };
            }
        }

        private IObserveOperationResult PerformParallelObserve(ICouchbaseServerPool pool, ObserveKeyState persistedKeyState, ObserveKeyState replicatedKeyState)
        {
            var commandConfig = SetupObserveOperation(pool);
            var observedNodes = commandConfig.CouchbaseNodes.Select(n => new ObservedNode
            {
                Node = n,
                IsMaster = n == commandConfig.CouchbaseNodes[0]
            }).ToArray();

            var replicaFoundCount = 0;
            var replicaPersistedCount = 0;
            var isKeyPersistedToMaster = false;

            IObserveOperationResult result = new ObserveOperationResult();

            do
            {
                var are = new AutoResetEvent(false);
                var timer = new Timer(state =>
                {
                    result = CheckNodesForKey(observedNodes, commandConfig.Operation, out isKeyPersistedToMaster, out replicaFoundCount, out replicaPersistedCount, persistedKeyState, replicatedKeyState);

                    if (result.Message == ObserveOperationConstants.MESSAGE_MODIFIED)
                    {
                        are.Set();
                        result.Fail(ObserveOperationConstants.MESSAGE_MODIFIED);
                    }
                    else if (IsInExpectedState(replicaFoundCount, replicaPersistedCount, isKeyPersistedToMaster))
                    {
                        result.Pass();
                        are.Set();
                    }
                }, are, 0, 500);

                if (!are.WaitOne(_settings.Timeout))
                {
                    timer.Change(-1, -1);
                    result.Fail(ObserveOperationConstants.MESSAGE_TIMEOUT, new TimeoutException());
                    return result;
                }

                if (result.Success)
                {
                    timer.Change(-1, -1);
                }
            } while (result.Message == string.Empty && !IsInExpectedState(replicaFoundCount, replicaPersistedCount, isKeyPersistedToMaster));

            return result;
        }

        private IObserveOperationResult CheckNodesForKey(IEnumerable<ObservedNode> nodes, IObserveOperation command, out bool isMasterInExpectedState, out int replicaFoundCount, out int replicaPersistedCount, ObserveKeyState persistedKeyState, ObserveKeyState replicatedKeyState)
        {
            var tmpReplicaFoundCount = 0;
            var tmpReplicaPersistedCount = 0;
            var tmpIsPersistedToMaster = false;
            var result = new ObserveOperationResult();

            var lockObject = new object();
            foreach (var node in nodes)
            {
                lock (lockObject)
                {
                    var opResult = node.Node.ExecuteObserveOperation(command);
                    if (Log.IsDebugEnabled) Log.Debug("Node: " + node.Node.EndPoint + ", Result: " + opResult.KeyState + ", Master: " + node.IsMaster + ", Cas: " + opResult.Cas + ", Key: " + _settings.Key);

                    if (!opResult.Success) //Probably an IO Exception
                    {
                        break;
                    }
                    if (node.IsMaster && opResult.Cas != _settings.Cas &&
                        (persistedKeyState == ObserveKeyState.FoundPersisted ||
                         replicatedKeyState == ObserveKeyState.FoundNotPersisted))
                    {
                        result.Success = false;
                        result.Message = ObserveOperationConstants.MESSAGE_MODIFIED;
                        break;
                    }
                    if (opResult.KeyState == persistedKeyState)
                    {
                        node.KeyIsPersisted = true;
                        if (node.IsMaster)
                        {
                            tmpIsPersistedToMaster = true;
                        }
                        else
                        {
                            tmpReplicaPersistedCount++;
                        }
                    }
                    else if (opResult.KeyState == replicatedKeyState)
                    {
                        if (!node.IsMaster)
                        {
                            tmpReplicaFoundCount++;
                        }
                    }
                }
            }

            isMasterInExpectedState = tmpIsPersistedToMaster;
            replicaFoundCount = tmpReplicaFoundCount;
            replicaPersistedCount = tmpReplicaPersistedCount;

            if (Log.IsDebugEnabled) Log.Debug("Master Persisted: " + tmpIsPersistedToMaster + ", Replica Found: " + replicaFoundCount + ", Replica Persisted: " + tmpReplicaPersistedCount);

            return result;
        }

        private bool IsInExpectedState(int replicaFoundCount, int replicaPersistedCount, bool masterPersisted)
        {
            var persistedTo = (int)_settings.PersistTo;
            var replicateTo = (int)_settings.ReplicateTo;

            var isExpectedReplication = (replicaFoundCount >= replicateTo || replicaPersistedCount >= replicateTo);
            var isExpectedReplicationPersistence = (replicaPersistedCount >= persistedTo - 1); //don't count master
            var isExpectedMasterPersistence = _settings.PersistTo == PersistTo.Zero || ((persistedTo >= 1) && masterPersisted);

            if (Log.IsDebugEnabled) Log.Debug("Expected Replication: " + isExpectedReplication + ", Expected Persistence: " + isExpectedReplicationPersistence + ", Expected Master Persistence: " + isExpectedMasterPersistence);

            return isExpectedReplication && isExpectedReplicationPersistence && isExpectedMasterPersistence;
        }

        private ObserveOperationSetup SetupObserveOperation(ICouchbaseServerPool pool)
        {
            var vbucket = pool.GetVBucket(_settings.Key);

            // Check to see if our persistence requirements can be satisfied
            if (((int)_settings.ReplicateTo > vbucket.Replicas.Length + 1) ||
                 ((int)_settings.PersistTo > vbucket.Replicas.Length + 1))
            {
                throw new ObserveExpectationException(
                    "Requested replication or persistence to more nodes than are currently " +
                    "configured");
            }
            var command = pool.OperationFactory.Observe(_settings.Key, vbucket.Index, _settings.Cas);

            var workingNodes = pool.GetWorkingNodes().Cast<ICouchbaseNode>().ToArray();
            var masterAndReplicaNodes = new List<ICouchbaseNode> {workingNodes[vbucket.Master]};

            for (var i = 0; i < vbucket.Replicas.Length; i++)
            {
                int replicaIndex = vbucket.Replicas[i];
                if (replicaIndex < 0)
                {
                    continue;
                }
                masterAndReplicaNodes.Add(workingNodes[replicaIndex]);
            }

            if (masterAndReplicaNodes.Count < (int)_settings.PersistTo ||
                masterAndReplicaNodes.Count - 1 < (int)_settings.ReplicateTo)
            {
                throw new ObserveExpectationException(
                    "Requested replication or persistence to more nodes than are currently " +
                    "online");
            }

            return new ObserveOperationSetup
            {
                VBucket = vbucket,
                CouchbaseNodes = masterAndReplicaNodes.ToArray(),
                Operation = command
            };
        }

        private class ObserveOperationSetup
        {
            public VBucket VBucket { private get; set; }

            public ICouchbaseNode[] CouchbaseNodes { get; set; }

            public IObserveOperation Operation { get; set; }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
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