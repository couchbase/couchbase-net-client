#if NETCOREAPP3_1_OR_GREATER
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;
using Couchbase.Management.Search;
using Couchbase.Protostellar.Admin.Search.V1;
using Couchbase.Stellar.Core;
using Couchbase.Stellar.Core.Retry;
using Google.Protobuf;
using CreateIndexRequest = Couchbase.Protostellar.Admin.Search.V1.CreateIndexRequest;
using CreateIndexResponse = Couchbase.Protostellar.Admin.Search.V1.CreateIndexResponse;

namespace Couchbase.Stellar.Management.Search;

#nullable enable

internal class StellarSearchIndexManager : ISearchIndexManager
{
    private readonly StellarCluster _stellarCluster;
    private readonly SearchAdminService.SearchAdminServiceClient _stellarSearchAdminClient;
    private readonly IRetryOrchestrator _retryHandler;

    public StellarSearchIndexManager(StellarCluster stellarCluster)
    {
        _stellarCluster = stellarCluster;
        _stellarSearchAdminClient = new SearchAdminService.SearchAdminServiceClient(_stellarCluster.GrpcChannel);
        _retryHandler = stellarCluster.RetryHandler;
    }

    public async Task<SearchIndex> GetIndexAsync(string indexName, GetSearchIndexOptions? options = null, IScope? scope = null)
    {
        var opts = options?.AsReadOnly() ?? GetSearchIndexOptions.DefaultReadOnly;

        var protoRequest = new GetIndexRequest
        {
            Name = indexName
        };

        async Task<GetIndexResponse> GrpcCall()
        {
            return await _stellarSearchAdminClient.GetIndexAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
        }
        var stellarRequest = new StellarRequest
        {
            Idempotent = true,
            Token = opts.TokenValue
        };
        var response = await _retryHandler.RetryAsync(GrpcCall, stellarRequest).ConfigureAwait(false);

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

    public async Task<IEnumerable<SearchIndex>> GetAllIndexesAsync(GetAllSearchIndexesOptions? options = null, IScope? scope = null)
    {
        var opts = options?.AsReadOnly() ?? GetAllSearchIndexesOptions.DefaultReadOnly;
        var protoRequest = new ListIndexesRequest();

        async Task<ListIndexesResponse> GrpcCall()
        {
            return await _stellarSearchAdminClient.ListIndexesAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
        }
        var stellarRequest = new StellarRequest
        {
            Idempotent = true,
            Token = opts.TokenValue
        };
        var response = await _retryHandler.RetryAsync(GrpcCall, stellarRequest).ConfigureAwait(false);

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

    public async Task UpsertIndexAsync(SearchIndex indexDefinition, UpsertSearchIndexOptions? options = null, IScope? scope = null)
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

            async Task<UpdateIndexResponse> GrpcCall()
            {
                return await _stellarSearchAdminClient.UpdateIndexAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
            }
            var stellarRequest = new StellarRequest
            {
                Idempotent = false,
                Token = opts.TokenValue
            };
            _ = await _retryHandler.RetryAsync(GrpcCall, stellarRequest).ConfigureAwait(false);
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

            async Task<CreateIndexResponse> GrpcCall()
            {
                return await _stellarSearchAdminClient.CreateIndexAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
            }
            var stellarRequest = new StellarRequest
            {
                Idempotent = false,
                Token = opts.TokenValue
            };

            _ = await _retryHandler.RetryAsync(GrpcCall, stellarRequest).ConfigureAwait(false);
        }
    }

    public async Task DropIndexAsync(string indexName, DropSearchIndexOptions? options = null, IScope? scope = null)
    {
        var opts = options?.AsReadOnly() ?? DropSearchIndexOptions.DefaultReadOnly;
        var protoRequest = new DeleteIndexRequest
        {
            Name = indexName
        };

        async Task<DeleteIndexResponse> GrpcCall()
        {
            return await _stellarSearchAdminClient.DeleteIndexAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
        }
        var stellarRequest = new StellarRequest
        {
            Idempotent = false,
            Token = opts.TokenValue
        };

        _ = await _retryHandler.RetryAsync(GrpcCall, stellarRequest).ConfigureAwait(false);
    }

    public async Task<int> GetIndexedDocumentsCountAsync(string indexName, GetSearchIndexDocumentCountOptions? options = null, IScope? scope = null)
    {
        var opts = options?.AsReadOnly() ?? GetSearchIndexDocumentCountOptions.DefaultReadOnly;
        var protoRequest = new GetIndexedDocumentsCountRequest
        {
            Name = indexName
        };

        async Task<GetIndexedDocumentsCountResponse> GrpcCall()
        {
            return await _stellarSearchAdminClient.GetIndexedDocumentsCountAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
        }
        var stellarRequest = new StellarRequest
        {
            Idempotent = true,
            Token = opts.TokenValue
        };

        var response = await _retryHandler.RetryAsync(GrpcCall, stellarRequest).ConfigureAwait(false);

        return (int)response.Count;
    }

