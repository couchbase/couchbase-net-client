using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Logging;
using Couchbase.Query;
using Microsoft.Extensions.Logging;

namespace Couchbase.Management.Query
{
    internal class QueryIndexes : IQueryIndexes
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<QueryIndexes>();
        private static readonly TimeSpan WatchIndexSleepDuration = TimeSpan.FromMilliseconds(50);

        private readonly IQueryClient _queryClient;

        public QueryIndexes(IQueryClient queryClient)
        {
            _queryClient = queryClient;
        }

        public async Task BuildDeferredAsync(string bucketName, BuildDeferredQueryIndexOptions options)
        {
            Logger.LogInformation($"Attempting to build deferred query indexes on bucket {bucketName}");

            try
            {
                var indexes = await this.GetAllAsync(bucketName,
                    queryOptions => queryOptions.WithCancellationToken(options.CancellationToken)
                );

                var tasks = new List<Task>();
                foreach (var index in indexes.Where(i => i.State == "pending" || i.State == "deferred"))
                {
                    var statement = $"BUILD INDEX ON {bucketName}({index.Name}) USING GSI;";
                    tasks.Add(_queryClient.QueryAsync<dynamic>(statement,
                        queryOptions => queryOptions.WithCancellationToken(options.CancellationToken)
                    ));
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Error trying to build deferred query indexes on {bucketName}");
                throw;
            }
        }

        public async Task CreateAsync(string bucketName, string indexName, IEnumerable<string> fields, CreateQueryIndexOptions options)
        {
            Logger.LogInformation($"Attempting to create query index {indexName} on bucket {bucketName}");

            try
            {
                var statement = $"CREATE INDEX {indexName} ON {bucketName}({string.Join(",", fields)}) USING GSI WITH {{\"defer_build\":{options.Deferred}}};";
                await _queryClient.QueryAsync<dynamic>(statement,
                    queryOptions => queryOptions.WithCancellationToken(options.CancellationToken)
                );
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Error trying to create query index {indexName} on {bucketName}");
                throw;
            }
        }

        public async Task CreatePrimaryAsync(string bucketName, CreatePrimaryQueryIndexOptions options)
        {
            Logger.LogInformation($"Attempting to create primary query index on bucket {bucketName}");

            try
            {
                var statement = $"CREATE PRIMARY INDEX ON {bucketName} USING GSI WITH {{\"defer_build\":{options.Deferred}}};";
                await _queryClient.QueryAsync<dynamic>(statement,
                    queryOptions => queryOptions.WithCancellationToken(options.CancellationToken)
                );
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Error trying to create primary query index on {bucketName}");
                throw;
            }
        }

        public async Task DropAsync(string bucketName, string indexName, DropQueryIndexOptions options)
        {
            Logger.LogInformation($"Attempting to drop query index {indexName} on bucket {bucketName}");

            try
            {
                var statement = $"DROP INDEX {bucketName}.{indexName} USING GSI;";
                await _queryClient.QueryAsync<dynamic>(statement,
                    queryOptions => queryOptions.WithCancellationToken(options.CancellationToken)
                );
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Error trying to drop query index {indexName} on {bucketName}");
                throw;
            }
        }

        public async Task DropPrimaryAsync(string bucketName, DropPrimaryQueryIndexOptions options)
        {
            Logger.LogInformation($"Attempting to drop primary query index on bucket {bucketName}");

            try
            {
                var statement = $"DROP PRIMARY INDEX ON {bucketName} USING GSI;";
                await _queryClient.QueryAsync<dynamic>(statement,
                    queryOptions => queryOptions.WithCancellationToken(options.CancellationToken)
                );
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Error trying to drop query index on {bucketName}");
                throw;
            }
        }

        public async Task<IEnumerable<QueryIndex>> GetAllAsync(string bucketName, GetAllQueryIndexOptions options)
        {
            Logger.LogInformation($"Attempting to get query indexes for bucket {bucketName}");

            try
            {
                var statement = $"SELECT i.* FROM system:indexes AS i WHERE i.keyspace_id=\"{bucketName}\" AND `using`=\"gsi\";";
                var result = await _queryClient.QueryAsync<QueryIndex>(statement,
                    queryOptions => queryOptions.WithCancellationToken(options.CancellationToken)
                );

                var indexes = new List<QueryIndex>();
                foreach (var row in result)
                {
                    indexes.Add(row);
                }

                return indexes;
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Error trying to get query indexes for bucket {bucketName}");
                throw;
            }
        }

        public async Task WatchAsync(string bucketName, IEnumerable<string> indexNames, WatchQueryIndexOptions options)
        {
            var indexesToWatch = string.Join(", ", indexNames.ToList());
            Logger.LogInformation($"Attempting to watch pending indexes ({indexesToWatch}) for bucket {bucketName}");

            try
            {
                while (!options.CancellationToken.IsCancellationRequested)
                {
                    var indexes = await this.GetAllAsync(bucketName,
                        queryOptions => queryOptions.WithCancellationToken(options.CancellationToken)
                    );

                    var pendingIndexes = indexes.Where(index => index.State != "online")
                        .Select(index => index.Name)
                        .Intersect(indexNames);
                    if (!pendingIndexes.Any())
                    {
                        break;
                    }

                    Logger.LogInformation($"Still waiting for indexes to complete building ({indexesToWatch})");
                    await Task.Delay(WatchIndexSleepDuration);
                }
            }
            catch (TaskCanceledException)
            {
                // application cancelled watch task
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Error trying to watch pending indexes ({indexesToWatch}) for bucket {bucketName}");
                throw;
            }
        }
    }
}
