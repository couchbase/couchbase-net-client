#if NETCOREAPP3_1_OR_GREATER
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Management.Search;
using Couchbase.Protostellar.Admin.Search.V1;
using Couchbase.Stellar.Core;
using Google.Protobuf;

namespace Couchbase.Stellar.Management.Search;

#nullable enable

internal class StellarSearchIndexManager : ISearchIndexManager
{
    private readonly StellarCluster _stellarCluster;
    private readonly SearchAdminService.SearchAdminServiceClient _stellarSearchAdminClient;

    public StellarSearchIndexManager(StellarCluster stellarCluster)
    {
        _stellarCluster = stellarCluster;
        _stellarSearchAdminClient = new SearchAdminService.SearchAdminServiceClient(_stellarCluster.GrpcChannel);
    }

    public async Task<SearchIndex> GetIndexAsync(string indexName, GetSearchIndexOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? GetSearchIndexOptions.DefaultReadOnly;

        var protoRequest = new GetIndexRequest
        {
            Name = indexName
        };

        var response = await _stellarSearchAdminClient.GetIndexAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
        return new SearchIndex
        {
            Type = response.Index.Type,
            Name = response.Index.Name,
            SourceType = response.Index.SourceType,
            SourceName = response.Index.SourceName,
            Uuid = response.Index.Uuid,
            SourceUuid = response.Index.SourceUuid,
            Params = response.Index.Params.ToCore(),
            SourceParams = response.Index.SourceParams.ToCore(),
            PlanParams = response.Index.PlanParams.ToCore()
        };
    }

    public async Task<IEnumerable<SearchIndex>> GetAllIndexesAsync(GetAllSearchIndexesOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? GetAllSearchIndexesOptions.DefaultReadOnly;
        var protoRequest = new ListIndexesRequest();

        var response = await _stellarSearchAdminClient.ListIndexesAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
        return response.Indexes.Select(protoIndex => new SearchIndex
        {
            Type = protoIndex.Type,
            Name = protoIndex.Name,
            SourceType = protoIndex.SourceType,
            SourceName = protoIndex.SourceName,
            Uuid = protoIndex.Uuid,
            SourceUuid = protoIndex.SourceUuid,
            Params = protoIndex.Params.ToCore(),
            SourceParams = protoIndex.SourceParams.ToCore(),
            PlanParams = protoIndex.PlanParams.ToCore()
        });
    }

    public async Task UpsertIndexAsync(SearchIndex indexDefinition, UpsertSearchIndexOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? UpsertSearchIndexOptions.DefaultReadOnly;
        if (indexDefinition.Uuid != null)
        {
            var protoRequest = new UpdateIndexRequest();
            protoRequest.Index.Name = indexDefinition.Name;
            protoRequest.Index.Type = indexDefinition.Type;
            protoRequest.Index.Uuid = indexDefinition.Uuid;
            protoRequest.Index.SourceName = indexDefinition.SourceName;
            protoRequest.Index.SourceType = indexDefinition.SourceType;
            protoRequest.Index.SourceUuid = indexDefinition.SourceUuid;
            foreach (var kvp in indexDefinition.Params)
            {
                protoRequest.Index.Params.Add(kvp.Key, ByteString.CopyFromUtf8(kvp.Value.ToString()));
            }
            foreach (var kvp in indexDefinition.PlanParams)
            {
                protoRequest.Index.PlanParams.Add(kvp.Key, ByteString.CopyFromUtf8(kvp.Value.ToString()));
            }
            foreach (var kvp in indexDefinition.SourceParams)
            {
                protoRequest.Index.SourceParams.Add(kvp.Key, ByteString.CopyFromUtf8(kvp.Value.ToString()));
            }

            await _stellarSearchAdminClient.UpdateIndexAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
        }
        else
        {
            var protoRequest = new CreateIndexRequest();
            protoRequest.Name = indexDefinition.Name;
            protoRequest.Type = indexDefinition.Type;
            protoRequest.SourceName = indexDefinition.SourceName;
            protoRequest.SourceType = indexDefinition.SourceType;
            protoRequest.SourceUuid = indexDefinition.SourceUuid;
            foreach (var kvp in indexDefinition.Params)
            {
                protoRequest.Params.Add(kvp.Key, ByteString.CopyFromUtf8(kvp.Value.ToString()));
            }
            foreach (var kvp in indexDefinition.PlanParams)
            {
                protoRequest.PlanParams.Add(kvp.Key, ByteString.CopyFromUtf8(kvp.Value.ToString()));
            }
            foreach (var kvp in indexDefinition.SourceParams)
            {
                protoRequest.SourceParams.Add(kvp.Key, ByteString.CopyFromUtf8(kvp.Value.ToString()));
            }

            await _stellarSearchAdminClient.CreateIndexAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
        }
    }

    public async Task DropIndexAsync(string indexName, DropSearchIndexOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? DropSearchIndexOptions.DefaultReadOnly;
        var protoRequest = new DeleteIndexRequest
        {
            Name = indexName
        };
        await _stellarSearchAdminClient.DeleteIndexAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
    }

    public async Task<int> GetIndexedDocumentsCountAsync(string indexName, GetSearchIndexDocumentCountOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? GetSearchIndexDocumentCountOptions.DefaultReadOnly;
        var protoRequest = new GetIndexedDocumentsCountRequest
        {
            Name = indexName
        };
        var response = await _stellarSearchAdminClient
            .GetIndexedDocumentsCountAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue))
            .ConfigureAwait(false);

        return (int)response.Count;
    }

    public async Task PauseIngestAsync(string indexName, PauseIngestSearchIndexOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? PauseIngestSearchIndexOptions.DefaultReadOnly;
        var protoRequest = new PauseIndexIngestRequest
        {
            Name = indexName
        };
        await _stellarSearchAdminClient
            .PauseIndexIngestAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
    }

    public async Task ResumeIngestAsync(string indexName, ResumeIngestSearchIndexOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? ResumeIngestSearchIndexOptions.DefaultReadOnly;
        var protoRequest = new ResumeIndexIngestRequest()
        {
            Name = indexName
        };
        await _stellarSearchAdminClient.ResumeIndexIngestAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
    }

    public async Task AllowQueryingAsync(string indexName, AllowQueryingSearchIndexOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? AllowQueryingSearchIndexOptions.DefaultReadOnly;
        var protoRequest = new AllowIndexQueryingRequest()
        {
            Name = indexName
        };
        await _stellarSearchAdminClient.AllowIndexQueryingAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
    }

    public async Task DisallowQueryingAsync(string indexName, DisallowQueryingSearchIndexOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? DisallowQueryingSearchIndexOptions.DefaultReadOnly;
        var protoRequest = new DisallowIndexQueryingRequest()
        {
            Name = indexName
        };
        await _stellarSearchAdminClient.DisallowIndexQueryingAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
    }

    public async Task FreezePlanAsync(string indexName, FreezePlanSearchIndexOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? FreezePlanSearchIndexOptions.DefaultReadOnly;
        var protoRequest = new FreezeIndexPlanRequest()
        {
            Name = indexName
        };
        await _stellarSearchAdminClient.FreezeIndexPlanAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
    }

    public async Task UnfreezePlanAsync(string indexName, UnfreezePlanSearchIndexOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? UnfreezePlanSearchIndexOptions.DefaultReadOnly;
        var protoRequest = new UnfreezeIndexPlanRequest()
        {
            Name = indexName
        };
        await _stellarSearchAdminClient.UnfreezeIndexPlanAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
    }
}
#endif
