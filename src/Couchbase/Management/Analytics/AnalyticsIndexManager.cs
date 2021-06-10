#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.Analytics;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.Management.Analytics.Link;
using Couchbase.Query;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Management.Analytics
{
    internal class AnalyticsIndexManager : IAnalyticsIndexManager
    {
        private readonly ILogger<AnalyticsIndexManager> _logger;
        private readonly IAnalyticsClient _client;
        private readonly IRedactor _redactor;
        private readonly IServiceUriProvider _serviceUriProvider;
        private readonly CouchbaseHttpClient _couchbaseHttpClient;

        public AnalyticsIndexManager(ILogger<AnalyticsIndexManager> logger, IAnalyticsClient client, IRedactor redactor,
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
                dataverseName = UncompoundName(dataverseName);
                var ignoreStr = options.IgnoreIfExistsValue ? " IF NOT EXISTS" : string.Empty;
                var statement = $"CREATE DATAVERSE {dataverseName}{ignoreStr}";

                await _client
                    .QueryAsync<dynamic>(statement, options.CreateAnalyticsOptions())
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
                dataverseName = UncompoundName(dataverseName);
                var ignoreStr = options.IgnoreIfNotExistsValue ? " IF EXISTS" : string.Empty;
                var statement = $"DROP DATAVERSE {dataverseName}{ignoreStr}";

                await _client.QueryAsync<dynamic>(statement, options.CreateAnalyticsOptions())
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
                datasetName = DataSetWithDataVerse(datasetName, options.DataverseNameValue);

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

                var statement = $"CREATE DATASET {ignoreStr}{datasetName} ON `{bucketName}`{whereStr}";

                await _client
                    .QueryAsync<dynamic>(statement, options.CreateAnalyticsOptions())
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
                datasetName = DataSetWithDataVerse(datasetName, options.DataverseNameValue);

                var ignoreStr = options.IgnoreIfNotExistsValue ? " IF EXISTS" : string.Empty;
                var statement = $"DROP DATASET {datasetName}{ignoreStr}";
                await _client
                    .QueryAsync<dynamic>(statement, options.CreateAnalyticsOptions())
                    .ConfigureAwait(false);
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
                var result = await _client
                    .QueryAsync<AnalyticsDataset>(statement, options.CreateAnalyticsOptions())
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
                datasetName = DataSetWithDataVerse(datasetName, options.DataverseNameValue);

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
                var statement = $"CREATE INDEX `{indexName}` {ignoreStr}ON {datasetName} {joinedFields}";

                await _client
                    .QueryAsync<dynamic>(statement, options.CreateAnalyticsOptions())
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
                datasetName = DataSetWithDataVerse(datasetName, options.DataverseNameValue);

                if (string.IsNullOrWhiteSpace(indexName))
                {
                    throw new InvalidArgumentException("indexName is a required parameter");
                }

                var ignoreStr = options.IgnoreIfNotExistsValue ? " IF EXISTS" : string.Empty;
                var statement = $"DROP INDEX {datasetName}.`{indexName}`{ignoreStr}";

                await _client
                    .QueryAsync<dynamic>(statement, options.CreateAnalyticsOptions())
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
                var result = await _client
                    .QueryAsync<AnalyticsIndex>(statement, options.CreateAnalyticsOptions())
                    .ConfigureAwait(false);

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
                    .QueryAsync<dynamic>(statement, options.CreateAnalyticsOptions())
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
                    .QueryAsync<dynamic>(statement, options.CreateAnalyticsOptions())
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
        public async Task CreateLinkAsync(AnalyticsLink link, CreateAnalyticsLinkOptions? options = null)
        {
            link.ValidateForRequest();
            options ??= new();
            try
            {
                var builder = new UriBuilder(_serviceUriProvider.GetRandomAnalyticsUri());
                builder.Path = link.ManagementPath;
                var uri = builder.Uri;
                var formContent = new FormUrlEncodedContent(link.FormData);
                var result = await _couchbaseHttpClient.PostAsync(uri, formContent, options.CancellationToken).ConfigureAwait(false);
                await HandleLinkManagementResultErrors(result, link);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to create link.");
                throw;
            }
        }

        public async Task ReplaceLinkAsync(AnalyticsLink link, ReplaceAnalyticsLinkOptions? options = null)
        {
            link.ValidateForRequest();
            options ??= new();
            try
            {
                var builder = new UriBuilder(_serviceUriProvider.GetRandomAnalyticsUri());
                builder.Path = link.ManagementPath;
                var uri = builder.Uri;
                var formContent = new FormUrlEncodedContent(link.FormData);
                var result = await _couchbaseHttpClient.PutAsync(uri, formContent, options.CancellationToken).ConfigureAwait(false);
                var responseBody = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                await HandleLinkManagementResultErrors(result, link);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to replace link.");
                throw;
            }
        }

        public async Task DropLinkAsync(string linkName, string dataverseName, DropAnalyticsLinkOptions? options = null)
        {
            _ = linkName ?? throw new ArgumentNullException(nameof(linkName));
            _ = dataverseName ?? throw new ArgumentNullException(nameof(dataverseName));
            options ??= new();
            try
            {
                var dummy = new GeneralAnalyticsLinkResponse(linkName, dataverseName);
                var builder = new UriBuilder(_serviceUriProvider.GetRandomAnalyticsUri());
                builder.Path = dummy.ManagementPath;
                var uri = builder.Uri;
                var result = await _couchbaseHttpClient.DeleteAsync(uri, options.CancellationToken).ConfigureAwait(false);
                await HandleLinkManagementResultErrors(result, linkName, dataverseName);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to delete link.");
                throw;
            }
        }

        public async Task<IEnumerable<AnalyticsLink>> GetLinks(GetAnalyticsLinksOptions? options = null)
        {
            options ??= new();
            try
            {
                var builder = new UriBuilder(_serviceUriProvider.GetRandomAnalyticsUri());
                var sb = new StringBuilder("analytics/link");
                if (!string.IsNullOrEmpty(options.DataverseName))
                {
                    sb.Append("/").Append(Uri.EscapeUriString(UncompoundName(options.DataverseName!)));
                    if (!string.IsNullOrEmpty(options.Name))
                    {
                        sb.Append("/").Append(Uri.EscapeUriString(options.Name!));
                    }
                }

                builder.Path = sb.ToString();
                if (!string.IsNullOrEmpty(options.LinkType))
                {
                    builder.Query = $"type={Uri.EscapeUriString(options.LinkType!)}";
                }

                var uri = builder.Uri;
                var result = await _couchbaseHttpClient.GetAsync(uri, options.CancellationToken).ConfigureAwait(false);
                var responseBody = await result.Content.ReadAsStringAsync();
                await HandleLinkManagementResultErrors(result, string.Empty, string.Empty);
                var jarray = JArray.Parse(responseBody);
                var typedResults = jarray.Select<JToken, AnalyticsLink>(token => token["type"].Value<string>() switch
                {
                    "s3" => token.ToObject<S3ExternalAnalyticsLinkResponse>().AsRequest(),
                    "couchbase" => token.ToObject<CouchbaseRemoteAnalyticsLinkResponse>().AsRequest(),
                    "azureblob" => token.ToObject<AzureBlobExternalAnalyticsLinkResponse>().AsRequest(),
                    _ => token.ToObject<GeneralAnalyticsLinkResponse>()
                });

                return typedResults;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to delete link.");
                throw;
            }
        }

        private Task HandleLinkManagementResultErrors(HttpResponseMessage result, AnalyticsLink link) => HandleLinkManagementResultErrors(result, link.Name, link.Dataverse);

        private async Task HandleLinkManagementResultErrors(HttpResponseMessage result, string linkName, string dataverseName)
        {
            if (!result.IsSuccessStatusCode)
            {
                var body = new StringBuilder(await result.Content.ReadAsStringAsync().ConfigureAwait(false));
                body.Replace(linkName, _redactor.MetaData(linkName)?.ToString())
                    .Replace(dataverseName, _redactor.MetaData(dataverseName)?.ToString());

                var message = body.ToString();
                switch (result.StatusCode)
                {
                    case System.Net.HttpStatusCode.BadRequest:
                        if (message.StartsWith("24006:"))
                        {
                            throw new LinkNotFoundException(message);
                        }
                        else
                        {
                            throw new InvalidArgumentException(message);
                        }
                    case System.Net.HttpStatusCode.Conflict:
                        throw new LinkExistsException(message);
                    case System.Net.HttpStatusCode.NotFound:
                        if (message.Contains("dataverse") == true)
                        {
                            throw new DataverseNotFoundException(message);
                        }
                        else goto default;
                    default:
                        throw new CouchbaseException($"Create link failed due to {result.StatusCode}");
                }
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


        private string UncompoundName(string dataverseName)
        {
            if (string.IsNullOrWhiteSpace(dataverseName))
            {
                throw new InvalidArgumentException("dataverseName is a required parameter");
            }

            if (!dataverseName.StartsWith("`"))
            {
                dataverseName = string.Concat("`", dataverseName, "`");
            }

            if (!dataverseName.Contains('/'))
            {
                return dataverseName;
            }

            var pieces = dataverseName.Split('/');
            return string.Join("`.`", pieces);
        }

        private string DataSetWithDataVerse(string datasetName, string? dataverseName)
        {
            if (string.IsNullOrWhiteSpace(datasetName))
            {
                throw new InvalidArgumentException("datasetName is a required parameter");
            }

            return string.IsNullOrWhiteSpace(dataverseName) ?
                           $"`{datasetName}`" :
                           $"{UncompoundName(dataverseName!)}.`{datasetName}`";
        }
    }
}
