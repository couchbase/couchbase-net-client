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

#nullable enable

namespace Couchbase.Core
{
    [DebuggerDisplay("Name = {Name}, BucketType = {BucketType}")]
    internal abstract partial class BucketBase : IBucket, IConfigUpdateEventSink, IBootstrappable
    {
        private ClusterState _clusterState;
        protected readonly IScopeFactory _scopeFactory;
        protected readonly ConcurrentDictionary<string, IScope> Scopes = new();
        public readonly ClusterNodeCollection Nodes = new();
        private readonly ILogger _logger;
        private volatile int _disposed;
        private bool? _supportsCollections;

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        protected BucketBase() { }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

        protected BucketBase(string name,
            ClusterContext context,
            IScopeFactory scopeFactory,
            IRetryOrchestrator retryOrchestrator,
            ILogger logger,
            TypedRedactor redactor,
            IBootstrapperFactory bootstrapperFactory,
            IRequestTracer tracer,
            IOperationConfigurator operationConfigurator,
            IRetryStrategy retryStrategy,
            BucketConfig config)
        {
            CurrentConfig = config;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            RetryOrchestrator = retryOrchestrator ?? throw new ArgumentNullException(nameof(retryOrchestrator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            Tracer = tracer;
            RetryStrategy = retryStrategy ?? throw new ArgumentNullException(nameof(retryStrategy));
            BootstrapperFactory = bootstrapperFactory ?? throw new ArgumentNullException(nameof(bootstrapperFactory));
            Bootstrapper = bootstrapperFactory.Create(Context.ClusterOptions.BootstrapPollInterval);
        }

        protected IRetryStrategy RetryStrategy { get; }
        public IBootstrapper Bootstrapper { get; }
        public IBootstrapperFactory BootstrapperFactory { get; }
        protected TypedRedactor Redactor { get; }
        protected IRequestTracer Tracer { get; }
        public ILogger Logger => _logger;
        public ClusterContext Context { get; }
        public IRetryOrchestrator RetryOrchestrator { get; }
        public BucketConfig? CurrentConfig { get; protected set; }
        protected Manifest? Manifest { get; set; }
        public IKeyMapper? KeyMapper { get; protected set; }

        protected bool Disposed => _disposed > 0;

        //for propagating errors during bootstrapping
        private readonly List<Exception> _deferredExceptions = new();

        public BucketType BucketType { get; protected set; }

        public virtual string Name { get; protected set; }

        public IEnumerable<IClusterNode> ClusterNodes => Nodes;

        /// <inheritdoc />
        public ICluster Cluster => Context.Cluster;

        #region Scopes

        public IScope this[string scopeName] => Scope(scopeName);

        public virtual IScope Scope(string scopeName)
        {
            LogScopeLookup(Redactor.UserData(scopeName));

            return Scopes.GetOrAdd(scopeName, _createScopeAction ??= CreateScope);
        }

        // Passed to Scopes.GetOrAdd so we only create the Func<> on the heap once per bucket
        private Func<string, IScope>? _createScopeAction;
        private IScope CreateScope(string scopeName) => _scopeFactory.CreateScope(scopeName, this);

        /// <inheritdoc />
        public ValueTask<IScope> ScopeAsync(string scopeName)
        {
            return new(Scope(scopeName));
        }

        /// <remarks>Volatile</remarks>
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
        public ICouchbaseCollection Collection(string collectionName)
        {
            var scope = DefaultScope();
            return scope[collectionName];
        }

        /// <inheritdoc />
        public async ValueTask<ICouchbaseCollection> CollectionAsync(string collectionName)
        {
            var scope = await DefaultScopeAsync().ConfigureAwait(false);
            return await scope.CollectionAsync(collectionName).ConfigureAwait(false);
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

        #endregion

        #region Send and Retry

        internal abstract Task<ResponseStatus> SendAsync(IOperation op, CancellationTokenPair tokenPair);

        internal Task<ResponseStatus> RetryAsync(IOperation operation, CancellationTokenPair tokenPair) =>
           RetryOrchestrator.RetryAsync(this, operation, tokenPair);

        #endregion

        #region Diagnostics

        public async Task<IPingReport> PingAsync(PingOptions? options = null)
        {
            ThrowIfBootStrapFailed();
            options ??= new PingOptions();
            return await DiagnosticsReportProvider.CreatePingReportAsync(Context, CurrentConfig, options)
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
                        await DiagnosticsReportProvider.CreatePingReportAsync(Context, CurrentConfig,
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

        public bool SupportsCollections
        {
            get
            {
                if (_supportsCollections.HasValue)
                {
                    return _supportsCollections.Value;
                }
                else
                {
                    _supportsCollections = CurrentConfig != null &&
                        CurrentConfig.BucketCapabilities.Contains("collections");
                }
                return _supportsCollections.Value;
            }
        }

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
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                LogDispose(Redactor.UserData(Name));
                Bootstrapper?.Dispose();
                Context.RemoveAllNodes(this);
                Context.RemoveBucket(this);
            }
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

        #region Logging

        [LoggerMessage(1, LogLevel.Debug, "Fetching scope {scopeName}")]
        private partial void LogScopeLookup(Redacted<string> scopeName);

        [LoggerMessage(100, LogLevel.Debug, "Disposing bucket [{name}]!")]
        private partial void LogDispose(Redacted<string> name);

        #endregion
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
