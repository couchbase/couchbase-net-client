using Couchbase.Management.Buckets;
using Couchbase.Management.Collections;
using Couchbase.Protostellar.Admin.Collection.V1;
using Grpc.Net.Client;

namespace Couchbase.Stellar.CouchbaseClient.Admin
{
    public class ProtoCollectionManager : ICouchbaseCollectionManager
    {
        private readonly string _bucketName;

        private readonly CollectionAdminService.CollectionAdminServiceClient _collectionAdminClient;

        public ProtoCollectionManager(GrpcChannel channel, string bucketName)
        {
            _bucketName = bucketName;
            _collectionAdminClient = new CollectionAdminService.CollectionAdminServiceClient(channel);
        }

        public Task<ScopeSpec> GetScopeAsync(string scopeName, GetScopeOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<ScopeSpec>> GetAllScopesAsync(GetAllScopesOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task CreateCollectionAsync(string scopeName, string collectionName, CreateCollectionSettings settings, CreateCollectionOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public async Task<ListCollectionsResponse> ListCollections()
        {
            var listCollectionsRequest = new ListCollectionsRequest
            {
                BucketName = _bucketName,
            };

            var response = await _collectionAdminClient
                .ListCollectionsAsync(listCollectionsRequest, new Grpc.Core.CallOptions())
                .ConfigureAwait(false);

            var listCollectionsResponse = new ListCollectionsResponse
            {
                Scopes = { response.Scopes }
            };

            return listCollectionsResponse;

        }

        public async Task CreateCollectionAsync(CollectionSpec spec, CreateCollectionOptions? options = null)
        {
            options ??= CreateCollectionOptions.Default;
            var createCollectionRequest = new CreateCollectionRequest
            {
                BucketName = _bucketName,
                CollectionName = spec.Name,
                ScopeName = spec.ScopeName
            };

            await _collectionAdminClient.CreateCollectionAsync(createCollectionRequest, new Grpc.Core.CallOptions())
                .ConfigureAwait(false);
        }

        public Task DropCollectionAsync(string scopeName, string collectionName, DropCollectionOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public async Task DropCollectionAsync(CollectionSpec spec, DropCollectionOptions? options = null)
        {
            options ??= DropCollectionOptions.Default;
            var dropCollectionRequest = new DeleteCollectionRequest
            {
                BucketName = _bucketName,
                CollectionName = spec.Name,
                ScopeName = spec.ScopeName
            };

            await _collectionAdminClient.DeleteCollectionAsync(dropCollectionRequest, new Grpc.Core.CallOptions())
                .ConfigureAwait(false);
        }

        public async Task CreateScopeAsync(ScopeSpec spec, CreateScopeOptions? options = null)
        {
            options ??= CreateScopeOptions.Default;
            var createScopeRequest = new CreateScopeRequest
            {
                BucketName = _bucketName,
                ScopeName = spec.Name
            };

            await _collectionAdminClient.CreateScopeAsync(createScopeRequest, new Grpc.Core.CallOptions())
                .ConfigureAwait(false);
        }

        public async Task CreateScopeAsync(string scopeName, CreateScopeOptions? options = null)
        {
            options ??= CreateScopeOptions.Default;
            var createScopeRequest = new CreateScopeRequest
            {
                BucketName = _bucketName,
                ScopeName = scopeName
            };

            await _collectionAdminClient.CreateScopeAsync(createScopeRequest, new Grpc.Core.CallOptions())
                .ConfigureAwait(false);
        }

        public async Task DropScopeAsync(string scopeName, DropScopeOptions? options = null)
        {
            options ??= DropScopeOptions.Default;
            var dropScopeRequest = new DeleteScopeRequest
            {
                BucketName = _bucketName,
                ScopeName = scopeName
            };

            await _collectionAdminClient.DeleteScopeAsync(dropScopeRequest, new Grpc.Core.CallOptions())
                .ConfigureAwait(false);
        }

        public Task UpdateCollectionAsync(string scopeName, string collectionName, UpdateCollectionSettings settings,
            UpdateCollectionOptions? options = null)
        {
            throw new NotImplementedException();
        }
    }
}
