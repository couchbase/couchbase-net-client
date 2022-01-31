using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;
using Couchbase.KeyValue;
using Couchbase.Management.Buckets;
using Couchbase.Management.Collections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using BucketNotFoundException = Couchbase.Core.Exceptions.BucketNotFoundException;

namespace Couchbase.Transactions.Tests.IntegrationTests.Fixtures
{
    public class ClusterFixture : IAsyncLifetime
    {
        public static readonly string BucketName = "default";
        public static readonly string CustomScopeName = "IntegrationTestCustomScope";
        public static readonly string CustomCollectionName = "IntTestCol";
        internal static StringBuilder Logs = new StringBuilder();
        private readonly TestSettings _settings;
        private bool _bucketOpened;

        public static LogLevel LogLevel { get; set; } = LogLevel.Information;

        public ClusterOptions ClusterOptions { get; }

        public ICluster Cluster { get; private set; }

        public ClusterFixture()
        {
            _settings = GetSettings();
            ClusterOptions = GetClusterOptions();
        }

        public async ValueTask<ICluster> GetCluster()
        {
            if (_bucketOpened)
            {
                return Cluster;
            }

            await GetDefaultBucket().ConfigureAwait(false);
            return Cluster;
        }

        public async Task<IBucket> GetDefaultBucket()
        {
            var bucket = await Cluster.BucketAsync(BucketName).ConfigureAwait(false);

            _bucketOpened = true;

            return bucket;
        }

        internal static TestSettings GetSettings()
        {
            return new ConfigurationBuilder()
                .AddJsonFile("config.json")
                .Build()
                .GetSection("testSettings")
                .Get<TestSettings>();
        }

        internal static ClusterOptions GetClusterOptions()
        {
            var settings = GetSettings();
            var options = new ConfigurationBuilder()
                .AddJsonFile("config.json")
                .Build()
                .GetSection("couchbase")
                .Get<ClusterOptions>();

            if (settings.SystemTextJson)
            {
                options.WithSerializer(SystemTextJsonSerializer.Create());
            }

            return options;
        }

        public async Task<ICluster> OpenClusterAsync(ITestOutputHelper outputHelper)
        {
            var opts = GetClusterOptions().WithLogging(new TestOutputLoggerFactory(outputHelper));
            var cluster = await Couchbase.Cluster.ConnectAsync(
                    _settings.ConnectionString,
                    opts)
                .ConfigureAwait(false);

            return cluster;
        }

        public async Task<ICouchbaseCollection> OpenDefaultCollection(ITestOutputHelper outputHelper)
        {
            var cluster = await OpenClusterAsync(outputHelper);
            var bucket = await cluster.BucketAsync(BucketName);
            return await bucket.DefaultCollectionAsync();
        }

        public async Task<ICouchbaseCollection> OpenCustomCollection(ITestOutputHelper outputHelper)
        {
            var cluster = await OpenClusterAsync(outputHelper);
            var bucket = await cluster.BucketAsync(BucketName);
            var scope = await bucket.ScopeAsync(CustomScopeName);
            var collection = await scope.CollectionAsync(CustomCollectionName);
            return collection;
        }

        public async Task InitializeAsync()
        {
            var opts = GetClusterOptions();
            Cluster = await Couchbase.Cluster.ConnectAsync(
                    _settings.ConnectionString,
                    opts)
                .ConfigureAwait(false);

            var bucketSettings = new BucketSettings()
                {
                    BucketType = BucketType.Couchbase,
                    Name = BucketName,
                    RamQuotaMB = 100,
                    NumReplicas = 0
                };

            try
            {
                await Cluster.Buckets.CreateBucketAsync(bucketSettings).ConfigureAwait(false);
            }
            catch (BucketExistsException)
            {
            }
            catch (CouchbaseException ex) when (ex.ToString().Contains("already exists"))
            {
            }
            catch (System.Net.Http.HttpRequestException)
            {
                // why did it fail?
            }
            catch (Exception ex)
            {
                throw;
            }

            try
            {
                var bucket = await Cluster.BucketAsync(BucketName);
                try
                {
                    await bucket.Collections.CreateScopeAsync(CustomScopeName);
                    await Task.Delay(5_000);
                }
                catch (ScopeExistsException)
                {}

                try
                {
                    var collectionSpec = new CollectionSpec(scopeName: CustomScopeName, CustomCollectionName);
                    await bucket.Collections.CreateCollectionAsync(collectionSpec);
                    await Task.Delay(5_000);
                }
                catch (CollectionExistsException)
                {}
            }
            catch
            {
                throw;
            }
        }

        public async Task DisposeAsync()
        {
            if (Cluster == null)
            {
                return;
            }

            if (_settings.CleanupTestBucket)
            {
                try
                {
                    await Cluster.Buckets.DropBucketAsync(BucketName);
                }
                catch (BucketNotFoundException)
                {
                }
            }

            Cluster.Dispose();
        }

        internal class TestOutputLoggerFactory : ILoggerFactory
        {
            private readonly ITestOutputHelper _outputHelper;

            public TestOutputLoggerFactory(ITestOutputHelper outputHelper)
            {
                _outputHelper = outputHelper;
            }

            public void AddProvider(ILoggerProvider provider)
            {
            }

            public ILogger CreateLogger(string categoryName) => new TestOutputLogger(_outputHelper, categoryName);

            public void Dispose()
            {
            }
        }

        private class TestOutputLogger : ILogger
        {
            private readonly ITestOutputHelper _outputHelper;
            private readonly string _categoryName;

            public TestOutputLogger(ITestOutputHelper outputHelper, string categoryName)
            {
                _outputHelper = outputHelper;
                _categoryName = categoryName;
            }

            public IDisposable BeginScope<TState>(TState state) => new Moq.Mock<IDisposable>().Object;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                try
                {
                    _outputHelper.WriteLine($"{logLevel}: {_categoryName} [{eventId}] {formatter(state, exception)}");
                }
                catch (InvalidOperationException)
                {
                }
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
