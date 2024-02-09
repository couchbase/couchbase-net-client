#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.Bootstrapping;
using Couchbase.Diagnostics;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Protostellar.Query.V1;
using Couchbase.Stellar.KeyValue;
using Couchbase.Stellar.Management.Collections;
using Couchbase.Stellar.Util;
using Couchbase.Utils;
using Couchbase.Views;

namespace Couchbase.Stellar;

#nullable enable

internal class StellarBucket : IBucket
{
    private readonly StellarCluster _stellarCluster;
    private readonly StellarCollectionManager _collectionManager;
    private readonly ConcurrentDictionary<string, IScope> _scopes = new();
    private volatile bool _disposed;

    internal StellarBucket(string name, StellarCluster stellarCluster, QueryService.QueryServiceClient queryClient)
    {
        Name = name;
        _stellarCluster = stellarCluster;
        _collectionManager = new StellarCollectionManager(_stellarCluster, name);
    }

    public bool SupportsCollections => true;

    public string Name { get; }

    public ICluster Cluster
    {
        get
        {
            CheckIfDisposed();
            return _stellarCluster;
        }
    }

    public IViewIndexManager ViewIndexes => throw new UnsupportedInProtostellarException(nameof(ViewIndexes));

    public ICouchbaseCollectionManager Collections
    {
        get
        {
            CheckIfDisposed();
            return _collectionManager;
        }
    }

    public ICouchbaseCollection Collection(string collectionName)
    {
        CheckIfDisposed();
        return DefaultScope().Collection(collectionName);
    }

    public ValueTask<ICouchbaseCollection> CollectionAsync(string collectionName)
    {
        CheckIfDisposed();
        return ValueTask.FromResult(Collection(collectionName));
    }

    public ICouchbaseCollection DefaultCollection()
    {
        CheckIfDisposed();
        return DefaultScope().Collection("_default");
    }

    public ValueTask<ICouchbaseCollection> DefaultCollectionAsync()
    {
        CheckIfDisposed();
        return ValueTask.FromResult(DefaultCollection());
    }

    public IScope DefaultScope()
    {
        CheckIfDisposed();
        return Scope("_default");
    }

    public ValueTask<IScope> DefaultScopeAsync()
    {
        CheckIfDisposed();
        return ValueTask.FromResult(DefaultScope());
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var cluster = _stellarCluster as IClusterExtended;
        cluster.RemoveBucket(Name);
        _scopes.Clear();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public Task<IPingReport> PingAsync(PingOptions? options = null)
    {
        throw new UnsupportedInProtostellarException("Ping Bucket");
    }

    public IScope Scope(string scopeName)
    {
        CheckIfDisposed();
        return _scopes.GetOrAdd(scopeName, new StellarScope(scopeName, this, _stellarCluster));
    }

    public ValueTask<IScope> ScopeAsync(string scopeName)
    {
        CheckIfDisposed();
        return ValueTask.FromResult(Scope(scopeName));
    }

    public Task<IViewResult<TKey, TValue>> ViewQueryAsync<TKey, TValue>(string designDocument, string viewName, ViewOptions? options = null)
    {
        throw new UnsupportedInProtostellarException("Bucket View Queries");
    }

    public Task WaitUntilReadyAsync(TimeSpan timeout, WaitUntilReadyOptions? options = null)
    {
        throw new UnsupportedInProtostellarException("Bucket WaitUntilReady");
    }

    private void CheckIfDisposed()
    {
        if (_disposed)
        {
            ThrowHelper.ThrowObjectDisposedException(nameof(StellarBucket));
        }
    }
}
#endif
