using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Logging;
using Couchbase.Query;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Management.Query
{
    internal class QueryIndexManager : IQueryIndexManager
    {
        private static readonly TimeSpan WatchIndexSleepDuration = TimeSpan.FromMilliseconds(50);

        private readonly IQueryClient _queryClient;
        private readonly ILogger<QueryIndexManager> _logger;
        private readonly IRedactor _redactor;

        public QueryIndexManager(IQueryClient queryClient, ILogger<QueryIndexManager> logger, IRedactor redactor)
        {
            _queryClient = queryClient ?? throw new ArgumentNullException(nameof(queryClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        }

        public async Task BuildDeferredIndexesAsync(string bucketName, BuildDeferredQueryIndexOptions? options = null)
        {
            options ??= BuildDeferredQueryIndexOptions.Default;
            _logger.LogInformation("Attempting to build deferred query indexes on bucket {bucketName}",
                _redactor.MetaData(bucketName));
            try
            {
                var indexes = await this.GetAllIndexesAsync(bucketName,
                    queryOptions => queryOptions.CancellationToken(options.TokenValue)
                );

                var tasks = new List<Task>();
                foreach (var index in indexes.Where(i => i.State == "pending" || i.State == "deferred"))
                {
                    var statement = $"BUILD INDEX ON {bucketName}({index.Name}) USING GSI;";
                    tasks.Add(_queryClient.QueryAsync<dynamic>(statement,
                        queryOptions => queryOptions.CancellationToken(options.TokenValue)
                    ));
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error trying to build deferred query indexes on {bucketName}",
                    _redactor.MetaData(bucketName));
                throw;
            }
        }

        public async Task CreateIndexAsync(string bucketName, string indexName, IEnumerable<string> fields, CreateQueryIndexOptions? options = null)
        {
            options ??= CreateQueryIndexOptions.Default;
            _logger.LogInformation("Attempting to create query index {indexName} on bucket {bucketName}",
                _redactor.MetaData(indexName), _redactor.MetaData(bucketName));

            try
            {
                var statement = $"CREATE INDEX {indexName} ON {bucketName}({string.Join(",", fields)}) USING GSI WITH {{\"defer_build\":{options.DeferredValue}}};";
                await _queryClient.QueryAsync<dynamic>(statement,
                    queryOptions => queryOptions.CancellationToken(options.TokenValue)
                );
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error trying to create query index {indexName} on {bucketName}",
                    _redactor.MetaData(indexName), _redactor.MetaData(bucketName));
                throw;
            }
        }

        public async Task CreatePrimaryIndexAsync(string bucketName, CreatePrimaryQueryIndexOptions? options = null)
        {
            options ??= CreatePrimaryQueryIndexOptions.Default;
            _logger.LogInformation("Attempting to create primary query index on bucket {bucketName}", _redactor.MetaData(bucketName));

            try
            {
                var statement = $"CREATE PRIMARY INDEX ON {bucketName} USING GSI WITH {{\"defer_build\":{options.DeferredValue}}};";
                await _queryClient.QueryAsync<dynamic>(statement,
                    queryOptions => queryOptions.CancellationToken(options.TokenValue)
                );
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error trying to create primary query index on {bucketName}",
                    _redactor.MetaData(bucketName));
                throw;
            }
        }

        public async Task DropIndexAsync(string bucketName, string indexName, DropQueryIndexOptions? options = null)
        {
            options ??= DropQueryIndexOptions.Default;
            _logger.LogInformation("Attempting to drop query index {indexName} on bucket {bucketName}",
                _redactor.MetaData(indexName), _redactor.MetaData(bucketName));

            try
            {
                var statement = $"DROP INDEX {bucketName}.{indexName} USING GSI;";
                await _queryClient.QueryAsync<dynamic>(statement,
                    queryOptions => queryOptions.CancellationToken(options.TokenValue)
                );
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, $"Error trying to drop query index {indexName} on {bucketName}",
                    _redactor.MetaData(indexName), _redactor.MetaData(bucketName));
                throw;
            }
        }

        public async Task DropPrimaryIndexAsync(string bucketName, DropPrimaryQueryIndexOptions? options = null)
        {
            options ??= DropPrimaryQueryIndexOptions.Default;
            _logger.LogInformation("Attempting to drop primary query index on bucket {bucketName}",
                _redactor.MetaData(bucketName));

            try
            {
                var statement = $"DROP PRIMARY INDEX ON {bucketName} USING GSI;";
                await _queryClient.QueryAsync<dynamic>(statement,
                    queryOptions => queryOptions.CancellationToken(options.TokenValue)
                );
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error trying to drop query index on {bucketName}",
                    _redactor.MetaData(bucketName));
                throw;
            }
        }

        public async Task<IEnumerable<QueryIndex>> GetAllIndexesAsync(string bucketName, GetAllQueryIndexOptions? options = null)
        {
            options ??= GetAllQueryIndexOptions.Default;
            _logger.LogInformation("Attempting to get query indexes for bucket {bucketName}",
                _redactor.MetaData(bucketName));

            try
            {
                var statement = $"SELECT i.* FROM system:indexes AS i WHERE i.keyspace_id=\"{bucketName}\" AND `using`=\"gsi\";";
                var result = await _queryClient.QueryAsync<QueryIndex>(statement,
                    queryOptions => queryOptions.CancellationToken(options.TokenValue)
                );

                var indexes = new List<QueryIndex>();
                await foreach (var row in result.ConfigureAwait(false))
                {
                    indexes.Add(row);
                }

                return indexes;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error trying to get query indexes for bucket {bucketName}",
                    _redactor.MetaData(bucketName));
                throw;
            }
        }

        public async Task WatchIndexesAsync(string bucketName, IEnumerable<string> indexNames, WatchQueryIndexOptions? options = null)
        {
            options ??= WatchQueryIndexOptions.Default;
            var indexesToWatch = string.Join(", ", indexNames.ToList());
            _logger.LogInformation("Attempting to watch pending indexes ({indexesToWatch}) for bucket {bucketName}",
                _redactor.MetaData(indexesToWatch), _redactor.MetaData(bucketName));

            try
            {
                while (!options.TokenValue.IsCancellationRequested)
                {
                    var indexes = await this.GetAllIndexesAsync(bucketName,
                        queryOptions => queryOptions.CancellationToken(options.TokenValue)
                    );

                    var pendingIndexes = indexes.Where(index => index.State != "online")
                        .Select(index => index.Name)
                        .Intersect(indexNames);
                    if (!pendingIndexes.Any())
                    {
                        break;
                    }

                    _logger.LogInformation($"Still waiting for indexes to complete building ({indexesToWatch})",
                        _redactor.MetaData(indexesToWatch));
                    await Task.Delay(WatchIndexSleepDuration);
                }
            }
            catch (TaskCanceledException)
            {
                // application cancelled watch task
            }
            catch (Exception exception)
            {
                _logger.LogError(exception,
                    $"Error trying to watch pending indexes ({indexesToWatch}) for bucket {bucketName}",
                    _redactor.MetaData(indexesToWatch), _redactor.MetaData(bucketName));
                throw;
            }
        }
    }
}
