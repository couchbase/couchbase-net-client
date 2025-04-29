
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Authentication.X509;
using Couchbase.Diagnostics;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.Utils;
using Couchbase.KeyValue;
using Couchbase.KeyValue.RangeScan;
using Couchbase.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.IntegrationTests
{
    public class ClusterTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public ClusterTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task Test_Open_More_Than_One_Bucket()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var key = Guid.NewGuid().ToString();

            var bucket1 = await cluster.BucketAsync("travel-sample").ConfigureAwait(false);
            Assert.NotNull(bucket1);

            var bucket2 = await cluster.BucketAsync("default").ConfigureAwait(false);
            Assert.NotNull(bucket2);

            try
            {
                var result1 = await (await bucket1.DefaultCollectionAsync()).InsertAsync(key, new {Whoah = "buddy!"})
                    .ConfigureAwait(false);

                var result2 = await (await bucket2.DefaultCollectionAsync()).InsertAsync(key, new {Whoah = "buddy!"})
                    .ConfigureAwait(false);
            }
            finally
            {
                await (await bucket1.DefaultCollectionAsync()).RemoveAsync(key).ConfigureAwait(false);
                await (await bucket2.DefaultCollectionAsync()).RemoveAsync(key).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task Test_Query_With_Positional_Parameters()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            var result = await cluster.QueryAsync<dynamic>("SELECT x.* FROM `default` WHERE x.Type=$1",
                options => { options.Parameter("foo"); }).ConfigureAwait(true);

            await foreach (var row in result)
            {
            }

            result.Dispose();
        }

        [Fact]
        public async Task Test_Query2()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            var result = await cluster.QueryAsync<dynamic>("SELECT * FROM `default` WHERE type=$name;",
                options => { options.Parameter("name", "person"); }).ConfigureAwait(false);

            await foreach (var o in result)
            {
            }

            result.Dispose();
        }

        [Fact]
        public async Task Test_Views()
        {
            var cluster = _fixture.Cluster;
            var bucket = await cluster.BucketAsync("beer-sample").ConfigureAwait(false);

            var results = await bucket.ViewQueryAsync<object, object>("beer", "brewery_beers").ConfigureAwait(false);
            await foreach (var result in results)
            {
                _outputHelper.WriteLine($"id={result.Id},key={result.Key},value={result.Value}");
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "6.5.0")]
        public async Task Test_WaitUntilReadyAsync()
        {
            var cluster = _fixture.Cluster;

            // To test properly, start this test without any nodes running.
            await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(120)).ConfigureAwait(false);
        }

        [Fact]
        public async Task Test_WaitUntilReadyAsync_Bucket()
        {
            var cluster = _fixture.Cluster;
            var defaultBucket = await cluster.BucketAsync("default").ConfigureAwait(false);
            await defaultBucket.WaitUntilReadyAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        }

        [CouchbaseVersionDependentTheory(MinVersion = "6.5.0")]
        [InlineData(ServiceType.KeyValue)]
        [InlineData(ServiceType.Views)]
        [InlineData(ServiceType.KeyValue, ServiceType.Views, ServiceType.Analytics, ServiceType.Query)]
        public async Task Test_WaitUntilReadyAsync_with_options(params ServiceType[] serviceTypes)
        {
            var cluster = _fixture.Cluster;
            var options = new WaitUntilReadyOptions()
            {
                CancellationTokenValue = CancellationToken.None,
                ExplicitServiceTypes = serviceTypes
            };

            await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(10), options).ConfigureAwait(false);
        }

        [Fact]
        public async Task Test_Default_Scope_Collection_Async()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var bucket = await cluster.BucketAsync("default");

            var scope = await bucket.DefaultScopeAsync();
            var collection = await bucket.DefaultCollectionAsync();

            Assert.Equal(Scope.DefaultScopeName, scope.Name);
            Assert.Equal(CouchbaseCollection.DefaultCollectionName, collection.Name);
        }

#if NET5_0_OR_GREATER
    //   [Fact(Skip = "Requires up-to-date cloud account credentials")]
#else
        //[Fact(Skip = "X509ChainPolicy.TrustMode not supported in older versions of .NET")]
