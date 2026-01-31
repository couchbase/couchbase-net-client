using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Diagnostics;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Views;
#pragma warning disable CS0618 // Type or member is obsolete

#nullable enable

namespace Couchbase.UnitTests.Core.Diagnostics.Tracing.Fakes
{
    internal class FakeBucket : IBucket
    {
        public ConcurrentDictionary<string, IScope> _scopes = new ConcurrentDictionary<string, IScope>();
        private readonly ClusterOptions? _clusterOptions;

        public FakeBucket(string name, ClusterOptions clusterOptions)
        {
            Name = name;
            _clusterOptions = clusterOptions;
        }
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }

        public string Name { get; }
        public ICluster Cluster { get; } = null!;

        public Task SendAsync(IOperation operation)
        {
            var fakeConnection = new FakeConnection
            {
                ConnectionId = ConnectionIdProvider.GetNextId(),
                LocalEndPoint = new IPEndPoint(IPAddress.Any, 11210),
                EndPoint = new IPEndPoint(IPAddress.Any, 11210)
            };
            return operation.SendAsync(fakeConnection);
        }
        public IScope Scope(string scopeName)
        {
            throw new NotImplementedException();
        }

        public ValueTask<IScope> ScopeAsync(string scopeName)
        {
            throw new NotImplementedException();
        }

        public IScope DefaultScope()
        {
            throw new NotImplementedException();
        }

        public ValueTask<IScope> DefaultScopeAsync()
        {
            if (_scopes.TryGetValue("_default", out IScope? scope))
            {
                return new ValueTask<IScope>(scope);
            }

            scope = new FakeScope("_default", this, _clusterOptions);
            _scopes.TryAdd("_default", scope);
            return new ValueTask<IScope>(scope);
        }

        public ICouchbaseCollection DefaultCollection()
        {
            throw new NotImplementedException();
        }

        public async ValueTask<ICouchbaseCollection> DefaultCollectionAsync()
        {
            if (_scopes.TryGetValue("_default", out IScope? scope))
            {
                return await scope.CollectionAsync("_default");
            }

            scope = await DefaultScopeAsync();
            return await scope.CollectionAsync("_default");
        }

        public ICouchbaseCollection Collection(string collectionName)
        {
            throw new NotImplementedException();
        }

        public ValueTask<ICouchbaseCollection> CollectionAsync(string collectionName)
        {
            throw new NotImplementedException();
        }

        public Task<IViewResult<TKey, TValue>> ViewQueryAsync<TKey, TValue>(string designDocument, string viewName, ViewOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public IViewIndexManager ViewIndexes { get; } = null!;
        public ICouchbaseCollectionManager Collections { get; } = null!;

        public bool SupportsCollections => throw new NotImplementedException();

        public Task<IPingReport> PingAsync(PingOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task WaitUntilReadyAsync(TimeSpan timeout, WaitUntilReadyOptions? options = null)
        {
            throw new NotImplementedException();
        }
    }
}
