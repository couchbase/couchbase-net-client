using System;
using System.Linq;
using System.Configuration;
using Enyim.Caching;
using Enyim.Caching.Memcached;
using Couchbase.Configuration;
using System.Collections.Generic;
using System.Threading;
using KVP_SU = System.Collections.Generic.KeyValuePair<string, ulong>;
using Enyim.Caching.Memcached.Results.Factories;
using Enyim.Caching.Memcached.Results.Extensions;
using Enyim.Caching.Memcached.Results;
using Couchbase.Operations;
using Couchbase.Results;
using Enyim.Caching.Memcached.Protocol.Binary;
using System.Threading.Tasks;
using Couchbase.Settings;

namespace Couchbase
{
	/// <summary>
	/// Client which can be used to connect to NothScale's Memcached and Couchbase servers.
	/// </summary>
	public class CouchbaseClient : MemcachedClient, IHttpClientLocator, ICouchbaseClient, ICouchbaseResultsClient
	{
		private static readonly Enyim.Caching.ILog log = Enyim.Caching.LogManager.GetLogger(typeof(CouchbaseClient));
		private static readonly ICouchbaseClientConfiguration DefaultConfig = (ICouchbaseClientConfiguration)ConfigurationManager.GetSection("couchbase");

		private INameTransformer documentNameTransformer;

		private ICouchbaseServerPool poolInstance;

		private TimeSpan observeTimeout;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Couchbase.CouchbaseClient" /> class using the default configuration and bucket.
		/// </summary>
		/// <remarks>The configuration is taken from the /configuration/Couchbase section.</remarks>
		public CouchbaseClient() : this(DefaultConfig) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Couchbase.CouchbaseClient" /> class using the default configuration and the specified bucket.
		/// </summary>
		/// <remarks>The configuration is taken from the /configuration/Couchbase section.</remarks>
		public CouchbaseClient(string bucketName, string bucketPassword) :
			this(DefaultConfig, bucketName, bucketPassword) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Couchbase.CouchbaseClient" /> class using a custom configuration provider.
		/// </summary>
		/// <param name="configuration">The custom configuration provider.</param>
		public CouchbaseClient(ICouchbaseClientConfiguration configuration) :
			this(configuration, configuration.Bucket, configuration.BucketPassword) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Couchbase.CouchbaseClient" /> class using the specified configuration 
		/// section and the specified bucket.
		/// </summary>
		/// <param name="sectionName">The name of the configuration section to load.</param>
		/// <param name="bucketName">The name of the bucket this client will connect to.</param>
		/// <param name="bucketPassword">The password of the bucket this client will connect to.</param>
		public CouchbaseClient(string sectionName, string bucketName, string bucketPassword) :
			this(If((ICouchbaseClientConfiguration)ConfigurationManager.GetSection(sectionName),
					(o) => { if (o == null) throw new ArgumentException("Missing section: " + sectionName); }),
				bucketName, bucketPassword) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Couchbase.CouchbaseClient" /> class 
		/// using a custom configuration provider and the specified bucket name and password.
		/// </summary>
		/// <param name="configuration">The custom configuration provider.</param>
		/// <param name="bucketName">The name of the bucket this client will connect to.</param>
		/// <param name="bucketPassword">The password of the bucket this client will connect to.</param>
		public CouchbaseClient(ICouchbaseClientConfiguration configuration, string bucketName, string bucketPassword) :
			this(new CouchbasePool(ThrowIfNull(configuration, "configuration"), bucketName, bucketPassword), configuration) { }

