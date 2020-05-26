using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

#nullable enable

namespace Couchbase.Management.Buckets
{
    internal class BucketManager : IBucketManager
    {
        private readonly IServiceUriProvider _serviceUriProvider;
        private readonly CouchbaseHttpClient _client;
        private readonly ILogger<BucketManager> _logger;
        private readonly IRedactor _redactor;

        public BucketManager(IServiceUriProvider serviceUriProvider, CouchbaseHttpClient client, ILogger<BucketManager> logger, IRedactor redactor)
        {
            _serviceUriProvider = serviceUriProvider ?? throw new ArgumentNullException(nameof(serviceUriProvider));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        }

        private Uri GetUri(string? bucketName = null)
        {
            var builder = new UriBuilder(_serviceUriProvider.GetRandomManagementUri())
            {
                Path = "pools/default/buckets"
            };

            if (!string.IsNullOrWhiteSpace(bucketName))
            {
                builder.Path += $"/{bucketName}";
            }

            return builder.Uri;
        }

        private BucketSettings GetBucketSettings(JToken json)
        {
            var settings = new BucketSettings
            {
                Name = json.SelectToken("name").Value<string>(),
                MaxTtl = json.SelectToken("maxTTL").Value<int>(),
                RamQuotaMB = json.SelectToken("quota.rawRAM").Value<int>(),
                FlushEnabled = json.SelectToken("controllers.flush") != null
            };

            var bucketTypeToken = json.SelectToken("bucketType");
            if (bucketTypeToken != null &&
                EnumExtensions.TryGetFromDescription(bucketTypeToken.Value<string>(), out BucketType bucketType))
            {
                settings.BucketType = bucketType;
            }

            if(settings.BucketType == BucketType.Couchbase)
            {
                settings.NumReplicas = json.SelectToken("replicaNumber").Value<int>();
                settings.ReplicaIndexes = json.SelectToken("replicaIndex").Value<bool>();
            }

            var conflictResolutionToken = json.SelectToken("conflictResolutionType");
            if (conflictResolutionToken != null &&
                EnumExtensions.TryGetFromDescription(conflictResolutionToken.Value<string>(), out ConflictResolutionType conflictResolutionType))
            {
                settings.ConflictResolutionType = conflictResolutionType;
            }

            var compressionModeToken = json.SelectToken("compressionMode");
            if (compressionModeToken != null &&
                EnumExtensions.TryGetFromDescription(compressionModeToken.Value<string>(), out CompressionMode compressionMode))
            {
                settings.CompressionMode = compressionMode;
            }

            var evictionPolicyToken = json.SelectToken("evictionPolicy");
            if (evictionPolicyToken != null &&
                EnumExtensions.TryGetFromDescription(evictionPolicyToken.Value<string>(), out EvictionPolicyType evictionPolicyType))
            {
                settings.EjectionMethod = evictionPolicyType;
            }

            return settings;
        }

        private IEnumerable<KeyValuePair<string, string>> GetBucketSettingAsFormValues(BucketSettings settings)
        {
            var values = new Dictionary<string, string>
            {
                {"name", settings.Name},
                {"bucketType", settings.BucketType.GetDescription()},
                {"ramQuotaMB", settings.RamQuotaMB.ToString()},
                {"flushEnabled", settings.FlushEnabled ? "1" : "0"}
            };

            if (settings.BucketType == BucketType.Couchbase)
            {
                values.Add("replicaNumber", settings.NumReplicas.ToString());
                values.Add("replicaIndex", settings.ReplicaIndexes ? "1" : "0");
            }

            if (settings.ConflictResolutionType.HasValue)
            {
                values.Add("conflictResolutionType", settings.ConflictResolutionType.GetDescription());
            }

            if (settings.EjectionMethod.HasValue)
            {
                values.Add("evictionPolicy", settings.EjectionMethod.GetDescription());
            }

            if (settings.MaxTtl > 0)
            {
                values.Add("maxTTL", settings.MaxTtl.ToString());
            }

            if (settings.CompressionMode.HasValue)
            {
                values.Add("compressionMode", settings.CompressionMode.GetDescription());
            }

            return values;
        }