    public async Task PauseIngestAsync(string indexName, PauseIngestSearchIndexOptions? options = null, IScope? scope = null)
    {
        var opts = options?.AsReadOnly() ?? PauseIngestSearchIndexOptions.DefaultReadOnly;
        var protoRequest = new PauseIndexIngestRequest
        {
            Name = indexName
        };

        async Task<PauseIndexIngestResponse> GrpcCall()
        {
            return await _stellarSearchAdminClient.PauseIndexIngestAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
        }
        var stellarRequest = new StellarRequest
        {
            Idempotent = false,
            Token = opts.TokenValue
        };

        _ = await _retryHandler.RetryAsync(GrpcCall, stellarRequest).ConfigureAwait(false);
    }

    public async Task ResumeIngestAsync(string indexName, ResumeIngestSearchIndexOptions? options = null, IScope? scope = null)
    {
        var opts = options?.AsReadOnly() ?? ResumeIngestSearchIndexOptions.DefaultReadOnly;
        var protoRequest = new ResumeIndexIngestRequest()
        {
            Name = indexName
        };

        async Task<ResumeIndexIngestResponse> GrpcCall()
        {
            return await _stellarSearchAdminClient.ResumeIndexIngestAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
        }
        var stellarRequest = new StellarRequest
        {
            Idempotent = false,
            Token = opts.TokenValue
        };

        _ = await _retryHandler.RetryAsync(GrpcCall, stellarRequest).ConfigureAwait(false);
    }

    public async Task AllowQueryingAsync(string indexName, AllowQueryingSearchIndexOptions? options = null, IScope? scope = null)
    {
        var opts = options?.AsReadOnly() ?? AllowQueryingSearchIndexOptions.DefaultReadOnly;
        var protoRequest = new AllowIndexQueryingRequest()
        {
            Name = indexName
        };

        async Task<AllowIndexQueryingResponse> GrpcCall()
        {
            return await _stellarSearchAdminClient.AllowIndexQueryingAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
        }
        var stellarRequest = new StellarRequest
        {
            Idempotent = false,
            Token = opts.TokenValue
        };

        _ = await _retryHandler.RetryAsync(GrpcCall, stellarRequest).ConfigureAwait(false);
    }

    public async Task DisallowQueryingAsync(string indexName, DisallowQueryingSearchIndexOptions? options = null, IScope? scope = null)
    {
        var opts = options?.AsReadOnly() ?? DisallowQueryingSearchIndexOptions.DefaultReadOnly;
        var protoRequest = new DisallowIndexQueryingRequest()
        {
            Name = indexName
        };

        async Task<DisallowIndexQueryingResponse> GrpcCall()
        {
            return await _stellarSearchAdminClient.DisallowIndexQueryingAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
        }
        var stellarRequest = new StellarRequest
        {
            Idempotent = false,
            Token = opts.TokenValue
        };

        _ = await _retryHandler.RetryAsync(GrpcCall, stellarRequest).ConfigureAwait(false);
    }

    public async Task FreezePlanAsync(string indexName, FreezePlanSearchIndexOptions? options = null, IScope? scope = null)
    {
        var opts = options?.AsReadOnly() ?? FreezePlanSearchIndexOptions.DefaultReadOnly;
        var protoRequest = new FreezeIndexPlanRequest()
        {
            Name = indexName
        };

        async Task<FreezeIndexPlanResponse> GrpcCall()
        {
            return await _stellarSearchAdminClient.FreezeIndexPlanAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
        }
        var stellarRequest = new StellarRequest
        {
            Idempotent = false,
            Token = opts.TokenValue
        };

        _ = await _retryHandler.RetryAsync(GrpcCall, stellarRequest).ConfigureAwait(false);
    }

    public async Task UnfreezePlanAsync(string indexName, UnfreezePlanSearchIndexOptions? options = null, IScope? scope = null)
    {
        var opts = options?.AsReadOnly() ?? UnfreezePlanSearchIndexOptions.DefaultReadOnly;
        var protoRequest = new UnfreezeIndexPlanRequest()
        {
            Name = indexName
        };

        async Task<UnfreezeIndexPlanResponse> GrpcCall()
        {
            return await _stellarSearchAdminClient.UnfreezeIndexPlanAsync(protoRequest, _stellarCluster.GrpcCallOptions(opts.TokenValue)).ConfigureAwait(false);
        }
        var stellarRequest = new StellarRequest
        {
            Idempotent = false,
            Token = opts.TokenValue
        };

        _ = await _retryHandler.RetryAsync(GrpcCall, stellarRequest).ConfigureAwait(false);
    }
}
#endif
