#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.Analytics;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.Query;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Couchbase.Management.Analytics
{
    internal class AnalyticsIndexManager : IAnalyticsIndexManager
    {
        private readonly ILogger<AnalyticsIndexManager> _logger;
        private readonly IQueryClient _client;
        private readonly IRedactor _redactor;
        private readonly IServiceUriProvider _serviceUriProvider;
        private readonly CouchbaseHttpClient _couchbaseHttpClient;

        public AnalyticsIndexManager(ILogger<AnalyticsIndexManager> logger, IQueryClient client, IRedactor redactor,
            IServiceUriProvider serviceUriProvider, CouchbaseHttpClient couchbaseHttpClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _serviceUriProvider = serviceUriProvider ?? throw new ArgumentNullException(nameof(serviceUriProvider));
            _couchbaseHttpClient = couchbaseHttpClient ?? throw new ArgumentNullException(nameof(couchbaseHttpClient));
        }

        public async Task CreateDataverseAsync(string dataverseName, CreateAnalyticsDataverseOptions? options = null)
        {
            options ??= new CreateAnalyticsDataverseOptions();

            _logger.LogInformation("Attempting to create dataverse with name {dataverseName}",
                _redactor.MetaData(dataverseName));

            try
            {
                if (string.IsNullOrWhiteSpace(dataverseName))
                {
                    throw new InvalidArgumentException("dataverseName is a required parameter");
                }

                var ignoreStr = options.IgnoreIfExistsValue ? " IF NOT EXISTS" : string.Empty;
                var statement = $"CREATE DATAVERSE `{dataverseName}`{ignoreStr}";
                await _client
                    .QueryAsync<dynamic>(statement, queryOptions => queryOptions.CancellationToken(options.TokenValue))
                    .ConfigureAwait(false);
            }
            catch (DataverseExistsException)
            {
                _logger.LogError("Failed to create dataverse with name {dataverseName} because it already exists.",
                    _redactor.MetaData(dataverseName));
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to create dataverse with name {dataverseName}",
                    _redactor.MetaData(dataverseName));
                throw;
            }
        }

        public async Task DropDataverseAsync(string dataverseName, DropAnalyticsDataverseOptions? options = null)
        {
            options ??= new DropAnalyticsDataverseOptions();
            _logger.LogInformation("Attempting to drop dataverse with name {dataverseName}",
                _redactor.MetaData(dataverseName));
            try
            {
                if (string.IsNullOrWhiteSpace(dataverseName))
                {
                    throw new InvalidArgumentException("dataverseName is a required parameter");
                }

                var ignoreStr = options.IgnoreIfNotExistsValue ? " IF EXISTS" : string.Empty;
                var statement = $"DROP DATAVERSE `{dataverseName}`{ignoreStr}";
                await _client.QueryAsync<dynamic>(statement,
                    queryOptions => queryOptions.CancellationToken(options.TokenValue))
                             .ConfigureAwait(false);
            }
            catch (DataverseNotFoundException)
            {
                _logger.LogError("Failed to drop dataverse with name {dataverseName} because it does not exists.",
                    _redactor.MetaData(dataverseName));
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to drop dataverse with name {dataverseName}",
                    _redactor.MetaData(dataverseName));
                throw;
            }
        }

        public async Task CreateDatasetAsync(string datasetName, string bucketName, CreateAnalyticsDatasetOptions? options = null)
        {
            options ??= new CreateAnalyticsDatasetOptions();
            _logger.LogInformation("Attempting to create dataSet with name {dataverseName} on {bucketName}",
                _redactor.MetaData(datasetName), _redactor.MetaData(bucketName));
            try
            {
                if (string.IsNullOrWhiteSpace(datasetName))
                {
                    throw new InvalidArgumentException("datasetName is a required parameter");
                }

                if (string.IsNullOrWhiteSpace(bucketName))
                {
                    throw new InvalidArgumentException("bucketName is a required parameter");
                }

                var ignoreStr = options.IgnoreIfExistsValue ? "IF NOT EXISTS " : string.Empty;
                var whereStr = string.Empty;
                if (!string.IsNullOrWhiteSpace(options.ConditionValue))
                {
                    if (!options.ConditionValue.ToLowerInvariant().Trim().StartsWith("where"))
                    {
                        whereStr += " WHERE";
                    }

                    whereStr += options.ConditionValue.StartsWith(" ") ? options.ConditionValue : " " + options.ConditionValue;
                }

                datasetName = string.IsNullOrWhiteSpace(options.DataverseNameValue) ?
                    $"`{datasetName}`" :
                    $"`{options.DataverseNameValue}`.`{datasetName}`";

                var statement = $"CREATE DATASET {ignoreStr}{datasetName} ON `{bucketName}`{whereStr}";

                await _client.QueryAsync<dynamic>(statement,
                    queryOptions => queryOptions.CancellationToken(options.TokenValue))
                             .ConfigureAwait(false);
            }
            catch (DatasetExistsException)
            {
                _logger.LogError("Failed to create dataset with name {datasetName} because it already exists on {bucketName}.",
                    _redactor.MetaData(datasetName), _redactor.MetaData(bucketName));
                throw;            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to create dataset with name {datasetName} on {bucketName}",
                    _redactor.MetaData(datasetName), _redactor.MetaData(bucketName));
                throw;
            }
        }

        public async Task DropDatasetAsync(string datasetName, DropAnalyticsDatasetOptions? options = null)
        {
            options ??= new DropAnalyticsDatasetOptions();
            _logger.LogInformation("attempting to drop dataset {datasetName}", _redactor.MetaData(datasetName));
            try
            {
                if (string.IsNullOrWhiteSpace(datasetName))
                {
                    throw new InvalidArgumentException("datasetName is a required parameter");
                }

                var ignoreStr = options.IgnoreIfNotExistsValue ? " IF EXISTS" : string.Empty;
                datasetName = !string.IsNullOrWhiteSpace(options.DataverseNameValue)
                    ? $"`{options.DataverseNameValue}`.`{datasetName}`"
                    : $"`{datasetName}`";

                var statement = $"DROP DATASET {datasetName}{ignoreStr}";
                await _client.QueryAsync<dynamic>(statement,
                    queryOptions => queryOptions.CancellationToken(options.TokenValue)).ConfigureAwait(false);
            }
            catch (DatasetNotFoundException)
            {
                _logger.LogError("Failed to drop dataset with name {datasetName} as it does not exist. ",
                    _redactor.MetaData(datasetName));
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to drop dataset with name {datasetName} ",
                    _redactor.MetaData(datasetName));
                throw;
            }
        }

        public async Task<IEnumerable<AnalyticsDataset>> GetAllDatasetsAsync(GetAllAnalyticsDatasetsOptions? options = null)
        {
            options ??= new GetAllAnalyticsDatasetsOptions();
            _logger.LogInformation("Retrieving all datasets.");
            try
            {
                var statement = "SELECT d.* FROM Metadata.`Dataset` d WHERE d.DataverseName <> \"Metadata\"";
                // Not using the extension method with Action<QueryOptions> as it interferes with unit tests on
                // methods where we care about the result.
                var queryOptions = new QueryOptions
                {
                    Token = options.TokenValue
                };
                var result = await _client.QueryAsync<AnalyticsDataset>(statement, queryOptions)
                    .ConfigureAwait(false);

                var dataSets = new List<AnalyticsDataset>();
                await foreach (var row in result.Rows.ConfigureAwait(false))
                {
                    dataSets.Add(row);
                }

                return dataSets;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to retrieve data sets.");
                throw;
            }
        }

        public async Task CreateIndexAsync(string datasetName, string indexName, Dictionary<string, string> fields, CreateAnalyticsIndexOptions? options = null)
        {
            options ??= new CreateAnalyticsIndexOptions();

            try
            {
                if (string.IsNullOrWhiteSpace(datasetName))
                {
                    throw new InvalidArgumentException("datasetName is a required parameter");
                }

                if (string.IsNullOrWhiteSpace(indexName))
                {
                    throw new InvalidArgumentException("indexName is a required parameter");
                }

                if (!fields.Any())
                {
                    throw new InvalidArgumentException("At least one field is required.");
                }

                var joinedFields = $"({string.Join(", ", fields.Select(x => $"{x.Key}: {x.Value}"))})";
                _logger.LogInformation("Attempting to create index {indexName} on {datasetName} {fields}",
                    _redactor.MetaData(indexName),
                    _redactor.MetaData(datasetName),
                    _redactor.MetaData(joinedFields));
                var ignoreStr = options.IgnoreIfExistsValue ? "IF NOT EXISTS " : string.Empty;
                datasetName = string.IsNullOrWhiteSpace(options.DataverseNameValue)
                    ? $"`{datasetName}`"
                    : $"`{options.DataverseNameValue}`.`{datasetName}`";
                var statement = $"CREATE INDEX `{indexName}` {ignoreStr}ON {datasetName} {joinedFields}";
                await _client
                    .QueryAsync<dynamic>(statement, queryOptions => queryOptions.CancellationToken(options.TokenValue))
                    .ConfigureAwait(false);

            }
            catch (IndexExistsException)
            {
                _logger.LogError("Failed to create index with name {indexName} because it already exists on {datasetName}.",
                    _redactor.MetaData(indexName),
                    _redactor.MetaData(datasetName));
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to create index with name {indexName} on {datasetName}",
                    _redactor.MetaData(indexName),
                    _redactor.MetaData(datasetName));
                throw;
            }
        }

        public async Task DropIndexAsync(string datasetName, string indexName, DropAnalyticsIndexOptions? options = null)
        {
            options ??= new DropAnalyticsIndexOptions();
            _logger.LogInformation("Attempting to drop {indexName} on {datasetName}",
                _redactor.MetaData(indexName),
                _redactor.MetaData(datasetName));
            try
            {
                if (string.IsNullOrWhiteSpace(datasetName))
                {
                    throw new InvalidArgumentException("datasetName is a required parameter");
                }

                if (string.IsNullOrWhiteSpace(indexName))
                {
                    throw new InvalidArgumentException("indexName is a required parameter");
                }

                var ignoreStr = options.IgnoreIfNotExistsValue ? " IF EXISTS" : string.Empty;
                datasetName = string.IsNullOrWhiteSpace(options.DataverseNameValue)
                    ? $"`{datasetName}`"
                    : $"`{options.DataverseNameValue}`.`{datasetName}`";
                var statement = $"DROP INDEX {datasetName}.`{indexName}`{ignoreStr}";
                await _client
                    .QueryAsync<dynamic>(statement, queryOptions => queryOptions.CancellationToken(options.TokenValue))
                    .ConfigureAwait(false);
            }
            catch (IndexNotFoundException)
            {
                _logger.LogError("Failed to drop index with name {indexName} because it does not exist on {datasetName}.",
                    _redactor.MetaData(indexName),
                    _redactor.MetaData(datasetName));
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to drop index with name {indexName} on {datasetName}",
                    _redactor.MetaData(indexName),
                    _redactor.MetaData(datasetName));
                throw;
            }
        }

        public async Task<IEnumerable<AnalyticsIndex>> GetAllIndexesAsync(GetAllAnalyticsIndexesOptions? options = null)
        {
            options ??= new GetAllAnalyticsIndexesOptions();
            _logger.LogInformation("Attempting to retrieve all indexes.");

            try
            {
                var statement = "SELECT d.* FROM Metadata.`Index` d WHERE d.DataverseName <> \"Metadata\"";
                // Not using the extension method with Action<QueryOptions> as it interferes with unit tests on
                // methods where we care about the result.
                var queryOptions = new QueryOptions
                {
                    Token = options.TokenValue
                };
                var result = await _client
                    .QueryAsync<AnalyticsIndex>(statement, queryOptions).ConfigureAwait(false);

                var indexes = new List<AnalyticsIndex>();

                await foreach (var row in result.Rows.ConfigureAwait(false))
                {
                    indexes.Add(row);
                }

                return indexes;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to retrieve all indexes.");
                throw;
            }
        }

        public async Task ConnectLinkAsync(ConnectAnalyticsLinkOptions? options = null)
        {
            options ??= new ConnectAnalyticsLinkOptions();
            _logger.LogInformation("Attempting to connect link {linkName}", _redactor.MetaData(options.LinkNameValue));

            try
            {
                var statement = $"CONNECT LINK {options.LinkNameValue}";
                await _client
                    .QueryAsync<dynamic>(statement, queryOptions => queryOptions.CancellationToken(options.TokenValue))
                    .ConfigureAwait(false);
            }
            catch (LinkNotFoundException)
            {
                _logger.LogError("Could not find link {linkName}", _redactor.MetaData(options.LinkNameValue));
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to create link {linkName}", _redactor.MetaData(options.LinkNameValue));
                throw;
            }
        }

        public async Task DisconnectLinkAsync(DisconnectAnalyticsLinkOptions? options = null)
        {
            options ??= new DisconnectAnalyticsLinkOptions();
            _logger.LogInformation("Attempting to disconnect link {linkName}", _redactor.MetaData(options.LinkNameValue));

            try
            {
                var statement = $"DISCONNECT LINK {options.LinkNameValue}";
                await _client
                    .QueryAsync<dynamic>(statement, queryOptions => queryOptions.CancellationToken(options.TokenValue))
                    .ConfigureAwait(false);
            }
            catch (LinkNotFoundException)
            {
                _logger.LogError("Could not find link {linkName}", _redactor.MetaData(options.LinkNameValue));
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to disconnect link {linkName}", _redactor.MetaData(options.LinkNameValue));
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetPendingMutationsAsync(GetPendingAnalyticsMutationsOptions? options = null)
        {
            options ??= new GetPendingAnalyticsMutationsOptions();
            _logger.LogInformation("Getting pending mutations.");
            try
            {
                var builder = new UriBuilder(_serviceUriProvider.GetRandomManagementUri());
                builder.Path += "analytics/node/agg/stats/remaining";
                var uri = builder.Uri;
                var result = await _couchbaseHttpClient.GetAsync(uri, options.TokenValue).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();
                var content = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                var json = JToken.Parse(content);

                return parseResult(json);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to retrieve pending mutations.");
                throw;
            }
        }

        private Dictionary<string, int> parseResult(JToken json)
        {
            var dictionary = new Dictionary<string, int>();
            foreach (var token in json.Children())
            {
                foreach (var child in token.Children())
                {
                    foreach (var prop in child.Children<JProperty>())
                    {
                        dictionary.Add(prop.Name, int.Parse(prop.Value.ToString()));
                    }
                }
            }
            return dictionary;
        }

    }
}
