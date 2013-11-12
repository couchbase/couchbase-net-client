using System;
using System.Linq;
using System.Configuration;
using Enyim;
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
using Couchbase.Settings;
using Couchbase.Constants;

namespace Couchbase
{
	/// <summary>D:\dev\couchbase-net-client\src\Couchbase\CouchbaseViewBase.cs
	/// Client which can be used to connect to Couchbase servers.
	/// </summary>
	public class CouchbaseClient : MemcachedClient, IHttpClientLocator, ICouchbaseClient
	{
		private static readonly Enyim.Caching.ILog Log = Enyim.Caching.LogManager.GetLogger(typeof(CouchbaseClient));
		private static readonly ICouchbaseClientConfiguration DefaultConfig = (ICouchbaseClientConfiguration)ConfigurationManager.GetSection("couchbase");

		private readonly INameTransformer _documentNameTransformer;
		private readonly ICouchbaseServerPool _poolInstance;
		private readonly TimeSpan _observeTimeout;

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
			this._documentNameTransformer = configuration.CreateDesignDocumentNameTransformer();
			this._poolInstance = (ICouchbaseServerPool)this.Pool;
			_observeTimeout = configuration.ObserveTimeout;

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

        /// <summary>
        /// Performs the try get.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="cas">The cas.</param>
        /// <param name="value">The value.</param>
        /// <returns></returns>
		protected override IGetOperationResult PerformTryGet(string key, out ulong cas, out object value)
		{
			var hashedKey = this.KeyTransformer.Transform(key);
			var node = this.Pool.Locate(hashedKey);
			var result = GetOperationResultFactory.Create();

			if (node != null)
			{
				var command = this.Pool.OperationFactory.Get(hashedKey);
				var executeResult = ExecuteWithRedirect(node, command);
				result.StatusCode = executeResult.StatusCode;

				if (executeResult.Success)
				{
					result.Value = value = this.Transcoder.Deserialize(command.Result);
					result.Cas = cas = command.CasValue;
					if (this.PerformanceMonitor != null) this.PerformanceMonitor.Get(1, true);

					result.Pass();
					return result;
				}
				else
				{
					value = null;
					cas = 0;
					executeResult.Combine(result);
					return result;
				}

			}

			value = null;
			cas = 0;
			if (this.PerformanceMonitor != null) this.PerformanceMonitor.Get(1, false);

			result.Fail(ClientErrors.FAILURE_NODE_NOT_FOUND);
	        result.StatusCode = StatusCode.UnableToLocateNode.ToInt();
			return result;
		}

        /// <summary>
        /// Performs the mutate.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="key">The key.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <param name="delta">The delta.</param>
        /// <param name="expires">The expires.</param>
        /// <param name="cas">The cas.</param>
        /// <returns></returns>
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
				result.StatusCode = commandResult.StatusCode;

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
			result.Fail(ClientErrors.FAILURE_NODE_NOT_FOUND);
			result.StatusCode = StatusCode.UnableToLocateNode.ToInt();
			return result;
		}

        /// <summary>
        /// Performs the concatenate.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="key">The key.</param>
        /// <param name="cas">The cas.</param>
        /// <param name="data">The data.</param>
        /// <returns></returns>
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

