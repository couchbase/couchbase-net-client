using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Operations.Legacy;
using Couchbase.Core.Logging;
using Couchbase.Core.Sharding;
using Couchbase.KeyValue;
using Couchbase.Management.Buckets;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Views;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core
{
    internal abstract class BucketBase : IBucket
    {
        internal const string DefaultScopeName = "_default";
        private static readonly ILogger Log = LogManager.CreateLogger<BucketBase>();
        protected readonly ConcurrentDictionary<string, IScope> Scopes = new ConcurrentDictionary<string, IScope>();

        protected ClusterContext Context;
        protected BucketConfig BucketConfig;
        protected Manifest Manifest;
        internal IKeyMapper KeyMapper;
        protected bool SupportsCollections;
        protected bool Disposed;

        public BucketType BucketType { get; protected set; }

        public string Name { get; protected set; }

        public abstract Task<IScope> this[string name] { get; }

        public Task<IScope> DefaultScopeAsync()
        {
            return Task.FromResult(Scopes[DefaultScopeName]);
        }
        public ICollection Collection(string scopeName, string connectionName)
        {
            throw new NotImplementedException();
        }
        public ICollection DefaultCollection()
        {
            return Scopes[DefaultScopeName][CouchbaseCollection.DefaultCollectionName];
        }

        public abstract Task<IViewResult<T>> ViewQueryAsync<T>(string designDocument, string viewName,
            ViewOptions options = default);

        public abstract IViewIndexManager Views { get; }

        public abstract ICollectionManager Collections { get; }

        internal abstract Task SendAsync(IOperation op, CancellationToken token = default, TimeSpan? timeout = null);

        internal abstract Task BootstrapAsync(IClusterNode node);

        internal abstract void ConfigUpdated(object sender, BucketConfigEventArgs e);

        protected void LoadManifest()
        {
            //The server supports collections so build them from the manifest
            if (SupportsCollections)
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

                    Scopes.TryAdd(scopeDef.name, new Scope(scopeDef.name, scopeDef.uid, collections, this));
                }
            }
            else
            {
                //build a fake scope and collection for pre-6.5 clusters
                var defaultCollection = new CouchbaseCollection(this, Context, null, "_default");
                var defaultScope = new Scope("_default", "0", new List<ICollection> { defaultCollection }, this);
                Scopes.TryAdd("_default", defaultScope);
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
