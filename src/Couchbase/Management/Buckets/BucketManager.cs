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

namespace Couchbase.Management.Buckets
{
    internal class BucketManager : IBucketManager
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<BucketManager>();
        private readonly ClusterContext _context;
        private readonly HttpClient _client;

        public BucketManager(ClusterContext context)
            : this(context, new HttpClient(new AuthenticatingHttpClientHandler(context)))
        { }

        public BucketManager(ClusterContext context, HttpClient client)
        {
            _context = context;
            _client = client;
        }

        private Uri GetUri(string bucketName = null)
        {
            var builder = new UriBuilder
            {
                Scheme = _context.ClusterOptions.EnableTls ? "https" : "http",
                Host = _context.ClusterOptions.Servers.GetRandom().Host,
                Port = _context.ClusterOptions.EnableTls ? 18091 : 8091,
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
            var settings = new BucketSettings();
            settings.Name = json.SelectToken("name").Value<string>();
            settings.MaxTtl = json.SelectToken("maxTTL").Value<int>();
            settings.NumReplicas = json.SelectToken("replicaNumber").Value<int>();
            settings.ReplicaIndexes = json.SelectToken("replicaIndex").Value<bool>();
            settings.RamQuotaMB = json.SelectToken("quota.rawRAM").Value<int>();
            settings.FlushEnabled = json.SelectToken("controllers.flush") != null;

            var bucketTypeToken = json.SelectToken("bucketType");
            if (bucketTypeToken != null &&
                EnumExtensions.TryGetFromDescription(bucketTypeToken.Value<string>(), out BucketType bucketType))
            {
                settings.BucketType = bucketType;
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
            var values = new Dictionary<string, string>();
            values.Add("name", settings.Name);
            values.Add("bucketType", settings.BucketType.GetDescription());
            values.Add("ramQuotaMB", settings.RamQuotaMB.ToString());
            values.Add("replicaIndex", settings.ReplicaIndexes ? "1" : "0");
            values.Add("replicaNumber", settings.NumReplicas.ToString());
            values.Add("flushEnabled", settings.FlushEnabled ? "1" : "0");

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

        public async Task CreateBucketAsync(BucketSettings settings, CreateBucketOptions options = null)
        {
            options = options ?? new CreateBucketOptions();
            var uri = GetUri();
            Logger.LogInformation($"Attempting to create bucket with name {settings.Name} - {uri}");

            try
            {
                // create bucket
                var content = new FormUrlEncodedContent(GetBucketSettingAsFormValues(settings));
                var result = await _client.PostAsync(uri, content, options.CancellationToken).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.BadRequest)
                {
                    var json = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (json.IndexOf("Bucket with given name already exists", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        throw new BucketAlreadyExistsException(settings.Name);
                    }
                }

                result.EnsureSuccessStatusCode();
            }
            catch (BucketAlreadyExistsException)
            {
                Logger.LogError($"Failed to create bucket with name {settings.Name} because it already exists");
                throw;
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to create bucket with name {settings.Name} - {uri}");
                throw;
            }
        }

        public async Task UpsertBucketAsync(BucketSettings settings, UpsertBucketOptions options = null)
        {
            options = options ?? new UpsertBucketOptions();
            var uri = GetUri(settings.Name);
            Logger.LogInformation($"Attempting to upsert bucket with name {settings.Name} - {uri}");

            try
            {
                // upsert bucket
                var content = new FormUrlEncodedContent(GetBucketSettingAsFormValues(settings));
                var result = await _client.PostAsync(uri, content, options.CancellationToken).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to upsert bucket with name {settings.Name} - {uri}");
                throw;
            }
        }

        public async Task DropBucketAsync(string bucketName, DropBucketOptions options = null)
        {
            options = options ?? new DropBucketOptions();
            var uri = GetUri(bucketName);
            Logger.LogInformation($"Attempting to drop bucket with name {bucketName} - {uri}");

            try
            {
                // perform drop
                var result = await _client.DeleteAsync(uri, options.CancellationToken).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new BucketNotFoundException(bucketName);
                }

                result.EnsureSuccessStatusCode();
            }
            catch (BucketNotFoundException)
            {
                Logger.LogError($"Unable to drop bucket with name {bucketName} because it does not exist");
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to drop bucket with name {bucketName}");
                throw;
            }
        }

        public async Task FlushBucketAsync(string bucketName, FlushBucketOptions options = null)
        {
            options = options ?? new FlushBucketOptions();
            // get uri and amend path to flush endpoint
            var builder = new UriBuilder(GetUri(bucketName));
            builder.Path = Path.Combine(builder.Path, "controller/doFlush");
            var uri = builder.Uri;

            Logger.LogInformation($"Attempting to flush bucket with name {bucketName} - {uri}");

            try
            {
                // try do flush
                var result = await _client.PostAsync(uri, null, options.CancellationToken).ConfigureAwait(false);
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
                Logger.LogError($"Unable to flush bucket with name {bucketName} because it does not exist");
                throw;
            }
            catch (BucketIsNotFlushableException)
            {
                Logger.LogError($"Failed to flush bucket with name {bucketName} because it is not flushable");
                throw;
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to flush bucket with name {bucketName}");
                throw;
            }
        }

        public async Task<Dictionary<string, BucketSettings>> GetAllBucketsAsync(GetAllBucketsOptions options = null)
        {
            options = options ?? new GetAllBucketsOptions();
            var uri = GetUri();
            Logger.LogInformation($"Attempting to get all buckets - {uri}");

            try
            {
                var result = await _client.GetAsync(uri, options.CancellationToken).ConfigureAwait(false);
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
                Logger.LogError(exception, $"Failed to get all buckets - {uri}");
                throw;
            }
        }

        public async Task<BucketSettings> GetBucketAsync(string bucketName, GetBucketOptions options = null)
        {
            options = options ?? new GetBucketOptions();
            var uri = GetUri(bucketName);
            Logger.LogInformation($"Attempting to get bucket with name {bucketName} - {uri}");

            try
            {
                var result = await _client.GetAsync(uri, options.CancellationToken).ConfigureAwait(false);
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
                Logger.LogError(exception, $"Failed to get bucket with name {bucketName} - {uri}");
                throw;
            }
        }
    }
}
