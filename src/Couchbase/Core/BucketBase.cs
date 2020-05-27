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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Logging;

#nullable enable

namespace Couchbase.Core
{
    internal abstract class BucketBase : IBucket, IConfigUpdateEventSink, IBootstrappable
    {
        private ClusterState _clusterState;
        private readonly IScopeFactory _scopeFactory;
        protected readonly ConcurrentDictionary<string, IScope> Scopes = new ConcurrentDictionary<string, IScope>();
        public readonly ClusterNodeCollection Nodes = new ClusterNodeCollection();

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        protected BucketBase() { }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

        protected BucketBase(string name, ClusterContext context, IScopeFactory scopeFactory, IRetryOrchestrator retryOrchestrator, ILogger logger, IRedactor redactor, IBootstrapperFactory bootstrapperFactory)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            RetryOrchestrator = retryOrchestrator ?? throw new ArgumentNullException(nameof(retryOrchestrator));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));

            BootstrapperFactory = bootstrapperFactory ?? throw new ArgumentNullException(nameof(bootstrapperFactory));
            Bootstrapper = bootstrapperFactory.Create(Context.ClusterOptions.BootstrapPollInterval);
        }

        public IBootstrapper Bootstrapper { get; }
        public IBootstrapperFactory BootstrapperFactory { get; }
        protected IRedactor Redactor { get; }
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

        #region Scopes

        public abstract IScope this[string scopeName] { get; }

        public virtual IScope Scope(string scopeName)
        {
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

        /// <remarks>Volatile</remarks>
        public IScope DefaultScope()
        {
            return Scope(KeyValue.Scope.DefaultScopeName);
        }

        #endregion

        #region Collections

        public abstract ICouchbaseCollectionManager Collections { get; }

        /// <remarks>Volatile</remarks>
        public ICouchbaseCollection Collection(string collectionName)
        {
            var scope = DefaultScope();
            return scope[collectionName];
        }

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
                    Scopes.TryAdd(scope.Name, scope);
                }
            }
            else
            {
                //build a fake scope and collection for pre-6.5 clusters or in the bootstrap failure case
                //for deferred error handling
                var defaultScope = _scopeFactory.CreateDefaultScope(this);
                Scopes.TryAdd(defaultScope.Name, defaultScope);
            }
        }

        #endregion

        #region Send and Retry

        internal abstract Task SendAsync(IOperation op, CancellationToken token = default, TimeSpan? timeout = null);

        public virtual Task RetryAsync(IOperation operation, CancellationToken token = default, TimeSpan? timeout = null) =>
            RetryOrchestrator.RetryAsync(this, operation, token, timeout);

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

        bool IBootstrappable.IsBootstrapped => !_deferredExceptions.Any();

        List<Exception> IBootstrappable.DeferredExceptions => _deferredExceptions;

        internal void ThrowIfBootStrapFailed()
        {
            var subject = this as IBootstrappable;
            if (!subject.IsBootstrapped)
            {
                throw new AggregateException($"Bootstrapping for bucket {Name} as failed.", subject.DeferredExceptions);
            }
        }

#endregion

        public virtual void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            Bootstrapper?.Dispose();
            Context.RemoveAllNodes();
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
