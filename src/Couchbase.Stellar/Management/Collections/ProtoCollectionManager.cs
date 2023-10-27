using Couchbase.Management.Buckets;
using Couchbase.Management.Collections;
using Couchbase.Protostellar.Admin.Collection.V1;
using Grpc.Net.Client;

namespace Couchbase.Stellar.Management.Collections;

internal class ProtoCollectionManager : ICouchbaseCollectionManager
{
    private readonly string _bucketName;
    private readonly CollectionAdminService.CollectionAdminServiceClient _collectionAdminClient;
    private readonly ProtoCluster _protoCluster;

    public ProtoCollectionManager(ProtoCluster protoCluster, string bucketName)
    {
        _bucketName = bucketName;
        _collectionAdminClient = new CollectionAdminService.CollectionAdminServiceClient(protoCluster.GrpcChannel);
        _protoCluster = protoCluster;
    }

    private async Task<ListCollectionsResponse> ListCollections()
    {
        var listCollectionsRequest = new ListCollectionsRequest
        {
            BucketName = _bucketName
        };

        var response = await _collectionAdminClient.ListCollectionsAsync(listCollectionsRequest, _protoCluster.GrpcCallOptions()).ConfigureAwait(false);

        return response;

    }

    public Task<ScopeSpec> GetScopeAsync(string scopeName, GetScopeOptions? options = null)
    {
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<ScopeSpec>> GetAllScopesAsync(GetAllScopesOptions? options = null)
    {
        var collections = await ListCollections().ConfigureAwait(false);
        var allScopes = collections.Scopes.Select(scope => new ScopeSpec(scope.Name)
        {
            Collections = scope.Collections.Select(collection => new CollectionSpec(scope.Name, collection.Name)).ToList()
        }).ToList();

        return allScopes;
    }

    public async Task CreateCollectionAsync(CollectionSpec spec, CreateCollectionOptions? options = null)
    {
        await CreateCollectionAsync(spec.ScopeName, spec.Name, new CreateCollectionSettings(spec.MaxExpiry, spec.History), options).ConfigureAwait(false);
    }

    public async Task CreateCollectionAsync(string scopeName, string collectionName, CreateCollectionSettings settings, CreateCollectionOptions? options = null)
    {
        options ??= CreateCollectionOptions.Default;
        var createCollectionRequest = new CreateCollectionRequest
        {
            BucketName = _bucketName,
            CollectionName = collectionName,
            ScopeName = scopeName
        };
        if (settings.MaxExpiry.HasValue) createCollectionRequest.MaxExpirySecs = (uint)settings.MaxExpiry.Value.TotalSeconds;

        await _collectionAdminClient.CreateCollectionAsync(createCollectionRequest, _protoCluster.GrpcCallOptions()).ConfigureAwait(false);
    }

    public async Task DropCollectionAsync(CollectionSpec spec, DropCollectionOptions? options = null)
    {
        await DropCollectionAsync(spec.ScopeName, spec.Name, options).ConfigureAwait(false);
    }

    public async Task DropCollectionAsync(string scopeName, string collectionName, DropCollectionOptions? options = null)
    {
        options ??= DropCollectionOptions.Default;
        var dropCollectionRequest = new DeleteCollectionRequest
        {
            BucketName = _bucketName,
            CollectionName = collectionName,
            ScopeName = scopeName
        };

        await _collectionAdminClient.DeleteCollectionAsync(dropCollectionRequest, _protoCluster.GrpcCallOptions()).ConfigureAwait(false);
    }

    public async Task CreateScopeAsync(ScopeSpec spec, CreateScopeOptions? options = null)
    {
        await CreateScopeAsync(spec.Name, options).ConfigureAwait(false);
    }

    public async Task CreateScopeAsync(string scopeName, CreateScopeOptions? options = null)
    {
        options ??= CreateScopeOptions.Default;
        var createScopeRequest = new CreateScopeRequest
        {
            BucketName = _bucketName,
            ScopeName = scopeName
        };

        await _collectionAdminClient.CreateScopeAsync(createScopeRequest, _protoCluster.GrpcCallOptions()).ConfigureAwait(false);
    }

    public async Task DropScopeAsync(string scopeName, DropScopeOptions? options = null)
    {
        options ??= DropScopeOptions.Default;
        var dropScopeRequest = new DeleteScopeRequest
        {
            BucketName = _bucketName,
            ScopeName = scopeName
        };

        await _collectionAdminClient.DeleteScopeAsync(dropScopeRequest, _protoCluster.GrpcCallOptions())
            .ConfigureAwait(false);
    }

    public Task UpdateCollectionAsync(string scopeName, string collectionName, UpdateCollectionSettings settings,
        UpdateCollectionOptions? options = null)
    {
        throw new NotImplementedException();
    }
}
