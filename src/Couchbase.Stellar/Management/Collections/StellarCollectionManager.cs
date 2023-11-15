using Couchbase.Management.Buckets;
using Couchbase.Management.Collections;
using Couchbase.Protostellar.Admin.Collection.V1;
using Couchbase.Stellar.Util;
using Grpc.Net.Client;

namespace Couchbase.Stellar.Management.Collections;

internal class StellarCollectionManager : ICouchbaseCollectionManager
{
    private readonly string _bucketName;
    private readonly CollectionAdminService.CollectionAdminServiceClient _collectionAdminClient;
    private readonly StellarCluster _stellarCluster;

    public StellarCollectionManager(StellarCluster stellarCluster, string bucketName)
    {
        _bucketName = bucketName;
        _collectionAdminClient = new CollectionAdminService.CollectionAdminServiceClient(stellarCluster.GrpcChannel);
        _stellarCluster = stellarCluster;
    }

    private async Task<ListCollectionsResponse> ListCollections()
    {
        var listCollectionsRequest = new ListCollectionsRequest
        {
            BucketName = _bucketName
        };

        var response = await _collectionAdminClient.ListCollectionsAsync(listCollectionsRequest, _stellarCluster.GrpcCallOptions()).ConfigureAwait(false);

        return response;

    }

    public async Task<IEnumerable<ScopeSpec>> GetAllScopesAsync(GetAllScopesOptions? options = null)
    {
        //TODO: Add AsReadOnly to GetAllScopesOptions
        var collections = await ListCollections().ConfigureAwait(false);
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

        await _collectionAdminClient.CreateCollectionAsync(createCollectionRequest, _stellarCluster.GrpcCallOptions(opts.CancellationToken)).ConfigureAwait(false);
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

        await _collectionAdminClient.DeleteCollectionAsync(dropCollectionRequest, _stellarCluster.GrpcCallOptions(opts.CancellationToken)).ConfigureAwait(false);
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

        await _collectionAdminClient.CreateScopeAsync(createScopeRequest, _stellarCluster.GrpcCallOptions(opts.CancellationToken)).ConfigureAwait(false);
    }

    public async Task DropScopeAsync(string scopeName, DropScopeOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? DropScopeOptions.DefaultReadOnly;
        var dropScopeRequest = new DeleteScopeRequest
        {
            BucketName = _bucketName,
            ScopeName = scopeName
        };

        await _collectionAdminClient.DeleteScopeAsync(dropScopeRequest, _stellarCluster.GrpcCallOptions(opts.CancellationToken))
            .ConfigureAwait(false);
    }

    public Task<ScopeSpec> GetScopeAsync(string scopeName, GetScopeOptions? options = null)
    {
        throw new UnsupportedInProtostellarException(nameof(GetScopeAsync));
    }

    public Task UpdateCollectionAsync(string scopeName, string collectionName, UpdateCollectionSettings settings,
        UpdateCollectionOptions? options = null)
    {
        throw new UnsupportedInProtostellarException(nameof(UpdateCollectionAsync));
    }
}
