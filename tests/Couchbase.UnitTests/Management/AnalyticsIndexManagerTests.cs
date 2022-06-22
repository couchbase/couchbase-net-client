using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Management.Analytics;
using Couchbase.Management.Analytics.Link;
using Couchbase.Query;
using Couchbase.UnitTests.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.UnitTests.Helpers;
using Xunit;

namespace Couchbase.UnitTests.Management
{
    public class AnalyticsIndexManagerTests
    {
        private readonly Mock<ILogger<AnalyticsIndexManager>> _mockLogger = new Mock<ILogger<AnalyticsIndexManager>>();
        private readonly Mock<IRedactor> _mockRedactor = new Mock<IRedactor>();
        private static FakeHttpMessageHandler _fakeHttpMessageHandler = FakeHttpMessageHandler.Create((req) =>
        {
            Assert.Equal("http://localhost:8094/analytics/node/agg/stats/remaining", req.RequestUri.ToString());
            return new HttpResponseMessage
            {
                Content = new StreamContent(GenerateStreamFromString("{\"Default\":{\"GleambookMessages\":0,\"GleambookUsers\":0}}"))
            };
        });
        private readonly ICouchbaseHttpClientFactory _httpClientFactory = new MockHttpClientFactory(new HttpClient(_fakeHttpMessageHandler));
        private readonly Mock<IServiceUriProvider> _mockProvider = new Mock<IServiceUriProvider>();

        private static Stream GenerateStreamFromString(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        [Fact]
        public void When_NotConnected_AnalyticsIndexManager_Throws_NodeUnavailableException()
        {
            var clusterContext = new ClusterContext();
            var serviceUriProviderMock = new Mock<ServiceUriProvider>(clusterContext);

            var serviceUriProvider = serviceUriProviderMock.Object;
            Assert.Throws<ServiceNotAvailableException>(() => serviceUriProvider.GetRandomAnalyticsUri());
        }

        [Theory]
        [InlineData(false, "test_dataverse", "CREATE DATAVERSE `test_dataverse`")]
        [InlineData(true, "test_dataverse", "CREATE DATAVERSE `test_dataverse` IF NOT EXISTS")]
        [InlineData(false, "test_dataverse/sub1", "CREATE DATAVERSE `test_dataverse`.`sub1`")]
        [InlineData(true, "test_dataverse/sub1", "CREATE DATAVERSE `test_dataverse`.`sub1` IF NOT EXISTS")]
        public async Task CreatesCorrectCreateDataverseQuery(bool ignoreIfExists, string dataverseName, string expectedStatement)
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                var mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<dynamic>(
                    It.Is<string>(s => s.Equals(expectedStatement)), It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<object>(stream, new DefaultSerializer()));

                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                await manager.CreateDataverseAsync(dataverseName, new CreateAnalyticsDataverseOptions().IgnoreIfExists(ignoreIfExists)).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }

        [Theory]
        [InlineData(false, "test_dataverse", "DROP DATAVERSE `test_dataverse`")]
        [InlineData(true, "test_dataverse", "DROP DATAVERSE `test_dataverse` IF EXISTS")]
        [InlineData(false, "test_dataverse/sub1", "DROP DATAVERSE `test_dataverse`.`sub1`")]
        [InlineData(true, "test_dataverse/sub1", "DROP DATAVERSE `test_dataverse`.`sub1` IF EXISTS")]
        public async Task CreatesCorrectDropDataverseQuery(bool ignoreIfExists, string dataverseName, string expectedStatement)
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IAnalyticsClient> mockAnalyticClient = new Mock<IAnalyticsClient>();
                mockAnalyticClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals(expectedStatement)),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                await manager.DropDataverseAsync(dataverseName, new DropAnalyticsDataverseOptions().IgnoreIfNotExists(ignoreIfExists)).ConfigureAwait(false);
                mockAnalyticClient.VerifyAll();
            }
        }

        [Fact]
        public async Task CreatesCorrectCreateDataSetQueryWithOutIfExistsNoDataverseNoCondition()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                var statement = "CREATE DATASET `test_dataset` ON `test_bucket`";
                Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals(statement)),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                await manager.CreateDatasetAsync("test_dataset",
                    "test_bucket",
                    new CreateAnalyticsDatasetOptions().IgnoreIfExists(false)).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectCreateDataSetQueryWithIfExistsDataverseConditionNoWhere()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CREATE DATASET IF NOT EXISTS `test_dataverse`.`test_dataset` ON `test_bucket` WHERE `type` = \"beer\"")),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                await manager.CreateDatasetAsync("test_dataset",
                    "test_bucket",
                    new CreateAnalyticsDatasetOptions().IgnoreIfExists(true).DataverseName("test_dataverse").Condition("`type` = \"beer\"")).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectCreateDataSetQueryWithIfExistsDataverseConditionWithWhere()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CREATE DATASET IF NOT EXISTS `test_dataverse`.`test_dataset` ON `test_bucket` WHERE `type` = \"beer\"")),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                await manager.CreateDatasetAsync("test_dataset",
                    "test_bucket",
                    new CreateAnalyticsDatasetOptions().IgnoreIfExists(true).DataverseName("test_dataverse").Condition("WHERE `type` = \"beer\"")).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectCreateDataSetQueryWithIfExistsNoDataverseConditionWithWhere()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CREATE DATASET IF NOT EXISTS `test_dataset` ON `test_bucket` WHERE `type` = \"beer\"")),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                await manager.CreateDatasetAsync("test_dataset",
                    "test_bucket",
                    new CreateAnalyticsDatasetOptions().IgnoreIfExists(true).Condition("WHERE `type` = \"beer\"")).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectCreateDataSetQueryWithOutIfExistsNoDataverseConditionWithWhere()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CREATE DATASET `test_dataset` ON `test_bucket` WHERE `type` = \"beer\"")),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                await manager.CreateDatasetAsync("test_dataset",
                    "test_bucket",
                    new CreateAnalyticsDatasetOptions().IgnoreIfExists(false).Condition("WHERE `type` = \"beer\"")).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectDropDatasetQueryWithIfExistsNoDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("DROP DATASET `test_dataset` IF EXISTS")),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                await manager.DropDatasetAsync("test_dataset", new DropAnalyticsDatasetOptions().IgnoreIfNotExists(true)).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectDropDatasetQueryWithoutIfExistsNoDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<dynamic>(
                        It.Is<string>(s => s.Equals("DROP DATASET `test_dataset`")),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                await manager.DropDatasetAsync("test_dataset", new DropAnalyticsDatasetOptions().IgnoreIfNotExists(false)).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectDropDatasetQueryWithIfExistsWithDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<dynamic>(
                        It.Is<string>(s => s.Equals("DROP DATASET `test_dataverse`.`test_dataset` IF EXISTS")),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));

                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                await manager.DropDatasetAsync("test_dataset", new DropAnalyticsDatasetOptions().IgnoreIfNotExists(true).DataverseName("test_dataverse")).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectDropDatasetQueryWithOutIfExistswithDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("DROP DATASET `test_dataverse`.`test_dataset`")),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                await manager.DropDatasetAsync("test_dataset", new DropAnalyticsDatasetOptions().IgnoreIfNotExists(false).DataverseName("test_dataverse")).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectCreateIndexQueryWithIfNotExistsWithDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CREATE INDEX `test_index` IF NOT EXISTS ON `test_dataverse`.`test_dataset` (name: string)")),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                var fields = new Dictionary<string, string> { { "name", "string" } };
                await manager.CreateIndexAsync("test_dataset", "test_index", fields, new CreateAnalyticsIndexOptions().IgnoreIfExists(true).DataverseName("test_dataverse")).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectCreateIndexQueryWithoutIfNotExistsWithDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CREATE INDEX `test_index` ON `test_dataverse`.`test_dataset` (name: string)")),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                var fields = new Dictionary<string, string> { { "name", "string" } };
                await manager.CreateIndexAsync("test_dataset", "test_index", fields, new CreateAnalyticsIndexOptions().IgnoreIfExists(false).DataverseName("test_dataverse")).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectCreateIndexQueryWithoutIfNotExistsWithoutDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CREATE INDEX `test_index` ON `test_dataset` (name: string)")),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                var fields = new Dictionary<string, string> { { "name", "string" } };
                await manager.CreateIndexAsync("test_dataset", "test_index", fields, new CreateAnalyticsIndexOptions().IgnoreIfExists(false)).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectCreateIndexQueryWithIfNotExistsWithoutDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CREATE INDEX `test_index` IF NOT EXISTS ON `test_dataset` (name: string)")),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                var fields = new Dictionary<string, string> { { "name", "string" } };
                await manager.CreateIndexAsync("test_dataset", "test_index", fields, new CreateAnalyticsIndexOptions().IgnoreIfExists(true)).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectDropIndexQueryWithIfExistsWithDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("DROP INDEX `test_dataverse`.`test_dataset`.`test_index` IF EXISTS")),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                await manager.DropIndexAsync("test_dataset", "test_index", new DropAnalyticsIndexOptions().IgnoreIfNotExists(true).DataverseName("test_dataverse")).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectDropIndexQueryWithoutIfExistsWithoutDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("DROP INDEX `test_dataset`.`test_index`")),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                await manager.DropIndexAsync("test_dataset", "test_index", new DropAnalyticsIndexOptions().IgnoreIfNotExists(false)).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectDropIndexQueryWithoutIfExistsWithDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("DROP INDEX `test_dataverse`.`test_dataset`.`test_index`")),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                await manager.DropIndexAsync("test_dataset", "test_index", new DropAnalyticsIndexOptions().IgnoreIfNotExists(false).DataverseName("test_dataverse")).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectDropIndexQueryWithIfExistsWithoutDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("DROP INDEX `test_dataset`.`test_index` IF EXISTS")),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                await manager.DropIndexAsync("test_dataset", "test_index", new DropAnalyticsIndexOptions().IgnoreIfNotExists(true)).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }

        [Fact]
        public async Task CreatesCorrectConnectLinkQueryWithLinkName()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CONNECT LINK test_link")),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                await manager.ConnectLinkAsync(new ConnectAnalyticsLinkOptions().LinkName("test_link")).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }

        [Fact]
        public async Task CreatesCorrectConnectLinkQueryWithDefaultLinkName()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CONNECT LINK Local")),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                await manager.ConnectLinkAsync(new ConnectAnalyticsLinkOptions()).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }

        [Fact]
        public async Task CreatesCorrectDisconnectLinkQueryWithLinkName()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("DISCONNECT LINK test_link")),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                await manager.DisconnectLinkAsync(new DisconnectAnalyticsLinkOptions().LinkName("test_link")).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }

        [Fact]
        public async Task CreatesCorrectDisconnectLinkQueryWithDefaultLinkName()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
                mockAnalyticsClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("DISCONNECT LINK Local")),
                        It.IsAny<AnalyticsOptions>()))
                    .ReturnsAsync(new StreamingAnalyticsResult<dynamic>(stream, new DefaultSerializer()));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
                await manager.DisconnectLinkAsync(new DisconnectAnalyticsLinkOptions()).ConfigureAwait(false);
                mockAnalyticsClient.VerifyAll();
            }
        }

        [Fact]
        public async Task RetrievesPendingMutationsAndParsesResponseCorrectly()
        {
            Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
            _mockProvider.Setup(x => x.GetRandomManagementUri()).Returns(new Uri("http://localhost:8094"));
            var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
            var results = await manager.GetPendingMutationsAsync(new GetPendingAnalyticsMutationsOptions()).ConfigureAwait(false);
            Assert.True(results.ContainsKey("GleambookMessages"));
            Assert.Equal(0, results["GleambookMessages"]);
        }

        [Fact]
        public async Task GetsAllIndexes()
        {
            var indexes = new List<AnalyticsIndex>
            {
                new AnalyticsIndex
                {
                    DatasetName = "test_dataset",
                    DataverseName = "test_dataverse",
                    IsPrimary = false,
                    Name = "test_index"
                },
                new AnalyticsIndex
                {
                    DatasetName = "test_dataset",
                    DataverseName = "test_dataverse",
                    IsPrimary = false,
                    Name = "beer_index"
                }
            };

            var queryResult = new Mock<IAnalyticsResult<AnalyticsIndex>>();
            queryResult
                .SetupGet(m => m.MetaData)
                .Returns(new AnalyticsMetaData
                {
                    Status = AnalyticsStatus.Success
                });
            queryResult.SetupGet(m => m.RetryReason).Returns(RetryReason.NoRetry);
            queryResult.SetupGet(m => m.Rows).Returns(indexes.ToAsyncEnumerable());

            Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
            mockAnalyticsClient.Setup(x => x.QueryAsync<AnalyticsIndex>(
                    It.Is<string>(s => s.Equals("SELECT d.* FROM Metadata.`Index` d WHERE d.DataverseName <> \"Metadata\"")),
                    It.IsAny<AnalyticsOptions>()))
                .ReturnsAsync(queryResult.Object);

            var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
            var result = await manager.GetAllIndexesAsync(new GetAllAnalyticsIndexesOptions()).ConfigureAwait(false);
            Assert.Equal(2, result.Count());
            var first = result.FirstOrDefault();
            Assert.NotNull(first);
            Assert.Equal("test_index", first.Name);
            mockAnalyticsClient.VerifyAll();
        }

        [Fact]
        public async Task GetsAllDataSets()
        {
            List<AnalyticsDataset> datasets = new List<AnalyticsDataset>
            {
                new AnalyticsDataset
                {
                    Name = "test_dataset",
                    DataverseName = "test_dataverse",
                    LinkName = "Local",
                    BucketName = "test_bucket"
                },
                new AnalyticsDataset
                {
                    Name = "beer_set",
                    DataverseName = "test_dataverse",
                    LinkName = "Local",
                    BucketName = "test_bucket"
                }
            };

            var queryResult = new Mock<IAnalyticsResult<AnalyticsDataset>>();
            queryResult
                .SetupGet(m => m.MetaData)
                .Returns(new AnalyticsMetaData
                {
                    Status = AnalyticsStatus.Success
                });
            queryResult.SetupGet(m => m.RetryReason).Returns(RetryReason.NoRetry);
            queryResult.SetupGet(m => m.Rows).Returns(datasets.ToAsyncEnumerable());
            Mock<IAnalyticsClient> mockAnalyticsClient = new Mock<IAnalyticsClient>();
            mockAnalyticsClient.Setup(x => x.QueryAsync<AnalyticsDataset>(
                    It.Is<string>(s => s.Equals("SELECT d.* FROM Metadata.`Dataset` d WHERE d.DataverseName <> \"Metadata\"")),
                    It.IsAny<AnalyticsOptions>()))
                .ReturnsAsync(queryResult.Object);
            var manager = new AnalyticsIndexManager(_mockLogger.Object, mockAnalyticsClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClientFactory);
            var result = await manager.GetAllDatasetsAsync(new GetAllAnalyticsDatasetsOptions()).ConfigureAwait(false);
            Assert.Equal(2, result.Count());
            var first = result.FirstOrDefault();
            Assert.NotNull(first);
            Assert.Equal("test_dataset", first.Name);
            mockAnalyticsClient.VerifyAll();
        }

        [Theory]
        [InlineData(CouchbaseRemoteAnalyticsLink.EncryptionLevel.None)]
        [InlineData(CouchbaseRemoteAnalyticsLink.EncryptionLevel.Half)]
        public void CouchbaseRemoteAnalyticsLink_LessThanFullEncryption_RequiresUserAndPass(CouchbaseRemoteAnalyticsLink.EncryptionLevel encryptionLevel)
        {
            var linkInvalid = new CouchbaseRemoteAnalyticsLink("fooLink", "fooVerse", "localhost", new(encryptionLevel));
            Assert.Equal("couchbase", linkInvalid.LinkType);
            var isValid = linkInvalid.TryValidateForRequest(out var errors);
            Assert.False(isValid, "link with no user/pass should be invalid");
            Assert.NotEmpty(errors);
            Assert.ThrowsAny<ArgumentException>(linkInvalid.ValidateForRequest);

            var linkValid = linkInvalid with
            {
                Username = "someUser",
                Password = "correct battery horse staple"
            };

            Assert.Equal("couchbase", linkValid.LinkType);
            Assert.True(linkValid.TryValidateForRequest(out var shouldBeNoErrors));
            Assert.Empty(shouldBeNoErrors);
        }

        [Theory]
        [InlineData("user", "pass", "Cert", null, null, true)]
        [InlineData("user", "pass", null, null, null, false)]
        [InlineData(null, null, "Cert", "clientCert", "clientKey", true)]
        [InlineData(null, null, null, "clientCert", "clientKey", false)]
        [InlineData("user", "pass", "Cert", "clientCert", "clientKey", true)]
        public void CouchbaseRemoteAnalyticsLink_FullEncryption_Validate(string username, string password, string certificate, string clientCert, string clientKey, bool expectValid)
        {
            // full encryption requires Certificate to be set in all cases, and either User+Pass or ClientCert+ClientKey
            var linkInvalid = new CouchbaseRemoteAnalyticsLink("fooLink", "fooVerse", "localhost", new(CouchbaseRemoteAnalyticsLink.EncryptionLevel.Full));
            Assert.Equal("couchbase", linkInvalid.LinkType);
            var isValid = linkInvalid.TryValidateForRequest(out var errors);
            Assert.False(isValid, "link with no user/pass or clientCert/Key should be invalid");
            Assert.NotEmpty(errors);
            Assert.ThrowsAny<ArgumentException>(linkInvalid.ValidateForRequest);

            var link = CouchbaseRemoteAnalyticsLink.WithFullEncryption(
                linkInvalid.Name,
                linkInvalid.Dataverse,
                linkInvalid.Hostname,
                certificate,
                clientCert, clientKey) with
            {
                Username = username,
                Password = password
            };

            Assert.Equal("couchbase", link.LinkType);
            Assert.Equal(expectValid, link.TryValidateForRequest(out var moreErrors));
            if (expectValid)
            {
                Assert.Empty(moreErrors);
            }
            else
            {
                Assert.NotEmpty(moreErrors);
            }
        }

        // TODO: verify appropriate fields after fetching for CouchbaseRemoteAnalyticsLink

        [Theory]
        [InlineData("someName", "someDataverse/sub1", "someAccessKey", "accessKeyId", "someSession", "someRegion", "someEndpoint", true)]
        [InlineData("someName", "someDataverse/sub1", "someAccessKey", "accessKeyId", null         , "someRegion", "someEndpoint", true)]
        [InlineData("someName", "someDataverse/sub1", "someAccessKey", "accessKeyId", null,          "someRegion", null          , true)]
        [InlineData(null,       "someDataverse/sub1", "someAccessKey", "accessKeyId", "someSession", "someRegion", "someEndpoint", false)]
        [InlineData("someName", null,                 "someAccessKey", "accessKeyId", "someSession", "someRegion", "someEndpoint", false)]
        [InlineData("someName", "someDataverse/sub1", null,            "accessKeyId", "someSession", "someRegion", "someEndpoint", false)]
        [InlineData("someName", "someDataverse/sub1", "someAccessKey", "accessKeyId", "someSession", null,         "someEndpoint", false)]
        [InlineData(null, null, null, null, null, null, null, false)]
        public void S3ExternalAnalyticsLink_Validate(string name, string dataverse, string secretAccessKey, string accessKeyId, string sessionToken, string region, string serviceEndpoint, bool expectValid)
        {
            var link = new S3ExternalAnalyticsLink(name, dataverse, accessKeyId, secretAccessKey, region)
            {
                SessionToken = sessionToken,
                ServiceEndpoint = serviceEndpoint
            };

            Assert.Equal("s3", link.LinkType);

            var isValid = link.TryValidateForRequest(out var errors);
            Assert.Equal(expectValid, isValid);
            if (expectValid)
            {
                Assert.Empty(errors);
            }
            else
            {
                Assert.NotEmpty(errors);
                Assert.Throws<ArgumentException>(link.ValidateForRequest);
            }
        }

        [Theory]
        [InlineData("someName", "someDV", "someCs", "someAcctName", "someAcctKey", "someSharedAccessSig", "someBlobEP", "someEPSuffix", true)]
        [InlineData("someName", "someDV", "someCs", "someAcctName", "someAcctKey", "someSharedAccessSig", null,         null,           true)]
        [InlineData("someName", "someDV", null,      "someAcctName", "someAcctKey", null,                  null,         null,           true)]
        [InlineData("someName", "someDV", null,      "someAcctName", null,          "someSharedAccessSig",                  null,         null,           true)]
        [InlineData("someName", "someDV", "someCs", null, null, null, null, null, true)]
        [InlineData(null,       "someDV", "someCs", "someAcctName", "someAcctKey", "someSharedAccessSig", "someBlobEP", "someEPSuffix", false)]
        [InlineData("someName", null,     "someCs", "someAcctName", "someAcctKey", "someSharedAccessSig", "someBlobEP", "someEPSuffix", false)]
        [InlineData("someName", "someDV", null,     null,           "someAcctKey", "someSharedAccessSig", "someBlobEP", "someEPSuffix", false)]
        [InlineData("someName", "someDV", null,     "someAcctName", null,          null,                  "someBlobEP", "someEPSuffix", false)]
        public void AzureBlobAnalyticsLink_Validate(string name, string dataverse, string connectionString, string accountName, string accountKey, string sharedAccessSignature, string blobEndpoint, string endpointSuffix, bool expectValid)
        {
            var link = new AzureBlobExternalAnalyticsLink(name, dataverse)
            {
                ConnectionString = connectionString,
                AccountName = accountName,
                AccountKey = accountKey,
                SharedAccessSignature = sharedAccessSignature,
                BlobEndpoint = blobEndpoint,
                EndpointSuffix = endpointSuffix
            };

            Assert.Equal("azureblob", link.LinkType);

            var isValid = link.TryValidateForRequest(out var errors);
            Assert.Equal(expectValid, isValid);
            if (expectValid)
            {
                Assert.Empty(errors);
            }
            else
            {
                Assert.NotEmpty(errors);
                Assert.Throws<ArgumentException>(link.ValidateForRequest);
            }
        }
    }
}
