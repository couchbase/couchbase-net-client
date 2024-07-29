using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.Analytics;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Management.Analytics.Link;
using Couchbase.Test.Common;
using Couchbase.Test.Common.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.IntegrationTests.Management
{
    [Collection(NonParallelDefinition.Name)]
    public class AnalyticsIndexManagerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public AnalyticsIndexManagerTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task Cluster_AnalyticsIndexes_Not_Null()
        {
            //arrange
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            //act
            var manager = cluster.AnalyticsIndexes;

            //assert
            Assert.NotNull(manager);
        }

        [Fact(Skip = "Can't run without external setup")]
        public async Task AnalyticsIndexes_CreatLink_Couchbase()
        {
            // this test is useful for running locally, but outside the scope of normal Combination Tests since they run against a single cluster
            // Make sure you do not commit any real user/password data if you change this locally.
            var otherClusterUserName = "changeme_username";
            var otherClusterPassword = "changeme_password";
            var otherClusterHostname = "changeme_hostname";
            var linkName = nameof(AnalyticsIndexes_CreatLink_Couchbase) + Guid.NewGuid().ToString();
            var dataverseName = "default";
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var manager = cluster.AnalyticsIndexes;
            await manager.CreateDataverseAsync(dataverseName, (new Couchbase.Management.Analytics.CreateAnalyticsDataverseOptions()).IgnoreIfExists(true));
            var link = new CouchbaseRemoteAnalyticsLink(linkName, dataverseName, otherClusterHostname)
                .WithUsername(otherClusterUserName)
                .WithPassword(otherClusterPassword);

            await manager.CreateLinkAsync(link);
            _outputHelper.WriteLine($"Create link succeeded for {link}");
        }

        [Fact(Skip = "Can't run without external setup")]
        public async Task AnalyticsIndexes_CreateLink_S3()
        {
            // this test is useful for running locally, but outside the scope of normal Combination Tests since they run against a single cluster
            // Make sure you do not commit any real user/password data if you change this locally.
            var accessKeyId = "changeme_accesskeyid";
            var secretAccessKey = "changeme_secret";
            var region = "us-east";
            var linkName = nameof(AnalyticsIndexes_CreateLink_S3) + Guid.NewGuid().ToString();
            var dataverseName = "default";
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var manager = cluster.AnalyticsIndexes;
            await manager.CreateDataverseAsync(dataverseName, (new Couchbase.Management.Analytics.CreateAnalyticsDataverseOptions()).IgnoreIfExists(true));
            var link = new S3ExternalAnalyticsLink(linkName, dataverseName, accessKeyId, secretAccessKey, region);

            await manager.CreateLinkAsync(link);
            _outputHelper.WriteLine($"Create link succeeded for {link}");

            var editedLink = link.WithServiceEndpoint("http://localhost");
            await manager.ReplaceLinkAsync(editedLink);
            _outputHelper.WriteLine("Link updated.");
        }

        [Fact(Skip = "Can't run without external setup")]
        public async Task AnalyticsIndexes_CreateLink_AzureBlob()
        {
            // this test is useful for running locally, but outside the scope of normal Combination Tests since they run against a single cluster
            // Make sure you do not commit any real user/password data if you change this locally.

            // TODO: still need to test 'azureblob' against a server version that supports it.
            // (requires DP enabled)
            var linkName = nameof(AnalyticsIndexes_CreateLink_AzureBlob) + Guid.NewGuid().ToString();
            var dataverseName = "default";
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var manager = cluster.AnalyticsIndexes;
            await manager.CreateDataverseAsync(dataverseName, (new Couchbase.Management.Analytics.CreateAnalyticsDataverseOptions()).IgnoreIfExists(true));
            var link = new AzureBlobExternalAnalyticsLink(linkName, dataverseName)
                .WithConnectionString("http://user@localhost");

            await manager.CreateLinkAsync(link);
            _outputHelper.WriteLine($"Create link succeeded for {link}");
        }

        [Fact]
        public async Task AnalyticsIndexes_CreateCouchaseLink_ExpectInvalidArgument()
        {
            var linkName = nameof(AnalyticsIndexes_CreateCouchaseLink_ExpectInvalidArgument) + Guid.NewGuid().ToString();
            var dataverseName = "default";
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var manager = cluster.AnalyticsIndexes;
            await manager.CreateDataverseAsync(dataverseName, (new Couchbase.Management.Analytics.CreateAnalyticsDataverseOptions()).IgnoreIfExists(true));
            var link = new CouchbaseRemoteAnalyticsLink(linkName, dataverseName, "127.0.0.1")
                .WithUsername("no_such_user")
                .WithPassword("no_such_password");

            var ex = await Assert.ThrowsAsync<InvalidArgumentException>(() => manager.CreateLinkAsync(link));
            _outputHelper.WriteLine(ex.ToString());
        }

        [Fact]
        public async Task AnalyticsIndexes_GetLinks()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var manager = cluster.AnalyticsIndexes;
            var links = (await manager.GetLinks()).ToList();
            _outputHelper.WriteLine($"links.Count = {links.Count}");
            foreach (var link in links)
            {
                _outputHelper.WriteLine(link.ToString());
            }
        }

        [Fact]
        public async Task AnalyticsIndexes_GetLinks_ByType()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var manager = cluster.AnalyticsIndexes;
            foreach (var linkType in new[] { "azureblob", "couchbase", "s3"})
            {
                var links = (await manager.GetLinks(new Couchbase.Management.Analytics.GetAnalyticsLinksOptions().WithLinkType(linkType))).ToList();
                _outputHelper.WriteLine($"{linkType} links.Count = {links.Count}");
                foreach (var link in links)
                {
                    _outputHelper.WriteLine(link.ToString());
                    Assert.Equal(linkType, link.LinkType);
                }
            }
        }

        [Fact]
        public async Task AnalyticsIndexes_DropAllTestLinks()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var manager = cluster.AnalyticsIndexes;

            var links = (await manager.GetLinks())
                .Where(al => al.Name.Contains("AnalyticsIndexes_Create"))
                .OrderBy(al => al.Name)
                .ToList();

            if (links.Count > 0)
            {
                foreach (var link in links)
                {
                    _outputHelper.WriteLine($"Dropping {link}");
                    await manager.DropLinkAsync(link.Name, link.Dataverse);
                    _outputHelper.WriteLine("Dropped Successfully");
                }
            }
        }
    }
}