			result.Fail(ClientErrors.FAILURE_NODE_NOT_FOUND);
			result.StatusCode = StatusCode.UnableToLocateNode.ToInt();
			return result;
		}

        /// <summary>
        /// Performs the store.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="expires">The expires.</param>
        /// <param name="cas">The cas.</param>
        /// <param name="statusCode">The status code.</param>
        /// <returns></returns>
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
					Log.Error(e);

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
					commandResult.Combine(result);
					return result;
				}

				if (this.PerformanceMonitor != null) this.PerformanceMonitor.Store(mode, 1, true);

				result.Pass();
				return result;
			}

			if (this.PerformanceMonitor != null) this.PerformanceMonitor.Store(mode, 1, false);

			result.Fail(ClientErrors.FAILURE_NODE_NOT_FOUND);
			result.StatusCode = StatusCode.UnableToLocateNode.ToInt();
			return result;
		}

		private IOperationResult ExecuteWithRedirect(IMemcachedNode startNode, ISingleItemOperation op)
		{
			var opResult = startNode.Execute(op);
			if (opResult.Success) return opResult;

			var iows = op as IOperationWithState;

			// different op factory, we do not know how to retry
			if (iows == null)
			{
				return opResult;
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
						return opResult;
					}

					// the node accepted our request so quit
					if (iows.State != OperationState.InvalidVBucket)
						break;
				}
			}

			//TODO: why would this happen?
			return opResult;
		}

        /// <summary>
        /// Touches the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="nextExpiration">The next expiration.</param>
		public void Touch(string key, DateTime nextExpiration)
		{
			PerformTouch(key, GetExpiration(null, nextExpiration));
		}

        /// <summary>
        /// Touches the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="nextExpiration">The next expiration.</param>
		public void Touch(string key, TimeSpan nextExpiration)
		{
			PerformTouch(key, GetExpiration(nextExpiration, null));
		}

        /// <summary>
        /// Performs the touch.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="nextExpiration">The next expiration.</param>
		protected void PerformTouch(string key, uint nextExpiration)
		{
			var hashedKey = this.KeyTransformer.Transform(key);
			var node = this.Pool.Locate(hashedKey);

			if (node != null)
			{
				var command = this._poolInstance.OperationFactory.Touch(key, nextExpiration);
				var retval = ExecuteWithRedirect(node, command);
			}
		}

        /// <summary>
        /// Gets the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="newExpiration">The new expiration.</param>
        /// <returns></returns>
		public object Get(string key, DateTime newExpiration)
		{
			object tmp;

			return this.TryGet(key, newExpiration, out tmp) ? tmp : null;
		}

        /// <summary>
        /// Gets the specified key.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key.</param>
        /// <param name="newExpiration">The new expiration.</param>
        /// <returns></returns>
		public T Get<T>(string key, DateTime newExpiration)
		{
			object tmp;

			return TryGet(key, newExpiration, out tmp) ? (T)tmp : default(T);
		}

        /// <summary>
        /// Executes the get.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="newExpiration">The new expiration.</param>
        /// <returns></returns>
		public IGetOperationResult ExecuteGet(string key, DateTime newExpiration)
		{
			object tmp;

			return this.ExecuteTryGet(key, newExpiration, out tmp);
		}

        /// <summary>
        /// Executes the get.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key.</param>
        /// <param name="newExpiration">The new expiration.</param>
        /// <returns></returns>
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

        /// <summary>
        /// Tries the get.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="newExpiration">The new expiration.</param>
        /// <param name="value">The value.</param>
        /// <returns></returns>
		public bool TryGet(string key, DateTime newExpiration, out object value)
		{
			ulong cas = 0;

			return this.PerformTryGetAndTouch(key, MemcachedClient.GetExpiration(null, newExpiration), out cas, out value).Success;
		}

        /// <summary>
        /// Executes the try get.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="newExpiration">The new expiration.</param>
        /// <param name="value">The value.</param>
        /// <returns></returns>
		public IGetOperationResult ExecuteTryGet(string key, DateTime newExpiration, out object value)
		{
			ulong cas = 0;

			return this.PerformTryGetAndTouch(key, MemcachedClient.GetExpiration(null, newExpiration), out cas, out value);
		}

        /// <summary>
        /// Tries the get with lock.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="lockExpiration">The lock expiration.</param>
        /// <param name="value">The value.</param>
        /// <returns></returns>
		public IGetOperationResult TryGetWithLock(string key, TimeSpan lockExpiration, out CasResult<object> value)
		{
			object tmp;
			ulong cas;

			var retval = this.PerformTryGetWithLock(key, lockExpiration, out cas, out tmp);
			value = new CasResult<object> { Cas = cas, Result = tmp };

			return retval;
		}

        /// <summary>
        /// Executes the get with lock.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
		public IGetOperationResult ExecuteGetWithLock(string key)
		{
			return this.ExecuteGetWithLock(key, TimeSpan.Zero);
		}

        /// <summary>
        /// Executes the get with lock.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key.</param>
        /// <returns></returns>
		public IGetOperationResult<T> ExecuteGetWithLock<T>(string key)
		{
			return ExecuteGetWithLock<T>(key, TimeSpan.Zero);
		}

        /// <summary>
        /// Executes the get with lock.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="lockExpiration">The lock expiration.</param>
        /// <returns></returns>
		public IGetOperationResult ExecuteGetWithLock(string key, TimeSpan lockExpiration)
		{
			CasResult<object> tmp;
			return this.TryGetWithLock(key, lockExpiration, out tmp);
		}

        /// <summary>
        /// Executes the get with lock.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key.</param>
        /// <param name="lockExpiration">The lock expiration.</param>
        /// <returns></returns>
		public IGetOperationResult<T> ExecuteGetWithLock<T>(string key, TimeSpan lockExpiration)
		{
			CasResult<object> tmp;
			var retVal = new DefaultGetOperationResultFactory<T>().Create();

			var result = TryGetWithLock(key, lockExpiration, out tmp);
			if (result.Success)
			{
				if (result.Value is T)
				{
					result.Copy(retVal);
					retVal.Value = (T)tmp.Result;
					retVal.Cas = result.Cas;
				}
				else
				{
					retVal.Value = default(T);
					retVal.Fail("Type mismatch", new InvalidCastException());
				}
				return retVal;
			}
			retVal.InnerResult = result;
			retVal.Fail(result.Message);
			return retVal;
		}

        /// <summary>
        /// Unlocks the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="cas">The cas.</param>
        /// <returns></returns>
		public bool Unlock(string key, ulong cas)
		{
			return ExecuteUnlock(key, cas).Success;
		}

        /// <summary>
        /// Executes the unlock.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="cas">The cas.</param>
        /// <returns></returns>
		public IUnlockOperationResult ExecuteUnlock(string key, ulong cas)
		{
			return PerformUnlock(key, cas);
		}

        /// <summary>
        /// Gets the with lock.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
		public CasResult<object> GetWithLock(string key)
		{
			return this.GetWithLock<object>(key);
		}

        /// <summary>
        /// Gets the with lock.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key.</param>
        /// <returns></returns>
		public CasResult<T> GetWithLock<T>(string key)
		{
			return GetWithLock<T>(key, TimeSpan.Zero);
		}

        /// <summary>
        /// Gets the with lock.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="lockExpiration">The lock expiration.</param>
        /// <returns></returns>
		public CasResult<object> GetWithLock(string key, TimeSpan lockExpiration)
		{
			return this.GetWithLock<object>(key, lockExpiration);
		}

        /// <summary>
        /// Gets the with lock.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key.</param>
        /// <param name="lockExpiration">The lock expiration.</param>
        /// <returns></returns>
		public CasResult<T> GetWithLock<T>(string key, TimeSpan lockExpiration)
		{
			CasResult<object> tmp;

			return this.TryGetWithLock(key, lockExpiration, out tmp).Success
					? new CasResult<T> { Cas = tmp.Cas, Result = (T)tmp.Result }
					: new CasResult<T> { Cas = tmp.Cas, Result = default(T) };
		}

        /// <summary>
        /// Gets the with cas.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="newExpiration">The new expiration.</param>
        /// <returns></returns>
		public CasResult<object> GetWithCas(string key, DateTime newExpiration)
		{
			return this.GetWithCas<object>(key, newExpiration);
		}

        /// <summary>
        /// Gets the with cas.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">The key.</param>
        /// <param name="newExpiration">The new expiration.</param>
        /// <returns></returns>
		public CasResult<T> GetWithCas<T>(string key, DateTime newExpiration)
		{
			CasResult<object> tmp;

			return this.TryGetWithCas(key, newExpiration, out tmp)
					? new CasResult<T> { Cas = tmp.Cas, Result = (T)tmp.Result }
					: new CasResult<T> { Cas = tmp.Cas, Result = default(T) };
		}

        /// <summary>
        /// Tries the get with cas.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="newExpiration">The new expiration.</param>
        /// <param name="value">The value.</param>
        /// <returns></returns>
		public bool TryGetWithCas(string key, DateTime newExpiration, out CasResult<object> value)
		{
			object tmp;
			ulong cas;

			var retval = this.PerformTryGetAndTouch(key, MemcachedClient.GetExpiration(null, newExpiration), out cas, out tmp).Success;

			value = new CasResult<object> { Cas = cas, Result = tmp };

			return retval;
		}

        /// <summary>
        /// Performs the try get and touch.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="nextExpiration">The next expiration.</param>
        /// <param name="cas">The cas.</param>
        /// <param name="value">The value.</param>
        /// <returns></returns>
		protected IGetOperationResult PerformTryGetAndTouch(string key, uint nextExpiration, out ulong cas, out object value)
		{
			var hashedKey = this.KeyTransformer.Transform(key);
			var node = this.Pool.Locate(hashedKey);
			var result = GetOperationResultFactory.Create();

			if (node != null)
			{
				var command = this._poolInstance.OperationFactory.GetAndTouch(hashedKey, nextExpiration);
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
					result.StatusCode = commandResult.StatusCode;
					result.Fail("Failed to execute Get and Touch operation, see InnerException or StatusCode for details");
					return result;
				}
			}

			value = null;
			cas = 0;
			if (this.PerformanceMonitor != null) this.PerformanceMonitor.Get(1, false);

			result.Fail(ClientErrors.FAILURE_NODE_NOT_FOUND);
			result.StatusCode = StatusCode.UnableToLocateNode.ToInt();
			return result;
		}

        /// <summary>
        /// Performs the try get with lock.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="lockExpiration">The lock expiration.</param>
        /// <param name="cas">The cas.</param>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentOutOfRangeException">Timeout cannot be greater than 30 seconds</exception>
		protected IGetOperationResult PerformTryGetWithLock(string key, TimeSpan lockExpiration, out ulong cas, out object value)
		{
			var hashedKey = this.KeyTransformer.Transform(key);
			var node = this.Pool.Locate(hashedKey);
			var result = GetOperationResultFactory.Create();
			var exp = (uint)lockExpiration.Seconds;

			if (exp > 30) throw new ArgumentOutOfRangeException("Timeout cannot be greater than 30 seconds");

			if (node != null)
			{
				var command = this._poolInstance.OperationFactory.GetWithLock(hashedKey, exp);
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
					commandResult.Combine(result);
					value = null;
					cas = 0;
					return result;
				}
			}

			value = null;
			cas = 0;
			if (this.PerformanceMonitor != null) this.PerformanceMonitor.Get(1, false);

			result.Fail(ClientErrors.FAILURE_NODE_NOT_FOUND);
			result.StatusCode = StatusCode.UnableToLocateNode.ToInt();
			return result;
		}

        /// <summary>
        /// Performs the unlock.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="cas">The cas.</param>
        /// <returns></returns>
		protected IUnlockOperationResult PerformUnlock(string key, ulong cas)
		{
			var hashedKey = this.KeyTransformer.Transform(key);
			var node = this.Pool.Locate(hashedKey);
			var result = new UnlockOperationResult();

			if (node != null)
			{
				var command = this._poolInstance.OperationFactory.Unlock(hashedKey, cas);
				var commandResult = this.ExecuteWithRedirect(node, command);

				if (commandResult.Success)
				{
					result.Pass();
					return result;
				}
				else
				{
					commandResult.Combine(result);
					return result;
				}
			}
			result.Fail(ClientErrors.FAILURE_NODE_NOT_FOUND);
			result.StatusCode = StatusCode.UnableToLocateNode.ToInt();
			return result;
		}

        /// <summary>
        /// Executes the store.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="persistTo">The persist to.</param>
        /// <param name="replciateTo">The replciate to.</param>
        /// <returns></returns>
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


        /// <summary>
        /// Executes the store.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="persistTo">The persist to.</param>
        /// <returns></returns>
		public IStoreOperationResult ExecuteStore(StoreMode mode, string key, object value, PersistTo persistTo)
		{
			return ExecuteStore(mode, key, value, persistTo, ReplicateTo.Zero);
		}

        /// <summary>
        /// Executes the store.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="replicateTo">The replicate to.</param>
        /// <returns></returns>
		public IStoreOperationResult ExecuteStore(StoreMode mode, string key, object value, ReplicateTo replicateTo)
		{
			return ExecuteStore(mode, key, value, PersistTo.Zero, replicateTo);
		}

        /// <summary>
        /// Executes the store.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="expiresAt">The expires at.</param>
        /// <param name="persistTo">The persist to.</param>
        /// <returns></returns>
		public IStoreOperationResult ExecuteStore(StoreMode mode, string key, object value, DateTime expiresAt, PersistTo persistTo)
		{
			return ExecuteStore(mode, key, value, expiresAt, persistTo, ReplicateTo.Zero);
		}

        /// <summary>
        /// Executes the store.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="expiresAt">The expires at.</param>
        /// <param name="replicateTo">The replicate to.</param>
        /// <returns></returns>
		public IStoreOperationResult ExecuteStore(StoreMode mode, string key, object value, DateTime expiresAt, ReplicateTo replicateTo)
		{
			return ExecuteStore(mode, key, value, expiresAt, PersistTo.Zero, replicateTo);
		}

        /// <summary>
        /// Executes the store.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="expiresAt">The expires at.</param>
        /// <param name="persistTo">The persist to.</param>
        /// <param name="replicateTo">The replicate to.</param>
        /// <returns></returns>
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

        /// <summary>
        /// Executes the store.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="validFor">The valid for.</param>
        /// <param name="replicateTo">The replicate to.</param>
        /// <returns></returns>
		public IStoreOperationResult ExecuteStore(StoreMode mode, string key, object value, TimeSpan validFor, ReplicateTo replicateTo)
		{
			return ExecuteStore(mode, key, value, validFor, PersistTo.Zero, replicateTo);
		}

        /// <summary>
        /// Executes the store.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="validFor">The valid for.</param>
        /// <param name="persistTo">The persist to.</param>
        /// <returns></returns>
		public IStoreOperationResult ExecuteStore(StoreMode mode, string key, object value, TimeSpan validFor, PersistTo persistTo)
		{
			return ExecuteStore(mode, key, value, validFor, persistTo, ReplicateTo.Zero);
		}

        /// <summary>
        /// Executes the store.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="validFor">The valid for.</param>
        /// <param name="persistTo">The persist to.</param>
        /// <param name="replciateTo">The replciate to.</param>
        /// <returns></returns>
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

        /// <summary>
        /// Executes the remove.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="persistTo">The persist to.</param>
        /// <param name="replciateTo">The replciate to.</param>
        /// <returns></returns>
		public IRemoveOperationResult ExecuteRemove(string key, PersistTo persistTo, ReplicateTo replciateTo)
		{
			var removeResult = base.ExecuteRemove(key);

			if (persistTo == PersistTo.Zero && replciateTo == ReplicateTo.Zero)
			{
				return removeResult;
			}

			var observeResult = Observe(key, 0, persistTo, replciateTo, ObserveKeyState.NotFound, ObserveKeyState.LogicallyDeleted);

			if (observeResult.Success)
			{
				observeResult.Combine(removeResult);
			}
			else
			{
				observeResult.Combine(removeResult);
			}

			return removeResult;
		}

        /// <summary>
        /// Executes the remove.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="persistTo">The persist to.</param>
        /// <returns></returns>
		public IRemoveOperationResult ExecuteRemove(string key, PersistTo persistTo)
		{
			return ExecuteRemove(key, persistTo, ReplicateTo.Zero);
		}

        /// <summary>
        /// Executes the remove.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="replicateTo">The replicate to.</param>
        /// <returns></returns>
		public IRemoveOperationResult ExecuteRemove(string key, ReplicateTo replicateTo)
		{
			return ExecuteRemove(key, PersistTo.Zero, replicateTo);
		}

        /// <summary>
        /// Keys the exists.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
		public bool KeyExists(string key)
		{
			return KeyExists(key, 0);
		}

        /// <summary>
        /// Keys the exists.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="cas">The cas.</param>
        /// <returns></returns>
		public bool KeyExists(string key, ulong cas)
		{
			var result = Observe(key, cas, PersistTo.Zero, ReplicateTo.Zero);
			return result.Success;
		}

        /// <summary>
        /// Observes the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="cas">The cas.</param>
        /// <param name="persistTo">The persist to.</param>
        /// <param name="replicateTo">The replicate to.</param>
        /// <param name="persistedKeyState">State of the persisted key.</param>
        /// <param name="replicatedState">State of the replicated.</param>
        /// <returns></returns>
		public IObserveOperationResult Observe(string key, ulong cas, PersistTo persistTo, ReplicateTo replicateTo,
											   ObserveKeyState persistedKeyState = ObserveKeyState.FoundPersisted,
											   ObserveKeyState replicatedState = ObserveKeyState.FoundNotPersisted)
		{
			var hashedKey = this.KeyTransformer.Transform(key);
			var vbucket = this._poolInstance.GetVBucket(key);
			var nodes = this._poolInstance.GetWorkingNodes().ToArray();
			var command = this._poolInstance.OperationFactory.Observe(hashedKey, vbucket.Index, cas);
			var runner = new ObserveHandler(new ObserveSettings
			{
				PersistTo = persistTo,
				ReplicateTo = replicateTo,
				Key = hashedKey,
				Cas = cas,
				Timeout = _observeTimeout
			});

			//Master only persistence
			if (replicateTo == ReplicateTo.Zero && persistTo == PersistTo.One)
			{
				return runner.HandleMasterPersistence(_poolInstance, persistedKeyState);
			}
			else if (replicateTo == ReplicateTo.Zero && persistTo == PersistTo.Zero) //used for key exists checks
			{
				return runner.HandleMasterOnlyInCache(_poolInstance);
			}
			else
			{
				return runner.HandleMasterPersistenceWithReplication(_poolInstance, persistedKeyState, replicatedState);
			}

		}

        /// <summary>
        /// Synchronizes the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="cas">The cas.</param>
        /// <param name="mode">The mode.</param>
        /// <returns></returns>
        [Obsolete("Functionality replaced by PersistTo and ReplicateTo parameters.")]
		public SyncResult Sync(string key, ulong cas, SyncMode mode)
		{
			return this.Sync(key, cas, mode, 0);
		}

        /// <summary>
        /// Synchronizes the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="cas">The cas.</param>
        /// <param name="mode">The mode.</param>
        /// <param name="replicationCount">The replication count.</param>
        /// <returns></returns>
        [Obsolete("Functionality replaced by PersistTo and ReplicateTo parameters.")]
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

        /// <summary>
        /// Synchronizes the specified mode.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="items">The items.</param>
        /// <returns></returns>
        [Obsolete("Functionality replaced by PersistTo and ReplicateTo parameters.")]
		public IDictionary<string, SyncResult> Sync(SyncMode mode, IEnumerable<KeyValuePair<string, ulong>> items)
		{
			return this.PerformMultiSync(mode, 0, items);
		}

        /// <summary>
        /// Performs the multi synchronize.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="replicationCount">The replication count.</param>
        /// <param name="items">The items.</param>
        /// <returns></returns>
        [Obsolete("Functionality replaced by PersistTo and ReplicateTo parameters.")]
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

					var sync = this._poolInstance.OperationFactory.Sync(mode, slice.Value, replicationCount);

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
								Log.Error(e);
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

        /// <summary>
        /// Gets the spatial view.
        /// </summary>
        /// <param name="designName">Name of the design.</param>
        /// <param name="viewName">Name of the view.</param>
        /// <returns></returns>
		public ISpatialView<ISpatialViewRow> GetSpatialView(string designName, string viewName)
		{
			getViewSetup(ref designName, ref viewName);
			return new CouchbaseSpatialView(this, this, designName, viewName);
		}

        /// <summary>
        /// Gets the spatial view.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="designName">Name of the design.</param>
        /// <param name="viewName">Name of the view.</param>
        /// <param name="shouldLookupDocById">if set to <c>true</c> [should lookup document by identifier].</param>
        /// <returns></returns>
		public ISpatialView<T> GetSpatialView<T>(string designName, string viewName, bool shouldLookupDocById = false)
		{
			getViewSetup(ref designName, ref viewName);
			return new CouchbaseSpatialView<T>(this, this, designName, viewName, shouldLookupDocById);
		}

        /// <summary>
        /// Gets the specified view.
        /// </summary>
        /// <param name="view">The view.</param>
        /// <returns></returns>
		public IDictionary<string, object> Get(IView view)
		{
			var keys = view.Select(row => row.ItemId);

			return this.Get(keys);
		}

		private void getViewSetup(ref string designName, ref string viewName)
		{
			if (String.IsNullOrEmpty(designName)) throw new ArgumentNullException("designName");
			if (String.IsNullOrEmpty(viewName)) throw new ArgumentNullException("viewName");

			if (this._documentNameTransformer != null)
				designName = this._documentNameTransformer.Transform(designName);
		}

		#region [ IHttpClientLocator		   ]

		IHttpClient IHttpClientLocator.Locate(string designDocument)
		{
			//pick a node at random to avoid overloading a single node with view requests
			var nodes = Pool.GetWorkingNodes()
				.Where(n => n is CouchbaseNode && n.IsAlive)
				.Select(n => n as CouchbaseNode)
				.ToList();

			if (nodes.Count == 0)
			{
				if (Log.IsDebugEnabled) Log.Debug("No working nodes found. Unable to execute view query");
				return null;
			}

			var idx = new Random(Environment.TickCount).Next(nodes.Count);
			var node = nodes[idx] as CouchbaseNode;
			return node.Client;
		}

		#endregion

		#region [ parameter helpers			]

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

		#region MemcachedClient overrides

        /// <summary>
        /// Removes all data from the cache. Note: this will invalidate all data on all servers in the pool.
        /// </summary>
        /// <exception cref="System.NotImplementedException">To flush a Couchbase bucket, use the Couchbase.Management API.</exception>
		public new void FlushAll()
		{
			var couchbaseNodes = _poolInstance.GetWorkingNodes().Where(n => n is CouchbaseNode);
		    if (couchbaseNodes.Any())
		    {
		        throw new NotImplementedException("To flush a Couchbase bucket, use the Couchbase.Management API.");
		    }
		    base.FlushAll();
		}
		#endregion
	}
}

#region [ License information		  ]
/* ************************************************************
 * 
 *	@author Couchbase <info@couchbase.com>
 *	@copyright 2012 Couchbase, Inc.
 *	@copyright 2010 Attila Kiskó, enyim.com
 *	
 *	Licensed under the Apache License, Version 2.0 (the "License");
 *	you may not use this file except in compliance with the License.
 *	You may obtain a copy of the License at
 *	
 *		http://www.apache.org/licenses/LICENSE-2.0
 *	
 *	Unless required by applicable law or agreed to in writing, software
 *	distributed under the License is distributed on an "AS IS" BASIS,
 *	WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *	See the License for the specific language governing permissions and
 *	limitations under the License.
 *	
 * ************************************************************/
#endregion
