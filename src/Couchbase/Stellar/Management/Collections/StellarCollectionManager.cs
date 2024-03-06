#if NETCOREAPP3_1_OR_GREATER
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Retry;
using Couchbase.Management.Buckets;
using Couchbase.Management.Collections;
using Couchbase.Protostellar.Admin.Collection.V1;
using Couchbase.Stellar.Core.Retry;
using Couchbase.Stellar.Util;
using Couchbase.Utils;

namespace Couchbase.Stellar.Management.Collections;

#nullable enable

internal class StellarCollectionManager : ICouchbaseCollectionManager
{
    private readonly string _bucketName;
    private readonly CollectionAdminService.CollectionAdminServiceClient _collectionAdminClient;
    private readonly StellarCluster _stellarCluster;
    private readonly IRetryOrchestrator _retryHandler;

    public StellarCollectionManager(StellarCluster stellarCluster, string bucketName)
    {
        _bucketName = bucketName;
        _collectionAdminClient = new CollectionAdminService.CollectionAdminServiceClient(stellarCluster.GrpcChannel);
        _stellarCluster = stellarCluster;
        _retryHandler = stellarCluster.RetryHandler;
    }

    private async Task<ListCollectionsResponse> ListCollections(CancellationToken? cancellationToken)
    {
        var listCollectionsRequest = new ListCollectionsRequest
        {
            BucketName = _bucketName
        };

        var callOptions = cancellationToken.HasValue ? _stellarCluster.GrpcCallOptions() : _stellarCluster.GrpcCallOptions(cancellationToken!.Value);

        async Task<ListCollectionsResponse> grpcCall()
        {
            return await _collectionAdminClient.ListCollectionsAsync(listCollectionsRequest, callOptions).ConfigureAwait(false);
        }
        var stellarRequest = new StellarRequest
        {
            Idempotent = false
        };
        var response = await _retryHandler.RetryAsync(grpcCall, stellarRequest).ConfigureAwait(false);

        return response;

    }

    public async Task<IEnumerable<ScopeSpec>> GetAllScopesAsync(GetAllScopesOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? GetAllScopesOptions.DefaultReadOnly;
        var collections = await ListCollections(opts.CancellationToken).ConfigureAwait(false);
        var allScopes = collections.Scopes.Select(scope => new ScopeSpec(scope.Name)
        {
            Collections = scope.Collections.Select(collection => new CollectionSpec(scope.Name, collection.Name))
                .ToList()
        });

        return allScopes;
    }

    public async Task CreateCollectionAsync(CollectionSpec spec, CreateCollectionOptions? options = null)
    {
        await CreateCollectionAsync(spec.ScopeName, spec.Name, new CreateCollectionSettings(spec.MaxExpiry, spec.History), options).ConfigureAwait(false);
    }

    public async Task CreateCollectionAsync(string scopeName, string collectionName, CreateCollectionSettings settings, CreateCollectionOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? CreateCollectionOptions.DefaultReadOnly;
        var createCollectionRequest = new CreateCollectionRequest
        {
            BucketName = _bucketName,
            CollectionName = collectionName,
            ScopeName = scopeName
        };
        if (settings.MaxExpiry.HasValue) createCollectionRequest.MaxExpirySecs = (uint)settings.MaxExpiry.Value.TotalSeconds;

        async Task<CreateCollectionResponse> grpcCall()
        {
            return await _collectionAdminClient.CreateCollectionAsync(createCollectionRequest, _stellarCluster.GrpcCallOptions(opts.CancellationToken)).ConfigureAwait(false);
        }
        var stellarRequest = new StellarRequest
        {
            Idempotent = false,
            Token = opts.CancellationToken
        };
        _ = await _retryHandler.RetryAsync(grpcCall, stellarRequest).ConfigureAwait(false);
    }

    public async Task DropCollectionAsync(CollectionSpec spec, DropCollectionOptions? options = null)
    {
        await DropCollectionAsync(spec.ScopeName, spec.Name, options).ConfigureAwait(false);
    }

    public async Task DropCollectionAsync(string scopeName, string collectionName, DropCollectionOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? DropCollectionOptions.DefaultReadOnly;
        var dropCollectionRequest = new DeleteCollectionRequest
        {
            BucketName = _bucketName,
            CollectionName = collectionName,
            ScopeName = scopeName
        };

        async Task<DeleteCollectionResponse> grpcCall()
        {
            return await _collectionAdminClient.DeleteCollectionAsync(dropCollectionRequest, _stellarCluster.GrpcCallOptions(opts.CancellationToken)).ConfigureAwait(false);
        }
        var stellarRequest = new StellarRequest
        {
            Idempotent = false,
            Token = opts.CancellationToken
        };

        _ = await _retryHandler.RetryAsync(grpcCall, stellarRequest).ConfigureAwait(false);
    }

    public async Task CreateScopeAsync(ScopeSpec spec, CreateScopeOptions? options = null)
    {
        await CreateScopeAsync(spec.Name, options).ConfigureAwait(false);
    }

    public async Task CreateScopeAsync(string scopeName, CreateScopeOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? CreateScopeOptions.DefaultReadOnly;
        var createScopeRequest = new CreateScopeRequest
        {
            BucketName = _bucketName,
            ScopeName = scopeName
        };

        async Task<CreateScopeResponse> grpcCall()
        {
            return await _collectionAdminClient.CreateScopeAsync(createScopeRequest, _stellarCluster.GrpcCallOptions(opts.CancellationToken)).ConfigureAwait(false);
        }
        var stellarRequest = new StellarRequest
        {
            Idempotent = false,
            Token = opts.CancellationToken
        };

        _ = await _retryHandler.RetryAsync(grpcCall, stellarRequest).ConfigureAwait(false);
    }

    public async Task DropScopeAsync(string scopeName, DropScopeOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? DropScopeOptions.DefaultReadOnly;
        var dropScopeRequest = new DeleteScopeRequest
        {
            BucketName = _bucketName,
            ScopeName = scopeName
        };

        async Task<DeleteScopeResponse> grpcCall()
        {
            return await _collectionAdminClient.DeleteScopeAsync(dropScopeRequest, _stellarCluster.GrpcCallOptions(opts.CancellationToken)).ConfigureAwait(false);
        }
        var stellarRequest = new StellarRequest
        {
            Idempotent = false,
            Token = opts.CancellationToken
        };

        _ = await _retryHandler.RetryAsync(grpcCall, stellarRequest).ConfigureAwait(false);
    }

    public Task<ScopeSpec> GetScopeAsync(string scopeName, GetScopeOptions? options = null) =>
        throw ThrowHelper.ThrowFeatureNotAvailableException(nameof(GetScopeAsync), "Protostellar");

    public Task UpdateCollectionAsync(string scopeName, string collectionName, UpdateCollectionSettings settings,
        UpdateCollectionOptions? options = null) =>
        throw ThrowHelper.ThrowFeatureNotAvailableException(nameof(GetScopeAsync), "Protostellar");
}
#endif
