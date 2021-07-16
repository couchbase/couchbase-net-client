using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.KeyValue;
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
                Name = json.GetTokenValue<string>("name"),
                MaxTtl = json.GetTokenValue<int>("maxTTL"),
                RamQuotaMB = json.GetTokenValue<long>("quota.rawRAM"),
                FlushEnabled = json.SelectToken("controllers.flush") != null
            };

            var bucketTypeToken = json.SelectToken("bucketType");
            if (bucketTypeToken != null &&
                EnumExtensions.TryGetFromDescription(bucketTypeToken.Value<string>(), out BucketType bucketType))
            {
                settings.BucketType = bucketType;
            }

            if(settings.BucketType != BucketType.Memcached)
            {
                settings.NumReplicas = json.GetTokenValue<int>("replicaNumber");
            }

            if (settings.BucketType == BucketType.Couchbase)
            {
                settings.ReplicaIndexes = json.GetTokenValue<bool>("replicaIndex");
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
                settings.EvictionPolicy = evictionPolicyType;
            }

            var durabilityMinLevelToken = json.SelectToken("durabilityMinLevel");
            if (durabilityMinLevelToken != null &&
                EnumExtensions.TryGetFromDescription(durabilityMinLevelToken.Value<string>(),
                    out DurabilityLevel durabilityMinLevel))
            {
                settings.DurabilityMinimumLevel = durabilityMinLevel;
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

            if (settings.BucketType != BucketType.Memcached)
            {
                values.Add("replicaNumber", settings.NumReplicas.ToString());
            }

            if (settings.BucketType == BucketType.Couchbase)
            {
                values.Add("replicaIndex", settings.ReplicaIndexes ? "1" : "0");
            }

            if (settings.ConflictResolutionType.HasValue)
            {
                values.Add("conflictResolutionType", settings.ConflictResolutionType.GetDescription());
            }

            /*Policy-assignment depends on bucket type. For a Couchbase bucket, the policy can be valueOnly (which is the default)
                or fullEviction. For an Ephemeral bucket, the policy can be noEviction (which is the default) or nruEviction. No policy
                can be assigned to a Memcached bucket.*/

            if (settings.EvictionPolicy.HasValue)
            {
                if (settings.BucketType == BucketType.Couchbase)
                {
                    if (settings.EvictionPolicy == EvictionPolicyType.NoEviction ||
                        settings.EvictionPolicy == EvictionPolicyType.NotRecentlyUsed)
                    {
                        throw new InvalidArgumentException(
                            "For a Couchbase bucket, the eviction policy can be valueOnly (which is the default) or fullEviction.");
                    }
                }

                if (settings.BucketType == BucketType.Ephemeral)
                {
                    if (settings.EvictionPolicy == EvictionPolicyType.ValueOnly ||
                        settings.EvictionPolicy == EvictionPolicyType.FullEviction)
                    {
                        throw new InvalidArgumentException(
                            "For an Ephemeral bucket, the eviction policy can be noEviction (which is the default) or nruEviction.");
                    }
                }

                if (settings.BucketType == BucketType.Memcached)
                {
                    throw new InvalidArgumentException("No eviction policy can be assigned to a Memcached bucket.");
                }

                values.Add("evictionPolicy", settings.EvictionPolicy.GetDescription());
            }

            if (settings.MaxTtl > 0)
            {
                values.Add("maxTTL", settings.MaxTtl.ToString());
            }

            if (settings.CompressionMode.HasValue)
            {
                values.Add("compressionMode", settings.CompressionMode.GetDescription());
            }

            if (settings.DurabilityMinimumLevel != DurabilityLevel.None)
            {
                values.Add("durabilityMinLevel", settings.DurabilityMinimumLevel.GetDescription());
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
