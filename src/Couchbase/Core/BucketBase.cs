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
using Couchbase.Core.Logging;

#nullable enable

namespace Couchbase.Core
{
    internal abstract class BucketBase : IBucket, IConfigUpdateEventSink
    {
        private readonly IScopeFactory _scopeFactory;
        protected readonly ConcurrentDictionary<string, IScope> Scopes = new ConcurrentDictionary<string, IScope>();

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        protected BucketBase() { }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

        protected BucketBase(string name, ClusterContext context, IScopeFactory scopeFactory, IRetryOrchestrator retryOrchestrator, ILogger logger, IRedactor redactor)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            RetryOrchestrator = retryOrchestrator ?? throw new ArgumentNullException(nameof(retryOrchestrator));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        }

        protected IRedactor Redactor { get; }
        public ILogger Logger { get; }
        public ClusterContext Context { get; }
        public IRetryOrchestrator RetryOrchestrator { get; }
        public BucketConfig? BucketConfig { get; protected set; }
        protected Manifest? Manifest { get; set; }
        public IKeyMapper? KeyMapper { get; protected set; }
        protected bool Disposed { get; private set; }

        //for propagating errors during bootstrapping
        protected readonly List<Exception> DeferredExceptions = new List<Exception>();
        internal bool BootstrapErrors => DeferredExceptions.Any();
        internal void ThrowIfBootStrapFailed()
        {
            if (BootstrapErrors)
            {
                throw new AggregateException($"Bootstrapping for bucket {Name} as failed.", DeferredExceptions);
            }
        }

        public BucketType BucketType { get; protected set; }

        public string Name { get; protected set; }

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

        /// <remarks>Volatile</remarks>
        public ICollection Collection(string collectionName)
        {
            var scope = DefaultScope();
            return scope[collectionName];
        }

        public ICollection DefaultCollection()
        {
            return Collection(CouchbaseCollection.DefaultCollectionName);
        }

        /// <inheritdoc />
        public abstract Task<IViewResult<TKey, TValue>> ViewQueryAsync<TKey, TValue>(string designDocument, string viewName,
            ViewOptions? options = default);

        public abstract IViewIndexManager ViewIndexes { get; }

        public abstract ICollectionManager Collections { get; }

        public async Task<IPingReport> PingAsync(PingOptions? options = null)
        {
            ThrowIfBootStrapFailed();

            options ??= new PingOptions();
            return await DiagnosticsReportProvider.CreatePingReportAsync(Context, BucketConfig, options)
                .ConfigureAwait(false);
        }

        internal abstract Task SendAsync(IOperation op, CancellationToken token = default, TimeSpan? timeout = null);

        internal abstract Task BootstrapAsync(IClusterNode node);

        public abstract Task ConfigUpdatedAsync(BucketConfig config);

        protected void LoadManifest()
        {
            //The server supports collections so build them from the manifest
            if (Context.SupportsCollections && !DeferredExceptions.Any())
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

        public virtual Task RetryAsync(IOperation operation, CancellationToken token = default, TimeSpan? timeout = null) =>
            RetryOrchestrator.RetryAsync(this, operation, token, timeout);

        internal void CaptureException(Exception e)
        {
            DeferredExceptions.Add(e);
        }

        public virtual void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            Context.RemoveAllNodes();
        }
    }
}
