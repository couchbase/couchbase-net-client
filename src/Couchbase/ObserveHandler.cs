using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Settings;
using Couchbase.Operations;
using System.Threading;
using Enyim.Caching.Configuration;
using Couchbase.Results;
using Enyim.Caching.Memcached;
using System.Threading.Tasks;
using Couchbase.Operations.Constants;
using Enyim.Caching.Memcached.Results.Extensions;
using System.Net;

namespace Couchbase
{
	internal class ObserveHandler
	{
		private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger("ObserveHandler");

		private readonly ObserveSettings _settings;

		public ObserveHandler(ObserveSettings settings)
		{
			_settings = settings;
		}

		/// <summary>
		/// Handle the scenario when PersistTo == 1
		/// </summary>
		public IObserveOperationResult HandleMasterPersistence(ICouchbaseServerPool pool)
		{
			try
			{
				var commandConfig = setupObserveOperation(pool);
				var node = commandConfig.Item2[0] as CouchbaseNode;
				IObserveOperationResult result = new ObserveOperationResult();

				do
				{
					var are = new AutoResetEvent(false);
					var timer = new Timer(state =>
					{
						result = node.ExecuteObserveOperation(commandConfig.Item3);

						if (log.IsDebugEnabled) log.Debug("Node: " + node.EndPoint + ", Result: " + result.KeyState + ", Cas: " + result.Cas + ", Key: " + _settings.Key);

						if (result.Success && result.Cas != _settings.Cas && result.Cas > 0)
						{
							result.Fail(ObserveOperationConstants.MESSAGE_MODIFIED);
							are.Set();
						}
						else if (result.KeyState == ObserveKeyState.FoundPersisted)
						{
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

				} while (result.Message == string.Empty && result.KeyState != ObserveKeyState.FoundPersisted);

				return result;
			}
			catch (Exception ex)
			{
				return new ObserveOperationResult { Success = false, Exception = ex };
			}
		}

		public IObserveOperationResult HandleMasterPersistenceWithReplication(ICouchbaseServerPool pool)
		{
			try
			{
				return performParallelObserve(pool);
			}
			catch (Exception ex)
			{
				return new ObserveOperationResult { Success = false, Exception = ex };
			}
		}

		private IObserveOperationResult performParallelObserve(ICouchbaseServerPool pool)
		{
			var commandConfig = setupObserveOperation(pool);
			var observedNodes = commandConfig.Item2.Select(n => new ObservedNode
			{
				Node = n as CouchbaseNode,
				IsMaster = n == commandConfig.Item2[0]
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
					result = checkNodesForKey(observedNodes, commandConfig.Item3, ref isKeyPersistedToMaster, ref replicaFoundCount, ref replicaPersistedCount);

					if (result.Message == ObserveOperationConstants.MESSAGE_MODIFIED)
					{
						are.Set();
						result.Fail(ObserveOperationConstants.MESSAGE_MODIFIED);
					}
					else if (isInExpectedState(replicaFoundCount, replicaPersistedCount, isKeyPersistedToMaster))
					{
						are.Set();
						result.Pass();
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


			} while (result.Message == string.Empty && !isInExpectedState(replicaFoundCount, replicaPersistedCount, isKeyPersistedToMaster));

			return result;
		}

		private IObserveOperationResult checkNodesForKey(ObservedNode[] nodes, IObserveOperation command, ref bool isMasterInExpectedState, ref int replicaFoundCount, ref int replicaPersistedCount)
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

					if (log.IsDebugEnabled) log.Debug("Node: " + node.Node.EndPoint + ", Result: " + opResult.KeyState + ", Master: " + node.IsMaster + ", Cas: " + opResult.Cas + ", Key: " + _settings.Key);

					if (!opResult.Success) //Probably an IO Exception
					{
						break;
					}
					else if (node.IsMaster && opResult.Cas != _settings.Cas)
					{
						result.Success = false;
						result.Message = ObserveOperationConstants.MESSAGE_MODIFIED;
						break;
					}
					else if (opResult.KeyState == ObserveKeyState.FoundPersisted)
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
					else if (opResult.KeyState == ObserveKeyState.FoundNotPersisted)
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

			if (log.IsDebugEnabled) log.Debug("Master Persisted: " + tmpIsPersistedToMaster + ", Replica Found: " + replicaFoundCount + ", Replica Persisted: " + tmpReplicaPersistedCount);

			return result;
		}

		private bool isInExpectedState(int replicaFoundCount, int replicaPersistedCount, bool masterPersisted)
		{
			var persistedTo = (int)_settings.PersistTo;
			var replicateTo = (int)_settings.ReplicateTo;

			var isExpectedReplication = (replicaFoundCount >= replicateTo || replicaPersistedCount >= replicateTo);
			var isExpectedReplicationPersistence = (replicaPersistedCount >= persistedTo-1); //don't count master
			var isExpectedMasterPersistence = _settings.PersistTo == PersistTo.Zero || ((persistedTo >= 1) && masterPersisted);

			if (log.IsDebugEnabled) log.Debug("Expected Replication: " + isExpectedReplication + ", Expected Persistence: " + isExpectedReplicationPersistence + ", Expected Master Persistence: " + isExpectedMasterPersistence);

			return isExpectedReplication && isExpectedReplicationPersistence && isExpectedMasterPersistence;
		}

		private Tuple<VBucket, CouchbaseNode[], IObserveOperation> setupObserveOperation(ICouchbaseServerPool pool)
		{
			var vbucket = pool.GetVBucket(_settings.Key);
			var command = pool.OperationFactory.Observe(_settings.Key, vbucket.Index, _settings.Cas);

			var workingNodes = pool.GetWorkingNodes().ToArray();

			var masterAndReplicaNodes = new CouchbaseNode[vbucket.Replicas.Count() + 1];

			masterAndReplicaNodes[0] = workingNodes[vbucket.Master] as CouchbaseNode;

			for (var i = 0; i < vbucket.Replicas.Length; i++)
			{
				masterAndReplicaNodes[i + 1] = workingNodes[vbucket.Replicas[i]] as CouchbaseNode;
			}

			return Tuple.Create(vbucket, masterAndReplicaNodes, command);
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