        public async Task CreateBucketAsync(BucketSettings settings, CreateBucketOptions? options = null)
        {
            options ??= new CreateBucketOptions();
            var uri = GetUri();

            _logger.LogInformation("Attempting to create bucket with name {settings.Name} - {uri}",
                _redactor.MetaData(settings.Name), _redactor.SystemData(uri));

            try
            {
                // create bucket
                var content = new FormUrlEncodedContent(GetBucketSettingAsFormValues(settings));
                var result = await _client.PostAsync(uri, content, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.BadRequest)
                {
                    var json = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (json.IndexOf("Bucket with given name already exists", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        throw new BucketExistsException(settings.Name);
                    }
                }

                result.EnsureSuccessStatusCode();
            }
            catch (BucketExistsException)
            {
                _logger.LogError("Failed to create bucket with name {settings.Name} because it already exists",
                    _redactor.MetaData(settings.Name));
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to create bucket with name {settings.Name} - {uri}",
                    _redactor.MetaData(settings.Name), _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task UpdateBucketAsync(BucketSettings settings, UpdateBucketOptions? options = null)
        {
            options ??= new UpdateBucketOptions();
            var uri = GetUri(settings.Name);
            _logger.LogInformation("Attempting to upsert bucket with name {settings.Name} - {uri}",
                _redactor.MetaData(settings.Name), _redactor.SystemData(uri));

            try
            {
                // upsert bucket
                var content = new FormUrlEncodedContent(GetBucketSettingAsFormValues(settings));
                var result = await _client.PostAsync(uri, content, options.TokenValue).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to upsert bucket with name {settings.Name} - {uri}",
                    _redactor.MetaData(settings.Name), _redactor.SystemData(uri));

                throw;
            }
        }

        public async Task DropBucketAsync(string bucketName, DropBucketOptions? options = null)
        {
            options ??= new DropBucketOptions();
            var uri = GetUri(bucketName);
            _logger.LogInformation("Attempting to drop bucket with name {bucketName} - {uri}",
                    _redactor.MetaData(bucketName), _redactor.SystemData(uri));

            try
            {
                // perform drop
                var result = await _client.DeleteAsync(uri, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new BucketNotFoundException(bucketName);
                }

                result.EnsureSuccessStatusCode();
            }
            catch (BucketNotFoundException)
            {
                _logger.LogError("Unable to drop bucket with name {bucketName} because it does not exist",
                    _redactor.MetaData(bucketName));
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to drop bucket with name {bucketName}",
                    _redactor.MetaData(bucketName));
                throw;
            }
        }

        public async Task FlushBucketAsync(string bucketName, FlushBucketOptions? options = null)
        {
            options ??= new FlushBucketOptions();
            // get uri and amend path to flush endpoint
            var builder = new UriBuilder(GetUri(bucketName));
            builder.Path = Path.Combine(builder.Path, "controller/doFlush");
            var uri = builder.Uri;

            _logger.LogInformation($"Attempting to flush bucket with name {bucketName} - {uri}",
                _redactor.MetaData(bucketName), _redactor.SystemData(uri));

            try
            {
                // try do flush
                var result = await _client.PostAsync(uri, null, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new BucketNotFoundException(bucketName);
                }

                if (result.StatusCode == HttpStatusCode.BadRequest)
                {
                    var json = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (json.IndexOf("Flush is disabled for the bucket", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        throw new BucketIsNotFlushableException(bucketName);
                    }
                }

                result.EnsureSuccessStatusCode();
            }
            catch (BucketNotFoundException)
            {
                _logger.LogError("Unable to flush bucket with name {bucketName} because it does not exist",
                    _redactor.MetaData(bucketName));
                throw;
            }
            catch (BucketIsNotFlushableException)
            {
                _logger.LogError("Failed to flush bucket with name {bucketName} because it is not flushable",
                    _redactor.MetaData(bucketName));
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to flush bucket with name {bucketName}",
                    _redactor.MetaData(bucketName));
                throw;
            }
        }

        public async Task<Dictionary<string, BucketSettings>> GetAllBucketsAsync(GetAllBucketsOptions? options = null)
        {
            options ??= new GetAllBucketsOptions();
            var uri = GetUri();
            _logger.LogInformation("Attempting to get all buckets - {uri}", _redactor.SystemData(uri));

            try
            {
                var result = await _client.GetAsync(uri, options.TokenValue).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();

                var content = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                var buckets = new Dictionary<string, BucketSettings>();
                var json = JArray.Parse(content);

                foreach (var row in json)
                {
                    var settings = GetBucketSettings(row);
                    buckets.Add(settings.Name, settings);
                }

                return buckets;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to get all buckets - {uri}", _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task<BucketSettings> GetBucketAsync(string bucketName, GetBucketOptions? options = null)
        {
            options ??= new GetBucketOptions();
            var uri = GetUri(bucketName);
            _logger.LogInformation("Attempting to get bucket with name {bucketName} - {uri}",
                _redactor.MetaData(bucketName), _redactor.SystemData(uri));

            try
            {
                var result = await _client.GetAsync(uri, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new BucketNotFoundException(bucketName);
                }

                result.EnsureSuccessStatusCode();

                var content = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                var json = JObject.Parse(content);
                return GetBucketSettings(json);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, $"Failed to get bucket with name {bucketName} - {uri}",
                    _redactor.MetaData(bucketName), _redactor.SystemData(uri));
                throw;
            }
        }
    }
}
