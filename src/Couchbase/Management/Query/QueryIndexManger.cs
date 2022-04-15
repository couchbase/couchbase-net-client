using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry.Query;
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

            Validate(bucketName, options.ScopeNameValue!, options.CollectionNameValue!);

            try
            {
                var indexes = await this.GetAllIndexesAsync(bucketName,
                    queryOptions => queryOptions.CancellationToken(options.TokenValue)
                ).ConfigureAwait(false);

                var tasks = new List<Task>();
                foreach (var index in indexes.Where(i => i.State == "pending" || i.State == "deferred"))
                {
                    var statement = QueryGenerator.CreateDeferredIndexStatement(bucketName, index.Name, options);
                    tasks.Add(_queryClient.QueryAsync<dynamic>(statement,
                        queryOptions => queryOptions.CancellationToken(options.TokenValue)
                    ));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
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

            Validate(bucketName, options.ScopeNameValue!, options.CollectionNameValue!);

            if (indexName == null)
            {
                throw new ArgumentNullException(nameof(indexName));
            }
            if(fields == null)
            {
                throw new ArgumentNullException(nameof(indexName));
            }
            if(fields.Count() == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fields));
            }

            try
            {
                var statement = QueryGenerator.CreateIndexStatement(bucketName, indexName, fields, options);
                await _queryClient.QueryAsync<dynamic>(statement,
                    queryOptions => queryOptions.CancellationToken(options.TokenValue)
                ).ConfigureAwait(false);
            }
            catch (IndexExistsException e)
            {
                if (!options.IgnoreIfExistsValue)
                {
                    _logger.LogError(e, "Error trying to create primary query index on {bucketName}",
                        _redactor.MetaData(bucketName));
                    throw;
                }
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

            Validate(bucketName, options.ScopeNameValue!, options.CollectionNameValue!);

            try
            {
                var statement = QueryGenerator.CreatePrimaryIndexStatement(bucketName, options);
                await _queryClient.QueryAsync<dynamic>(statement,
                    queryOptions => queryOptions.CancellationToken(options.TokenValue)
                ).ConfigureAwait(false);
            }
            catch (IndexExistsException e)
            {
                if (!options.IgnoreIfExistsValue)
                {
                    _logger.LogError(e, "Error trying to create primary query index on {bucketName}",
                        _redactor.MetaData(bucketName));
                    throw;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error trying to create primary query index on {bucketName}",
                    _redactor.MetaData(bucketName));
                throw;
            }
        }

        public async Task DropIndexAsync(string bucketName, string indexName, DropQueryIndexOptions? options = null)
        {
            options ??= DropQueryIndexOptions.Default;
            _logger.LogInformation("Attempting to drop query index {indexName} on bucket {bucketName}",
                _redactor.MetaData(indexName), _redactor.MetaData(bucketName));

            Validate(bucketName, options.ScopeNameValue!, options.CollectionNameValue!);

            if (indexName == null)
            {
                throw new ArgumentNullException(nameof(indexName));
            }

            try
            {
                var statement = QueryGenerator.CreateDropIndexStatement(bucketName, indexName, options);
                await _queryClient.QueryAsync<dynamic>(statement,
                    queryOptions => queryOptions.CancellationToken(options.TokenValue)
                ).ConfigureAwait(false);
            }
            catch (IndexExistsException e)
            {
                if (!options.IgnoreIfExistsValue)
                {
                    _logger.LogError(e, "Error trying to create primary query index on {bucketName}",
                        _redactor.MetaData(bucketName));
                    throw;
                }
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

            Validate(bucketName, options.ScopeNameValue!, options.CollectionNameValue!);

            try
            {
                var statement = QueryGenerator.CreateDropPrimaryIndexStatement(bucketName, options);
                await _queryClient.QueryAsync<dynamic>(statement,
                    queryOptions => queryOptions.CancellationToken(options.TokenValue)
                ).ConfigureAwait(false);
            }
            catch (IndexExistsException e)
            {
                if (!options.IgnoreIfExistsValue)
                {
                    _logger.LogError(e, "Error trying to create primary query index on {bucketName}",
                        _redactor.MetaData(bucketName));
                    throw;
                }
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
                var queryOptions = new QueryOptions()
                    .Parameter("bucketName", bucketName)
                    .CancellationToken(options.TokenValue);

                if (!string.IsNullOrWhiteSpace(options.ScopeNameValue))
                {
                    queryOptions.Parameter("scopeName", options.ScopeNameValue!);
                }
                if (!string.IsNullOrWhiteSpace(options.CollectionNameValue))
                {
                    queryOptions.Parameter("collectionName", options.CollectionNameValue!);
                }

                var statement = QueryGenerator.CreateGetAllIndexesStatement(options);
                var result = await _queryClient.QueryAsync<QueryIndex>(statement, queryOptions)
                    .ConfigureAwait(false);

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

            Validate(bucketName, options.ScopeNameValue!, options.CollectionNameValue!);

            try
            {
                while (!options.TokenValue.IsCancellationRequested)
                {
                    var indexes = await this.GetAllIndexesAsync(bucketName,
                        queryOptions => queryOptions.CancellationToken(options.TokenValue)
                    ).ConfigureAwait(false);

                    var pendingIndexes = indexes.Where(index => index.State != "online")
                        .Select(index => index.Name)
                        .Intersect(indexNames);
                    if (!pendingIndexes.Any())
                    {
                        break;
                    }

                    _logger.LogInformation($"Still waiting for indexes to complete building ({indexesToWatch})",
                        _redactor.MetaData(indexesToWatch));
                    await Task.Delay(WatchIndexSleepDuration).ConfigureAwait(false);
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

        private void Validate(string bucketName, string scope, string collection)
        {
            if (string.IsNullOrEmpty(bucketName))
            {
                throw new ArgumentNullException(nameof(bucketName));
            }
            if(scope == null && collection == null)
            {
                return;
            }
            if(scope == null && collection != null)
            {
                throw new ArgumentNullException(nameof(scope));
            }
            if (scope != null && collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
