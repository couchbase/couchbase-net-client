using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Retry;
using Couchbase.Core.Sharding;
using Couchbase.Diagnostics;
using Couchbase.KeyValue;
using Couchbase.Management.Buckets;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Views;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Logging;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core
{
    [DebuggerDisplay("Name = {Name}, BucketType = {BucketType}")]
    internal abstract class BucketBase : IBucket, IConfigUpdateEventSink, IBootstrappable
    {
        private ClusterState _clusterState;
        protected readonly IScopeFactory _scopeFactory;
        protected readonly ConcurrentDictionary<string, IScope> Scopes = new ConcurrentDictionary<string, IScope>();
        public readonly ClusterNodeCollection Nodes = new ClusterNodeCollection();

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        protected BucketBase() { }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

        protected BucketBase(string name,
            ClusterContext context,
            IScopeFactory scopeFactory,
            IRetryOrchestrator retryOrchestrator,
            ILogger logger,
            IRedactor redactor,
            IBootstrapperFactory bootstrapperFactory,
            IRequestTracer tracer)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            RetryOrchestrator = retryOrchestrator ?? throw new ArgumentNullException(nameof(retryOrchestrator));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            Tracer = tracer;

            _createDefaultScopeFunc = key => _scopeFactory.CreateDefaultScope(this);

            BootstrapperFactory = bootstrapperFactory ?? throw new ArgumentNullException(nameof(bootstrapperFactory));
            Bootstrapper = bootstrapperFactory.Create(Context.ClusterOptions.BootstrapPollInterval);
        }

        public IBootstrapper Bootstrapper { get; }
        public IBootstrapperFactory BootstrapperFactory { get; }
        protected IRedactor Redactor { get; }
        protected IRequestTracer Tracer { get; }
        public ILogger Logger { get; }
        public ClusterContext Context { get; }
        public IRetryOrchestrator RetryOrchestrator { get; }
        public BucketConfig? BucketConfig { get; protected set; }
        protected Manifest? Manifest { get; set; }
        public IKeyMapper? KeyMapper { get; protected set; }
        protected bool Disposed { get; private set; }

        //for propagating errors during bootstrapping
        private readonly List<Exception> _deferredExceptions = new List<Exception>();

        public BucketType BucketType { get; protected set; }

        public string Name { get; protected set; }

        /// <inheritdoc />
        public ICluster Cluster => Context.Cluster;

        #region Scopes

        [Obsolete("Use asynchronous equivalent instead.")]
        public abstract IScope this[string scopeName] { get; }

        [Obsolete("Use asynchronous equivalent instead.")]
        public virtual IScope Scope(string scopeName)
        {
            if (Scopes.Count == 0)
            {
                LoadManifest();
            }
            if (!Scopes.ContainsKey(scopeName))
            {
                LoadManifest();
            }

            if (Scopes.TryGetValue(scopeName, out var scope))
            {
                return scope;
            }
            throw new ScopeNotFoundException(scopeName);
        }

        /// <inheritdoc />
        public async ValueTask<IScope> ScopeAsync(string scopeName)
        {
            Logger.LogDebug("Fetching scope {scopeName}", Redactor.UserData(scopeName));
            if (scopeName == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(scopeName));
            }

            //Load the default scope for pre-CB7 servers
            LoadDefaultScope();

            // ReSharper disable once AssignNullToNotNullAttribute
            if (Scopes.TryGetValue(scopeName, out var scope))
            {
                return scope;
            }

            var clusterNode = Nodes.FirstOrDefault();
            if (clusterNode == null)
            {
                ThrowHelper.ThrowNodeUnavailableException($"Could not select a node while fetching scope {scopeName} for bucket {Name}");
            }

            // ReSharper disable once PossibleNullReferenceException
            var scopeIdentifier = await clusterNode.GetSid(scopeName).ConfigureAwait(false);
            var newScope = _scopeFactory.CreateScope(scopeName, Convert.ToString(scopeIdentifier.Value), this);
            if (Scopes.TryAdd(scopeName, newScope))
            {
                return newScope;
            }

            throw new ScopeNotFoundException(scopeName);
        }

        /// <remarks>Volatile</remarks>
        [Obsolete("Use asynchronous equivalent instead.")]
        public IScope DefaultScope()
        {
            return Scope(KeyValue.Scope.DefaultScopeName);
        }

        /// <inheritdoc />
        public ValueTask<IScope> DefaultScopeAsync()
        {
            return ScopeAsync(KeyValue.Scope.DefaultScopeName);
        }

        #endregion

        #region Collections

        public abstract ICouchbaseCollectionManager Collections { get; }

        /// <inheritdoc />
        public ValueTask<ICouchbaseCollection> DefaultCollectionAsync()
        {
            return CollectionAsync(CouchbaseCollection.DefaultCollectionName);
        }

        /// <remarks>Volatile</remarks>
        [Obsolete("Use asynchronous equivalent instead.")]
        public ICouchbaseCollection Collection(string collectionName)
        {
            if (Scopes.Count == 0)
            {
                LoadManifest();
            }
            var scope = DefaultScope();
            return scope[collectionName];
        }

        /// <inheritdoc />
        public async ValueTask<ICouchbaseCollection> CollectionAsync(string collectionName)
        {
            var scope = await DefaultScopeAsync().ConfigureAwait(false);
            return await scope.CollectionAsync(collectionName).ConfigureAwait(false);
        }

        [Obsolete("Use asynchronous equivalent instead.")]
        public ICouchbaseCollection DefaultCollection()
        {
            return Collection(CouchbaseCollection.DefaultCollectionName);
        }

        #endregion

        #region Views

        /// <inheritdoc />
        public abstract Task<IViewResult<TKey, TValue>> ViewQueryAsync<TKey, TValue>(string designDocument, string viewName,
            ViewOptions? options = default);

        public abstract IViewIndexManager ViewIndexes { get; }

        #endregion

        #region Config & Manifest

        public abstract Task ConfigUpdatedAsync(BucketConfig config);

        protected void LoadManifest()
        {
            var subject = this as IBootstrappable;

            //The server supports collections so build them from the manifest
            if (Context.SupportsCollections && subject.IsBootstrapped && BucketType != BucketType.Memcached)
            {
                foreach (var scope in _scopeFactory.CreateScopes(this, Manifest!))
                {
                    Scopes.AddOrUpdate(scope.Name, scope, (_, oldScope) => scope);
                }

                return;
            }

            LoadDefaultScope();
        }

        private readonly Func<string, IScope> _createDefaultScopeFunc;

        protected void LoadDefaultScope()
        {
            if (!Context.SupportsCollections)
            {
                //build a fake scope and collection for pre-6.5 clusters or in the bootstrap failure case for deferred error handling
                Scopes.GetOrAdd(Couchbase.KeyValue.Scope.DefaultScopeName, _createDefaultScopeFunc);
            }
        }

        #endregion

        #region Send and Retry

        internal abstract Task SendAsync(IOperation op, CancellationTokenPair tokenPair = default);

        public Task RetryAsync(IOperation operation, CancellationTokenPair tokenPair = default) =>
           RetryOrchestrator.RetryAsync(this, operation, tokenPair);

        #endregion

        #region Diagnostics

        public async Task<IPingReport> PingAsync(PingOptions? options = null)
        {
            ThrowIfBootStrapFailed();
            options ??= new PingOptions();
            return await DiagnosticsReportProvider.CreatePingReportAsync(Context, BucketConfig, options)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Waits until bootstrapping has completed and all services have been initialized.
        /// </summary>
        /// <param name="timeout">The amount of time to wait for the desired <see cref="ClusterState"/>.</param>
        /// <param name="options">The optional arguments.</param>
        public async Task WaitUntilReadyAsync(TimeSpan timeout, WaitUntilReadyOptions? options = null)
        {
            options ??= new WaitUntilReadyOptions();
            if (options?.DesiredStateValue == ClusterState.Offline)
                throw new ArgumentException(nameof(options.DesiredStateValue));

            var token = options?.CancellationTokenValue ?? new CancellationToken();
            CancellationTokenSource? tokenSource = null;
            if (token == CancellationToken.None)
            {
                tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
                tokenSource.CancelAfter(timeout);
                token = tokenSource.Token;
            }

            try
            {
                token.ThrowIfCancellationRequested();
                while (!token.IsCancellationRequested)
                {
                    var pingReport =
                        await DiagnosticsReportProvider.CreatePingReportAsync(Context, BucketConfig,
                            new PingOptions
                            {
                                ServiceTypesValue = options?.ServiceTypesValue
                            }).ConfigureAwait(false);

                    var status = new Dictionary<string, bool>();
                    foreach (var service in pingReport.Services)
                    {
                        var failures = service.Value.Any(x => x.State != ServiceState.Ok);
                        if (failures)
                        {
                            //mark a service as failed
                            status.Add(service.Key, failures);
                        }
                    }

                    //everything is up
                    if (status.Count == 0)
                    {
                        _clusterState = ClusterState.Online;
                        return;
                    }

                    //determine if completely offline or degraded
                    _clusterState = status.Count == pingReport.Services.Count ? ClusterState.Offline : ClusterState.Degraded;
                    if (_clusterState == options?.DesiredStateValue)
                    {
                        return;
                    }

                    await Task.Delay(100, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException e)
            {
                throw new UnambiguousTimeoutException($"Timed out after {timeout}.", e);
            }
            catch (Exception e)
            {
                throw new CouchbaseException("An error has occurred, see the inner exception for details.", e);
            }
            finally
            {
                tokenSource?.Dispose();
            }
        }

        #endregion

        #region Bootstrap Error Handling and Propagation

        internal abstract Task BootstrapAsync(IClusterNode node);

        internal void CaptureException(Exception e)
        {
            var subject = this as IBootstrappable;
            subject.DeferredExceptions.Add(e);
        }

        async Task IBootstrappable.BootStrapAsync()
        {
            await Context.RebootStrapAsync(Name).ConfigureAwait(false);
        }

        /// <summary>
        /// Private implementation of <see cref="IBootstrappable.IsBootstrapped"/> which supports
        /// inlining. The IBootstrappable implementation of IsBootstrapped cannot be inlined.
        /// </summary>
        private bool IsBootstrapped => _deferredExceptions.Count == 0;
        bool IBootstrappable.IsBootstrapped => IsBootstrapped;

        List<Exception> IBootstrappable.DeferredExceptions => _deferredExceptions;

        /// <summary>
        /// Throw an exception if the bucket is not bootstrapped successfully.
        /// </summary>
        internal void ThrowIfBootStrapFailed()
        {
            if (!IsBootstrapped)
            {
                ThrowBootStrapFailed();
            }
        }

        /// <summary>
        /// Throw am AggregateException with deferred bootstrap exceptions.
        /// </summary>
        /// <remarks>
        /// This is a separate method from <see cref="ThrowIfBootStrapFailed"/> to allow that method to
        /// be inlined for the fast, common path where there the bucket is bootstrapped.
        /// </remarks>
        private void ThrowBootStrapFailed()
        {
            throw new AggregateException($"Bootstrapping for bucket {Name} as failed.", _deferredExceptions);
        }

#endregion

        public virtual void Dispose()
        {
            Logger.LogDebug("Disposing bucket [{name}]!", Name);
            if (Disposed) return;
            Disposed = true;
            Bootstrapper?.Dispose();
            Context.RemoveAllNodes(this);
        }

        /// <inheritdoc />
        public virtual ValueTask DisposeAsync()
        {
            try
            {
                Dispose();
                return default;
            }
            catch (Exception ex)
            {
                return new ValueTask(Task.FromException(ex));
            }
        }
    }
}
