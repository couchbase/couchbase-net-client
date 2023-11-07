using Couchbase;
using Couchbase.KeyValue;
using Couchbase.Test.Common.Utils;
using System;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.CombinationTests.Tests.Cluster
{
    [Collection(CombinationTestingCollection.Name)]
    public class ClusterTests
    {
        private readonly CouchbaseFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public ClusterTests(CouchbaseFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task Test_Connect_BadPassword_ThrowsAuthenticationFailure()
        {
            // NCBC-3000: wrong password raises ServiceNotAvailable instead of AuthenticationFailure
            using var loggerFactory = new TestOutputLoggerFactory(_outputHelper);
            var clusterOptions = _fixture.GetOptionsFromConfig();
            clusterOptions.Password = "WRONG_PASSWORD_ON_PURPOSE";
            clusterOptions.WithLogging(loggerFactory);
            var t = Couchbase.NetClient.Cluster.ConnectAsync(clusterOptions);
            var ex = await Assert.ThrowsAsync<AuthenticationFailureException>(() => t);
            // ServiceNotAvailableException would be raised later, when a Query was attempted and no endpoints were bootstrapped.
        }

        [Fact]
        public async Task Test_BucketDoesNotExist_ThrowsAuthenticationFailure()
        {
            using var loggerFactory = new TestOutputLoggerFactory(_outputHelper);
            var clusterOptions = _fixture.GetOptionsFromConfig();
            clusterOptions.WithLogging(loggerFactory);
            var cluster = await NetClient.Cluster.ConnectAsync(clusterOptions);
            var t = cluster.BucketAsync("BUCKET_THAT_DOES_NOT_EXIST");
            var ex = await Assert.ThrowsAsync<AuthenticationFailureException>(() => t.AsTask());
            Assert.Contains("hibernat", ex.Message);
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_WaitUntilReady_Completes()
        {
            using var loggerFactory = new TestOutputLoggerFactory(_outputHelper);
            var clusterOptions = _fixture.GetOptionsFromConfig();
            clusterOptions.WithLogging(loggerFactory);
            var cluster = await NetClient.Cluster.ConnectAsync(clusterOptions);
            await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(30));
        }
    }
}
