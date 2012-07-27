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

namespace Couchbase
{
	internal class ObserveHandler
	{
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

				var node = commandConfig.Item2[commandConfig.Item1.Master] as CouchbaseNode;
				IObserveOperationResult result = new ObserveOperationResult();

				do
				{
					result.Success = retryWithTimer(result, state =>
					{
						var stateInfo = state as AutoResetEvent;
						result = node.ExecuteObserveOperation(commandConfig.Item3);

						if (result.Success && result.Cas > 0 && result.Cas != _settings.Cas)
						{
							result.Success = false;
							result.Message = ObserveOperationConstants.MESSAGE_MODIFIED;
							stateInfo.Set();
						}
						else if (result.KeyState == ObserveKeyState.FoundPersisted)
						{
							result.Success = true;
							stateInfo.Set();
						}
					});

				} while (result.KeyState != ObserveKeyState.FoundPersisted);

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

		private bool retryWithTimer(IObserveOperationResult previousResult, TimerCallback tcb)
		{
			var autoResetEvent = new AutoResetEvent(false);

			var pollingInterval = Math.Max(previousResult.ReplicationStats, previousResult.PersistenceStats);
			pollingInterval = pollingInterval > 0 ? pollingInterval : 400; //default to 400ms when unknown server stats

			var timer = new Timer(state =>
			{
				tcb(state);

			}, autoResetEvent, 0, pollingInterval);

			if (autoResetEvent.WaitOne(_settings.Timeout))
			{
				timer.Change(Timeout.Infinite, Timeout.Infinite);
				timer.Dispose();
				return true;
			}
			else
			{
				return false;
			}
		}

		private IObserveOperationResult performParallelObserve(ICouchbaseServerPool pool)
		{
			var commandConfig = setupObserveOperation(pool);

			var i = 0;
			var observedNodes = commandConfig.Item2.Select(n => new ObservedNode
			{
				Node = n as CouchbaseNode,
				IsMaster = i++ == commandConfig.Item1.Master
			}).ToArray();

			var replicaFoundCount = 0;
			var replicaPersistedCount = 0;
			var isKeyPersistedToMaster = false;

			IObserveOperationResult result = new ObserveOperationResult();

			Func<int, int, bool, bool> isInExpectedState = (f, p, m) =>
			{
				var persisteTo = (int)_settings.PersistTo;
				var replicateTo = (int)_settings.ReplicateTo;
				
				//check whether the key has been replicated (found) or persisted to the specified 
				//number of replicas or it's been persisted to the specified number of nodes.
				return (f >= replicateTo || p >= replicateTo) || (p >= persisteTo);
							
			};

			do
			{
				var autoResetEvent = new AutoResetEvent(false);
				result.Success = retryWithTimer(result, state =>
				{
					result = checkNodesForKey(observedNodes, commandConfig.Item3, ref isKeyPersistedToMaster, ref replicaFoundCount, ref replicaPersistedCount);

					if (isInExpectedState(replicaFoundCount, replicaPersistedCount, isKeyPersistedToMaster))
					{
						(state as AutoResetEvent).Set();
						result.Success = true;
					}
				});
			
			} while (!isInExpectedState(replicaFoundCount, replicaPersistedCount, isKeyPersistedToMaster));

			return result;
		}

		private IObserveOperationResult checkNodesForKey(ObservedNode[] nodes, IObserveOperation command, ref bool isMasterInExpectedState, ref int replicaFoundCount, ref int replicaPersistedCount)
		{
			var tmpReplicaFoundCount = 0;
			var tmpReplicaPersistedCount = 0;
			var tmpIsPersistedToMaster = false;
			var result = new ObserveOperationResult();

			//if node already marked as having persisted the node, don't recheck
			//var nodesToRecheck = nodes.Where(n => !n.KeyIsPersisted).ToArray();

			var lockObject = new object();
			Parallel.ForEach(nodes, (n, s) =>
			{
				lock (lockObject)
				{
					var opResult = n.Node.ExecuteObserveOperation(command);
					if (!opResult.Success) //Probably an IO Exception
					{
						s.Stop();
					}
					else if (n.IsMaster && opResult.Cas > 0 && opResult.Cas != _settings.Cas)
					{
						result.Success = false;
						result.Message = ObserveOperationConstants.MESSAGE_MODIFIED;
						s.Stop();
					}
					else if (opResult.KeyState == ObserveKeyState.FoundPersisted)
					{
						n.KeyIsPersisted = true;
						if (n.IsMaster)
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
						tmpReplicaFoundCount++;
					}
				}
			});

			isMasterInExpectedState = tmpIsPersistedToMaster;
			replicaFoundCount = tmpReplicaFoundCount;
			replicaPersistedCount = tmpReplicaPersistedCount;
			return result;
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