#endif
        [Fact]
        public async Task Test_Cloud_Default()
        {
            // Taken from the code given in the Capella UI for connecting with the SDK.
            // This example should work without ignoring certificate name mismatches.

            // Update this to your cluster
            var endpoint = "cb.3nakwwqp5hwkuek.nonprod-project-avengers.com";
            var bucketName = "travel-sample";

            // In the cloud dashboard, go to Clusters -> <your cluster> -> Connect -> Database Access -> Manage Credentials
            var username = "myelixirtest01";
            var password = "!Salmon2";
            // User Input ends here.

            // default without overriding any callbacks.
            {
                // Initialize the Connection
                var opts = new ClusterOptions().WithCredentials(username, password);
                opts.EnableTls = true;
                opts.ForceIpAsTargetHost = false;

                IServiceCollection serviceCollection = new ServiceCollection();
                serviceCollection.AddLogging(builder => builder
                    .AddFilter(level => level >= LogLevel.Debug)
                );

                var loggerFactory = serviceCollection.BuildServiceProvider().GetService<ILoggerFactory>();
                loggerFactory.AddFile("Logs/myapp-{Date}.txt", LogLevel.Debug);
                opts.WithLogging(loggerFactory);

                var cluster = await Couchbase.Cluster.ConnectAsync("couchbases://" + endpoint, opts);

                await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(49));
                var bucket = await cluster.BucketAsync(bucketName);
                var collection = bucket.DefaultCollection();

                var scan = collection.ScanAsync(new RangeScan(ScanTerm.Minimum, ScanTerm.Maximum));

                await foreach(var i in scan)
                {

                }
                // Store a Document
                var upsertResult = await collection.UpsertAsync("king_arthur", new
                {
                    Name = "Arthur",
                    Email = "kingarthur@couchbase.com",
                    Interests = new[] { "Holy Grail", "African Swallows" }
                });

                // Load the Document and print it
                var getResult = await collection.GetAsync("king_arthur");
                Console.WriteLine(getResult.ContentAs<dynamic>());

                // Perform a N1QL Query
                var queryResult = await cluster.QueryAsync<dynamic>(
                    String.Format("SELECT name FROM `{0}` WHERE $1 IN interests", bucketName),
                    new QueryOptions().Parameter("African Swallows")
                );
            }

            // If a callback is specified, default certificates should not be used.
          /*  {
                // Initialize the Connection
                var opts = new ClusterOptions().WithCredentials(username, password);
                opts.EnableTls = true;
                opts.KvCertificateCallbackValidation = (a, b, c, d) => false;

                var cluster = await Cluster.ConnectAsync("couchbases://" + endpoint, opts);

                // this will fail due to certificate validation failing.
                var ex = await Assert.ThrowsAsync<Couchbase.Management.Buckets.BucketNotFoundException>(async () => await cluster.BucketAsync(bucketName));

                // this will fail because the cluster bootstraps with KV validation.
                var queryResultEx = await Assert.ThrowsAsync<ServiceNotAvailableException>(() => cluster.QueryAsync<dynamic>(
                    String.Format("SELECT name FROM `{0}` WHERE $1 IN interests", bucketName),
                    new QueryOptions().Parameter("African Swallows")
                ));
            }

            // If a query callback is specified but KV is not, KV should still work.
            {
                // Initialize the Connection
                var opts = new ClusterOptions().WithCredentials(username, password);
                opts.EnableTls = true;
                opts.HttpCertificateCallbackValidation = (a, b, c, d) => false;

                var cluster = await Cluster.ConnectAsync("couchbases://" + endpoint, opts);
                var bucket = await cluster.BucketAsync(bucketName);
                var collection = bucket.DefaultCollection();

                // Store a Document
                var upsertResult = await collection.UpsertAsync("king_arthur", new
                {
                    Name = "Arthur",
                    Email = "kingarthur@couchbase.com",
                    Interests = new[] { "Holy Grail", "African Swallows" }
                });

                // Perform a N1QL Query
                // this will fail because the cluster bootstraps with KV validation.
                var queryResultEx = await Assert.ThrowsAsync<Couchbase.Core.Exceptions.RequestCanceledException>(() => cluster.QueryAsync<dynamic>(
                    String.Format("SELECT name FROM `{0}` WHERE $1 IN interests", bucketName),
                    new QueryOptions().Parameter("African Swallows")
                ));
            }*/
        }

        [Fact (Skip = "Certificate tests Need manual setup and running. Comment out the Skip() to run")]
        public async Task Test_Certificates()
        {
            // Taken from the code given in the Capella UI for connecting with the SDK.
            // This example should work without ignoring certificate name mismatches.
            // to cause it to fail, use a hostname different from that in the certificate
            // i.e. if the name in the certificate is IP:127.0.0.1, use localhost instead.
            // Log at Debug and search the log file for X509
            /// tail -f ./tests/Couchbase.IntegrationTests/bin/Debug/net8.0/Logs/myapp-20250401.txt  | grep X509

            // Update this to your cluster
            var endpoint = "127.0.0.1";
            var bucketName = "travel-sample";

            // In the cloud dashboard, go to Clusters -> <your cluster> -> Connect -> Database Access -> Manage Credentials
            var username = "clientuser";
            var password = "password";
            // User Input ends here.

            // default without overriding any callbacks.
            {
                // Initialize the Connection
                var opts = new ClusterOptions().WithCredentials(username, password);
                opts.EnableTls = true;
                opts.ForceIpAsTargetHost = false;
                opts.KvIgnoreRemoteCertificateNameMismatch = false;
                opts.HttpIgnoreRemoteCertificateMismatch = false;
                X509Certificate2[] certs = new X509Certificate2[1];
                const string capemPath = "/Users/michaelreiche/ca.pem";
                certs[0] = new X509Certificate2(capemPath);
                opts.WithX509CertificateFactory(CertificateFactory.FromCertificates(certs));

                IServiceCollection serviceCollection = new ServiceCollection();
                serviceCollection.AddLogging(builder => builder
                    .AddFilter(level => level >= LogLevel.Trace)
                );

                var loggerFactory = serviceCollection.BuildServiceProvider()
                    .GetService<ILoggerFactory>();
                loggerFactory.AddFile("Logs/myapp-{Date}.txt", LogLevel.Debug);
                opts.WithLogging(loggerFactory);

                var cluster =
                    await Couchbase.Cluster.ConnectAsync("couchbases://" + endpoint, opts);

                await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(5));
                var bucket = await cluster.BucketAsync(bucketName);
                var collection = bucket.DefaultCollection();


                // Store a Document
                var upsertResult = await collection.UpsertAsync("king_arthur", new
                {
                    Name = "Arthur",
                    Email = "kingarthur@couchbase.com",
                    Interests = new[] { "Holy Grail", "African Swallows" }
                });

                // Load the Document and print it
                var getResult = await collection.GetAsync("king_arthur");
                Console.WriteLine(getResult.ContentAs<dynamic>());
            }
        }

        [Fact]
        public async Task Test_Default_Scope_Collection_Legacy()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var bucket = await cluster.BucketAsync("default");

            var scope = bucket.DefaultScope();
            var collection = bucket.DefaultCollection();

            Assert.Equal(Scope.DefaultScopeName, scope.Name);
            Assert.Equal(CouchbaseCollection.DefaultCollectionName, collection.Name);
        }

        [Fact]
        public async Task Test_Default_Open_Collection_From_Scope_Async()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var bucket = await cluster.BucketAsync("default");

            var scope = await bucket.DefaultScopeAsync();
            var collection = await scope.CollectionAsync("_default");

            Assert.Equal(Scope.DefaultScopeName, scope.Name);
            Assert.Equal(CouchbaseCollection.DefaultCollectionName, collection.Name);
        }

        [Fact]
        public async Task Test_Default_Open_Collection_From_Scope_Legacy()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var bucket = await cluster.BucketAsync("default");

            var scope = bucket.DefaultScope();
            var collection = scope.Collection("_default");

            Assert.Equal(Scope.DefaultScopeName, scope.Name);
            Assert.Equal(CouchbaseCollection.DefaultCollectionName, collection.Name);
        }
    }
}
