using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Sharding;
using Couchbase.Diagnostics;
using Couchbase.KeyValue;
using Couchbase.Management.Buckets;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Views;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core
{
    internal abstract class BucketBase : IBucket
    {
        internal const string DefaultScopeName = "_default";
        private static readonly ILogger Log = LogManager.CreateLogger<BucketBase>();
        protected readonly ConcurrentDictionary<string, IScope> Scopes = new ConcurrentDictionary<string, IScope>();

        protected BucketBase(string name, ClusterContext context)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public ClusterContext Context { get; }
        public BucketConfig? BucketConfig { get; protected set; }
        protected Manifest? Manifest { get; set; }
        public IKeyMapper? KeyMapper { get; protected set; }
        protected bool Disposed { get; private set; }

        public BucketType BucketType { get; protected set; }

        public string Name { get; protected set; }

        public abstract Task<IScope> this[string name] { get; }

        /// <remarks>Volatile</remarks>
        public IScope DefaultScope()
        {
            return Scopes[DefaultScopeName];
        }

        /// <remarks>Volatile</remarks>
        public ICollection Collection(string collectionName)
        {
            return Scopes[DefaultScopeName][collectionName];
        }
        public ICollection DefaultCollection()
        {
            return Scopes[DefaultScopeName][CouchbaseCollection.DefaultCollectionName];
        }

        /// <inheritdoc />
        public abstract Task<IViewResult<TKey, TValue>> ViewQueryAsync<TKey, TValue>(string designDocument, string viewName,
            ViewOptions? options = default);

        public abstract IViewIndexManager ViewIndexes { get; }

        public abstract ICollectionManager Collections { get; }

        public Task<IPingReport> PingAsync(PingOptions? options = null)
        {
            options ??= new PingOptions();
            return Task.Run(()=> DiagnosticsReportProvider.CreatePingReport(Context, BucketConfig, options));
        }

        internal abstract Task SendAsync(IOperation op, CancellationToken token = default, TimeSpan? timeout = null);

        internal abstract Task BootstrapAsync(IClusterNode node);

        internal abstract void ConfigUpdated(object sender, BucketConfigEventArgs e);

        protected void LoadManifest()
        {
            //The server supports collections so build them from the manifest
            if (Context.SupportsCollections && Manifest != null)
            {
                //warmup the scopes/collections and cache them
                foreach (var scopeDef in Manifest.scopes)
                {
                    var collections = new List<ICollection>();
                    foreach (var collectionDef in scopeDef.collections)
                    {
                        collections.Add(new CouchbaseCollection(this, Context,
                            Convert.ToUInt32(collectionDef.uid, 16), collectionDef.name));
                    }
                    var scope = new Scope(scopeDef.name, scopeDef.uid, collections, this);
                    Scopes.TryAdd(scopeDef.name, scope);
                }
            }
            else
            {
                //build a fake scope and collection for pre-6.5 clusters
                var collections = new List<ICollection>
                {
                    new CouchbaseCollection(this, Context, null, "_default")
                };
                Scopes.TryAdd("_default", new Scope("_default", "0", collections, this));
            }
        }

        public virtual void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            Context.RemoveNodes();
        }
    }
}