		protected CouchbaseClient(ICouchbaseServerPool pool, ICouchbaseClientConfiguration configuration)
			: base(pool,
					configuration.CreateKeyTransformer(),
					configuration.CreateTranscoder(),
					configuration.CreatePerformanceMonitor())
		{
			this.documentNameTransformer = configuration.CreateDesignDocumentNameTransformer();
			this.poolInstance = (ICouchbaseServerPool)this.Pool;
			observeTimeout = configuration.ObserveTimeout;

			StoreOperationResultFactory = new DefaultStoreOperationResultFactory();
			GetOperationResultFactory = new DefaultGetOperationResultFactory();
			MutateOperationResultFactory = new DefaultMutateOperationResultFactory();
			ConcatOperationResultFactory = new DefaultConcatOperationResultFactory();
			RemoveOperationResultFactory = new DefaultRemoveOperationResultFactory();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Couchbase.CouchbaseClient" /> class using the specified configuration 
		/// section.
		/// </summary>
		/// <param name="sectionName">The name of the configuration section to load.</param>
		public CouchbaseClient(string sectionName) :
			this(If((ICouchbaseClientConfiguration)ConfigurationManager.GetSection(sectionName),
					(o) => { if (o == null) throw new ArgumentException("Missing section: " + sectionName); })) { }

		protected override IGetOperationResult PerformTryGet(string key, out ulong cas, out object value)
		{
			var hashedKey = this.KeyTransformer.Transform(key);
			var node = this.Pool.Locate(hashedKey);
			var result = GetOperationResultFactory.Create();

			if (node != null)
			{
				var command = this.Pool.OperationFactory.Get(hashedKey);

				var executeResult = ExecuteWithRedirect(node, command);
				if (executeResult.Success)
				{
					result.Value = value = this.Transcoder.Deserialize(command.Result);
					result.Cas = cas = command.CasValue;
					if (this.PerformanceMonitor != null) this.PerformanceMonitor.Get(1, true);

					result.Pass();
					return result;
				}
			}

			value = null;
			cas = 0;
			if (this.PerformanceMonitor != null) this.PerformanceMonitor.Get(1, false);

			result.Fail("Unable to locate node");
			return result;
		}

		protected override IMutateOperationResult PerformMutate(MutationMode mode, string key, ulong defaultValue, ulong delta, uint expires, ref ulong cas)
		{
			var hashedKey = this.KeyTransformer.Transform(key);
			var node = this.Pool.Locate(hashedKey);
			var result = MutateOperationResultFactory.Create();

			if (node != null)
			{
				var command = this.Pool.OperationFactory.Mutate(mode, hashedKey, defaultValue, delta, expires, cas);
				var commandResult = ExecuteWithRedirect(node, command);

				if (this.PerformanceMonitor != null) this.PerformanceMonitor.Mutate(mode, 1, commandResult.Success);

				result.Cas = cas = command.CasValue;

				if (commandResult.Success)
				{
					result.Value = command.Result;
					result.Pass();
				}
				else
				{
					result.Value = defaultValue;
					result.Fail("Mutate operation failed, see InnerException or StatusCode for details.");
				}

				return result;
			}

			if (this.PerformanceMonitor != null) this.PerformanceMonitor.Mutate(mode, 1, false);

			result.Value = defaultValue;
			result.Fail("Failed to locate node");
			return result;
		}

		protected override IConcatOperationResult PerformConcatenate(ConcatenationMode mode, string key, ref ulong cas, ArraySegment<byte> data)
		{
			var hashedKey = this.KeyTransformer.Transform(key);
			var node = this.Pool.Locate(hashedKey);
			var result = ConcatOperationResultFactory.Create();

			if (node != null)
			{
				var command = this.Pool.OperationFactory.Concat(mode, hashedKey, cas, data);
				var commandResult = this.ExecuteWithRedirect(node, command);

				result.Cas = cas = command.CasValue;
				result.StatusCode = command.StatusCode;

				if (!commandResult.Success)
				{
					result.InnerResult = commandResult;
					result.Fail("Concat operation failed, see InnerResult or StatusCode for more information");
					return result;
				}

				if (this.PerformanceMonitor != null) this.PerformanceMonitor.Concatenate(mode, 1, true);

				result.Pass();
				return result;
			}

			if (this.PerformanceMonitor != null) this.PerformanceMonitor.Concatenate(mode, 1, false);

			result.Fail("Failed to locate node");
			return result;
		}

		protected override IStoreOperationResult PerformStore(StoreMode mode, string key, object value, uint expires, ref ulong cas, out int statusCode)
		{
			var hashedKey = this.KeyTransformer.Transform(key);
			var node = this.Pool.Locate(hashedKey);
			var result = StoreOperationResultFactory.Create();
			statusCode = -1;

			if (node != null)
			{
				CacheItem item;

				try { item = this.Transcoder.Serialize(value); }
				catch (Exception e)
				{
					log.Error(e);

					if (this.PerformanceMonitor != null) this.PerformanceMonitor.Store(mode, 1, false);

					result.Fail("Store operation failed during serialization", e);
					return result;
				}

				var command = this.Pool.OperationFactory.Store(mode, hashedKey, item, expires, cas);
				var commandResult = ExecuteWithRedirect(node, command);

				result.Cas = cas = command.CasValue;
				result.StatusCode = statusCode = command.StatusCode;

				if (!commandResult.Success)
				{
					result.InnerResult = commandResult;
					result.Fail("Store operation failed, see InnerResult or StatusCode for details");
					return result;
				}

				if (this.PerformanceMonitor != null) this.PerformanceMonitor.Store(mode, 1, true);

				result.Pass();
				return result;
			}

			if (this.PerformanceMonitor != null) this.PerformanceMonitor.Store(mode, 1, false);

			result.Fail("Failed to locate node");
			return result;
		}

		private IOperationResult ExecuteWithRedirect(IMemcachedNode startNode, ISingleItemOperation op)
		{
			var result = new BinaryOperationResult();

			var opResult = startNode.Execute(op);
			if (opResult.Success) return result.Pass();

			var iows = op as IOperationWithState;

			// different op factory, we do not know how to retry
			if (iows == null)
			{
				result.InnerResult = opResult.InnerResult;
				return result.Fail("Operation state was invalid");
			}

#if HAS_FORWARD_MAP
			// node responded with invalid vbucket
			// this should happen only when a node is in a transitioning state
			if (iows.State == OpState.InvalidVBucket)
			{
				// check if we have a forward-locator
				// (whihc supposedly reflects the state of the cluster when all vbuckets have been migrated succesfully)
				IMemcachedNodeLocator fl = this.nsPool.ForwardLocator;
				if (fl != null)
				{
					var nextNode = fl.Locate(op.Key);
					if (nextNode != null)
					{
						// the node accepted the requesta
						if (nextNode.Execute(op)) return true;
					}
				}
			}
#endif
			// still invalid vbucket, try all nodes in sequence
			if (iows.State == OperationState.InvalidVBucket)
			{
				var nodes = this.Pool.GetWorkingNodes();

				foreach (var node in nodes)
				{
					opResult = node.Execute(op);
					if (opResult.Success)
					{
						return result.Pass();
					}

					// the node accepted our request so quit
					if (iows.State != OperationState.InvalidVBucket)
						break;
				}
			}

			//TODO: why would this happen?
			return result.Fail("Failed to execute operation");
		}

		public void Touch(string key, DateTime nextExpiration)
		{
			PerformTouch(key, GetExpiration(null, nextExpiration));
		}

		public void Touch(string key, TimeSpan nextExpiration)
		{
			PerformTouch(key, GetExpiration(nextExpiration, null));
		}

		protected void PerformTouch(string key, uint nextExpiration)
		{
			var hashedKey = this.KeyTransformer.Transform(key);
			var node = this.Pool.Locate(hashedKey);

			if (node != null)
			{
				var command = this.poolInstance.OperationFactory.Touch(key, nextExpiration);
				var retval = ExecuteWithRedirect(node, command);
			}
		}

		public object Get(string key, DateTime newExpiration)
		{
			object tmp;

			return this.TryGet(key, newExpiration, out tmp) ? tmp : null;
		}

		public T Get<T>(string key, DateTime newExpiration)
		{
			object tmp;

			return TryGet(key, newExpiration, out tmp) ? (T)tmp : default(T);
		}

		public IGetOperationResult ExecuteGet(string key, DateTime newExpiration)
		{
			object tmp;

			return this.ExecuteTryGet(key, newExpiration, out tmp);
		}

		public IGetOperationResult<T> ExecuteGet<T>(string key, DateTime newExpiration)
		{
			object tmp;
			var result = new DefaultGetOperationResultFactory<T>().Create();

			var tryGetResult = ExecuteTryGet(key, newExpiration, out tmp);
			if (tryGetResult.Success)
			{
				if (tryGetResult.Value is T)
				{
					//HACK: this isn't optimal
					tryGetResult.Copy(result);

					result.Value = (T)tmp;
					result.Cas = tryGetResult.Cas;
				}
				else
				{
					result.Value = default(T);
					result.Fail("Type mismatch", new InvalidCastException());
				}
				return result;
			}
			result.InnerResult = tryGetResult;
			result.Fail("Get failed. See InnerResult or StatusCode for details");
			return result;
		}

		public bool TryGet(string key, DateTime newExpiration, out object value)
		{
			ulong cas = 0;

			return this.PerformTryGetAndTouch(key, MemcachedClient.GetExpiration(null, newExpiration), out cas, out value).Success;
		}

		public IGetOperationResult ExecuteTryGet(string key, DateTime newExpiration, out object value)
		{
			ulong cas = 0;

			return this.PerformTryGetAndTouch(key, MemcachedClient.GetExpiration(null, newExpiration), out cas, out value);
		}

		public CasResult<object> GetWithCas(string key, DateTime newExpiration)
		{
			return this.GetWithCas<object>(key, newExpiration);
		}

		public CasResult<T> GetWithCas<T>(string key, DateTime newExpiration)
		{
			CasResult<object> tmp;

			return this.TryGetWithCas(key, newExpiration, out tmp)
					? new CasResult<T> { Cas = tmp.Cas, Result = (T)tmp.Result }
					: new CasResult<T> { Cas = tmp.Cas, Result = default(T) };
		}

		public bool TryGetWithCas(string key, DateTime newExpiration, out CasResult<object> value)
		{
			object tmp;
			ulong cas;

			var retval = this.PerformTryGetAndTouch(key, MemcachedClient.GetExpiration(null, newExpiration), out cas, out tmp).Success;

			value = new CasResult<object> { Cas = cas, Result = tmp };

			return retval;
		}

		protected IGetOperationResult PerformTryGetAndTouch(string key, uint nextExpiration, out ulong cas, out object value)
		{
			var hashedKey = this.KeyTransformer.Transform(key);
			var node = this.Pool.Locate(hashedKey);
			var result = GetOperationResultFactory.Create();

			if (node != null)
			{
				var command = this.poolInstance.OperationFactory.GetAndTouch(hashedKey, nextExpiration);
				var commandResult = this.ExecuteWithRedirect(node, command);

				if (commandResult.Success)
				{
					result.Value = value = this.Transcoder.Deserialize(command.Result);
					result.Cas = cas = command.CasValue;
					if (this.PerformanceMonitor != null) this.PerformanceMonitor.Get(1, true);

					result.Pass();
					return result;
				}
				else
				{
					cas = 0;
					value = null;
					result.InnerResult = commandResult;
					result.Fail("Failed to execute Get and Touch operation, see InnerException or StatusCode for details");
				}
			}

			value = null;
			cas = 0;
			if (this.PerformanceMonitor != null) this.PerformanceMonitor.Get(1, false);

			result.Fail("Unable to locate node");
			return result;
		}

		public IStoreOperationResult ExecuteStore(StoreMode mode, string key, object value, PersistTo persistTo, ReplicateTo replciateTo)
		{
			var storeResult = base.ExecuteStore(mode, key, value);
			if (persistTo == PersistTo.Zero && replciateTo == ReplicateTo.Zero)
			{
				return storeResult;
			}
			var observeResult = Observe(key, storeResult.Cas, persistTo, replciateTo);

			if (observeResult.Success)
			{
				storeResult.Pass();
			}
			else
			{
				observeResult.Copy(storeResult);
			}

			return storeResult;
		}


		public IStoreOperationResult ExecuteStore(StoreMode mode, string key, object value, PersistTo persistTo)
		{
			return ExecuteStore(mode, key, value, persistTo, ReplicateTo.Zero);
		}

		public IStoreOperationResult ExecuteStore(StoreMode mode, string key, object value, ReplicateTo replicateTo)
		{
			return ExecuteStore(mode, key, value, PersistTo.Zero, replicateTo);
		}

		public IStoreOperationResult ExecuteStore(StoreMode mode, string key, object value, DateTime expiresAt, PersistTo persistTo)
		{
			return ExecuteStore(mode, key, value, expiresAt, persistTo, ReplicateTo.Zero);
		}

		public IStoreOperationResult ExecuteStore(StoreMode mode, string key, object value, DateTime expiresAt, ReplicateTo replicateTo)
		{
			return ExecuteStore(mode, key, value, expiresAt, PersistTo.Zero, replicateTo);
		}

		public IStoreOperationResult ExecuteStore(StoreMode mode, string key, object value, DateTime expiresAt, PersistTo persistTo, ReplicateTo replicateTo)
		{
			var storeResult = base.ExecuteStore(mode, key, value, expiresAt);

			if (persistTo == PersistTo.Zero && replicateTo == ReplicateTo.Zero)
			{
				return storeResult;
			}

			var observeResult = Observe(key, storeResult.Cas, persistTo, replicateTo);

			if (observeResult.Success)
			{
				storeResult.Pass();
			}
			else
			{
				observeResult.Copy(storeResult);
			}

			return storeResult;
		}

		public IStoreOperationResult ExecuteStore(StoreMode mode, string key, object value, TimeSpan validFor, ReplicateTo replicateTo)
		{
			return ExecuteStore(mode, key, value, validFor, PersistTo.Zero, replicateTo);
		}

		public IStoreOperationResult ExecuteStore(StoreMode mode, string key, object value, TimeSpan validFor, PersistTo persistTo)
		{
			return ExecuteStore(mode, key, value, validFor, persistTo, ReplicateTo.Zero);
		}

		public IStoreOperationResult ExecuteStore(StoreMode mode, string key, object value, TimeSpan validFor, PersistTo persistTo, ReplicateTo replciateTo)
		{
			var storeResult = base.ExecuteStore(mode, key, value, validFor);

			if (persistTo == PersistTo.Zero && replciateTo == ReplicateTo.Zero)
			{
				return storeResult;
			}

			var observeResult = Observe(key, storeResult.Cas, persistTo, replciateTo);

			if (observeResult.Success)
			{
				observeResult.Combine(storeResult);
			}
			else
			{
				observeResult.Combine(storeResult);
			}

			return storeResult;
		}

		public IObserveOperationResult Observe(string key, ulong cas, PersistTo persistTo, ReplicateTo replicateTo)
		{
			var hashedKey = this.KeyTransformer.Transform(key);
			var vbucket = this.poolInstance.GetVBucket(key);
			var nodes = this.poolInstance.GetWorkingNodes().ToArray();
			var command = this.poolInstance.OperationFactory.Observe(hashedKey, vbucket.Index, cas);
			var runner = new ObserveHandler(new ObserveSettings
			{
				PersistTo = persistTo,
				ReplicateTo = replicateTo,
				Key = hashedKey,
				Cas = cas,
				Timeout = observeTimeout
			});

			//Master only persistence
			if (replicateTo == ReplicateTo.Zero && persistTo == PersistTo.One)
			{
				return runner.HandleMasterPersistence(poolInstance);
			}
			else
			{
				return runner.HandleMasterPersistenceWithReplication(poolInstance);
			}

		}

		public SyncResult Sync(string key, ulong cas, SyncMode mode)
		{
			return this.Sync(key, cas, mode, 0);
		}

		public SyncResult Sync(string key, ulong cas, SyncMode mode, int replicationCount)
		{
			var hashedKey = this.KeyTransformer.Transform(key);
			var node = this.Pool.Locate(hashedKey);

			var tmp = this.PerformMultiSync(mode, replicationCount, new[] { new KeyValuePair<string, ulong>(key, cas) });
			SyncResult retval;

			return tmp.TryGetValue(key, out retval)
				? retval
				: null;
		}

		public IDictionary<string, SyncResult> Sync(SyncMode mode, IEnumerable<KeyValuePair<string, ulong>> items)
		{
			return this.PerformMultiSync(mode, 0, items);
		}

		protected IDictionary<string, SyncResult> PerformMultiSync(SyncMode mode, int replicationCount, IEnumerable<KeyValuePair<string, ulong>> items)
		{
			// transform the keys and index them by hashed => original
			// the results will be mapped using this index
			var hashed = new Dictionary<string, string>();
			var hashedAndMapped = new Dictionary<IMemcachedNode, IList<KVP_SU>>();

			foreach (var k in items)
			{
				var hashedKey = this.KeyTransformer.Transform(k.Key);
				var node = this.Pool.Locate(hashedKey);

				if (node == null) continue;

				hashed[hashedKey] = k.Key;

				IList<KVP_SU> list;
				if (!hashedAndMapped.TryGetValue(node, out list))
					hashedAndMapped[node] = list = new List<KVP_SU>(4);

				list.Add(k);
			}

			var retval = new Dictionary<string, SyncResult>(hashed.Count);
			if (hashedAndMapped.Count == 0) return retval;

			using (var spin = new ReaderWriterLockSlim())
			using (var latch = new Enyim.Caching.CountdownEvent(hashedAndMapped.Count))
			{
				//execute each list of keys on their respective node
				foreach (var slice in hashedAndMapped)
				{
					var node = slice.Key;
					var nodeKeys = slice.Value;

					var sync = this.poolInstance.OperationFactory.Sync(mode, slice.Value, replicationCount);

					#region result gathering
					// ExecuteAsync will not call the delegate if the
					// node was already in a failed state but will return false immediately
					var execSuccess = node.ExecuteAsync(sync, success =>
					{
						if (success)
							try
							{
								var result = sync.Result;

								if (result != null && result.Length > 0)
								{
									string original;

									foreach (var kvp in result)
										if (hashed.TryGetValue(kvp.Key, out original))
										{
											spin.EnterWriteLock();
											try
											{ retval[original] = kvp; }
											finally
											{ spin.ExitWriteLock(); }
										}
								}
							}
							catch (Exception e)
							{
								log.Error(e);
							}

						latch.Signal();
					});
					#endregion

					// signal the latch when the node fails immediately (e.g. it was already dead)
					if (!execSuccess) latch.Signal();
				}

				latch.Wait();
			}

			return retval;
		}

		/// <summary>
		/// Returns an object representing the specified view in the specified design document.
		/// </summary>
		/// <param name="designName">The name of the design document.</param>
		/// <param name="viewName">The name of the view.</param>
		/// <returns></returns>
		public IView<IViewRow> GetView(string designName, string viewName)
		{
			getViewSetup(ref designName, ref viewName);
			return new CouchbaseView(this, this, designName, viewName);
		}

		/// <summary>
		/// Returns an object representing the specified view in the specified design document.
		/// </summary>
		/// <param name="designName">The name of the design document.</param>
		/// <param name="viewName">The name of the view.</param>
		/// <param name="shouldLookupDocById">
		///		When true, the client will return an instance of T by deserializing the document that
		///		is retrieved using the row's id.  When false (use when emitting projections), the client 
		///		simply attempts to deserialize the view's value into an instance of T by matching properties.
		/// </param>
		/// <returns></returns>
		public IView<T> GetView<T>(string designName, string viewName, bool shouldLookupDocById = false)
		{

			getViewSetup(ref designName, ref viewName);
			return new CouchbaseView<T>(this, this, designName, viewName, shouldLookupDocById);
		}

		public IDictionary<string, object> Get(IView view)
		{
			var keys = view.Select(row => row.ItemId);

			return this.Get(keys);
		}

		private void getViewSetup(ref string designName, ref string viewName)
		{
			if (String.IsNullOrEmpty(designName)) throw new ArgumentNullException("designName");
			if (String.IsNullOrEmpty(viewName)) throw new ArgumentNullException("viewName");

			if (this.documentNameTransformer != null)
				designName = this.documentNameTransformer.Transform(designName);
		}

		#region [ IHttpClientLocator           ]

		IHttpClient IHttpClientLocator.Locate(string designDocument)
		{
			// find the node hosting this design document
			var node = this.Pool.Locate(designDocument) as CouchbaseNode;

			// return null if the node is dead
			return (node != null && node.IsAlive)
					? node.Client
					: null;
		}

		#endregion

		#region [ parameter helpers            ]

		private static T ThrowIfNull<T>(T input, string parameterName)
			where T : class
		{
			if (input == null) throw new ArgumentNullException(parameterName);

			return input;
		}

		private static T If<T>(T input, Action<T> check)
		{
			check(input);

			return input;
		}

		#endregion
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
