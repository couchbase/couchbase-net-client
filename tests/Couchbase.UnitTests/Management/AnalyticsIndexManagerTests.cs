using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Management.Analytics;
using Couchbase.Query;
using Couchbase.UnitTests.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
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
        private readonly CouchbaseHttpClient _httpClient = new CouchbaseHttpClient(_fakeHttpMessageHandler);
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
        public async Task CreatesCorrectCreateDataverseQueryNoIfExists()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CREATE DATAVERSE `test_dataverse`")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                await manager.CreateDataverseAsync("test_dataverse", new CreateAnalyticsDataverseOptions()).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectCreateDataverseQueryWithIfExists()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CREATE DATAVERSE `test_dataverse` IF NOT EXISTS")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                await manager.CreateDataverseAsync("test_dataverse", new CreateAnalyticsDataverseOptions().IgnoreIfExists(true)).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }

        [Fact]
        public async Task CreatesCorrectDropDataverseQueryWithIfExists()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("DROP DATAVERSE `test_dataverse` IF EXISTS")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                await manager.DropDataverseAsync("test_dataverse", new DropAnalyticsDataverseOptions().IgnoreIfNotExists(true)).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectDropDataverseQueryWithOutIfExists()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("DROP DATAVERSE `test_dataverse`")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                await manager.DropDataverseAsync("test_dataverse", new DropAnalyticsDataverseOptions().IgnoreIfNotExists(false)).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectCreateDataSetQueryWithOutIfExistsNoDataverseNoCondition()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CREATE DATASET `test_dataset` ON `test_bucket`")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                await manager.CreateDatasetAsync("test_dataset",
                    "test_bucket",
                    new CreateAnalyticsDatasetOptions().IgnoreIfExists(false)).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectCreateDataSetQueryWithIfExistsDataverseConditionNoWhere()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CREATE DATASET IF NOT EXISTS `test_dataverse`.`test_dataset` ON `test_bucket` WHERE `type` = \"beer\"")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                await manager.CreateDatasetAsync("test_dataset",
                    "test_bucket",
                    new CreateAnalyticsDatasetOptions().IgnoreIfExists(true).DataverseName("test_dataverse").Condition("`type` = \"beer\"")).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectCreateDataSetQueryWithIfExistsDataverseConditionWithWhere()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CREATE DATASET IF NOT EXISTS `test_dataverse`.`test_dataset` ON `test_bucket` WHERE `type` = \"beer\"")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                await manager.CreateDatasetAsync("test_dataset",
                    "test_bucket",
                    new CreateAnalyticsDatasetOptions().IgnoreIfExists(true).DataverseName("test_dataverse").Condition("WHERE `type` = \"beer\"")).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectCreateDataSetQueryWithIfExistsNoDataverseConditionWithWhere()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CREATE DATASET IF NOT EXISTS `test_dataset` ON `test_bucket` WHERE `type` = \"beer\"")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                await manager.CreateDatasetAsync("test_dataset",
                    "test_bucket",
                    new CreateAnalyticsDatasetOptions().IgnoreIfExists(true).Condition("WHERE `type` = \"beer\"")).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectCreateDataSetQueryWithOutIfExistsNoDataverseConditionWithWhere()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CREATE DATASET `test_dataset` ON `test_bucket` WHERE `type` = \"beer\"")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                await manager.CreateDatasetAsync("test_dataset",
                    "test_bucket",
                    new CreateAnalyticsDatasetOptions().IgnoreIfExists(false).Condition("WHERE `type` = \"beer\"")).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectDropDatasetQueryWithIfExistsNoDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("DROP DATASET `test_dataset` IF EXISTS")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                await manager.DropDatasetAsync("test_dataset", new DropAnalyticsDatasetOptions().IgnoreIfNotExists(true)).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectDropDatasetQueryWithoutIfExistsNoDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("DROP DATASET `test_dataset`")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                await manager.DropDatasetAsync("test_dataset", new DropAnalyticsDatasetOptions().IgnoreIfNotExists(false)).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectDropDatasetQueryWithIfExistsWithDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("DROP DATASET `test_dataverse`.`test_dataset` IF EXISTS")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                await manager.DropDatasetAsync("test_dataset", new DropAnalyticsDatasetOptions().IgnoreIfNotExists(true).DataverseName("test_dataverse")).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectDropDatasetQueryWithOutIfExistswithDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("DROP DATASET `test_dataverse`.`test_dataset`")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                await manager.DropDatasetAsync("test_dataset", new DropAnalyticsDatasetOptions().IgnoreIfNotExists(false).DataverseName("test_dataverse")).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectCreateIndexQueryWithIfNotExistsWithDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CREATE INDEX `test_index` IF NOT EXISTS ON `test_dataverse`.`test_dataset` (name: string)")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                var fields = new Dictionary<string, string> {{"name", "string"}};
                await manager.CreateIndexAsync("test_dataset", "test_index", fields, new CreateAnalyticsIndexOptions().IgnoreIfExists(true).DataverseName("test_dataverse")).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectCreateIndexQueryWithoutIfNotExistsWithDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CREATE INDEX `test_index` ON `test_dataverse`.`test_dataset` (name: string)")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                var fields = new Dictionary<string, string> {{"name", "string"}};
                await manager.CreateIndexAsync("test_dataset", "test_index", fields, new CreateAnalyticsIndexOptions().IgnoreIfExists(false).DataverseName("test_dataverse")).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectCreateIndexQueryWithoutIfNotExistsWithoutDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CREATE INDEX `test_index` ON `test_dataset` (name: string)")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                var fields = new Dictionary<string, string> {{"name", "string"}};
                await manager.CreateIndexAsync("test_dataset", "test_index", fields, new CreateAnalyticsIndexOptions().IgnoreIfExists(false)).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectCreateIndexQueryWithIfNotExistsWithoutDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CREATE INDEX `test_index` IF NOT EXISTS ON `test_dataset` (name: string)")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                var fields = new Dictionary<string, string> {{"name", "string"}};
                await manager.CreateIndexAsync("test_dataset", "test_index", fields, new CreateAnalyticsIndexOptions().IgnoreIfExists(true)).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectDropIndexQueryWithIfExistsWithDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("DROP INDEX `test_dataverse`.`test_dataset`.`test_index` IF EXISTS")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                await manager.DropIndexAsync("test_dataset", "test_index", new DropAnalyticsIndexOptions().IgnoreIfNotExists(true).DataverseName("test_dataverse")).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectDropIndexQueryWithoutIfExistsWithoutDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("DROP INDEX `test_dataset`.`test_index`")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                await manager.DropIndexAsync("test_dataset", "test_index", new DropAnalyticsIndexOptions().IgnoreIfNotExists(false)).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectDropIndexQueryWithoutIfExistsWithDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("DROP INDEX `test_dataverse`.`test_dataset`.`test_index`")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                await manager.DropIndexAsync("test_dataset", "test_index", new DropAnalyticsIndexOptions().IgnoreIfNotExists(false).DataverseName("test_dataverse")).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }
        [Fact]
        public async Task CreatesCorrectDropIndexQueryWithIfExistsWithoutDataverse()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("DROP INDEX `test_dataset`.`test_index` IF EXISTS")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                await manager.DropIndexAsync("test_dataset", "test_index", new DropAnalyticsIndexOptions().IgnoreIfNotExists(true)).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }

        [Fact]
        public async Task CreatesCorrectConnectLinkQueryWithLinkName()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CONNECT LINK test_link")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                await manager.ConnectLinkAsync(new ConnectAnalyticsLinkOptions().LinkName("test_link")).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }

        [Fact]
        public async Task CreatesCorrectConnectLinkQueryWithDefaultLinkName()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("CONNECT LINK Local")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                await manager.ConnectLinkAsync(new ConnectAnalyticsLinkOptions()).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }

        [Fact]
        public async Task CreatesCorrectDisconnectLinkQueryWithLinkName()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("DISCONNECT LINK test_link")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                await manager.DisconnectLinkAsync(new DisconnectAnalyticsLinkOptions().LinkName("test_link")).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }

        [Fact]
        public async Task CreatesCorrectDisconnectLinkQueryWithDefaultLinkName()
        {
            using (var stream = GenerateStreamFromString("Here is a stream."))
            {
                Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
                _mockQueryClient.Setup(x => x.QueryAsync<object>(
                        It.Is<string>(s => s.Equals("DISCONNECT LINK Local")),
                        It.IsAny<QueryOptions>()))
                    .ReturnsAsync(new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory));
                var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
                await manager.DisconnectLinkAsync(new DisconnectAnalyticsLinkOptions()).ConfigureAwait(false);
                _mockQueryClient.VerifyAll();
            }
        }

        [Fact]
        public async Task RetrievesPendingMutationsAndParsesResponseCorrectly()
        {
            Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
            _mockProvider.Setup(x => x.GetRandomManagementUri()).Returns(new Uri("http://localhost:8094"));
            var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
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

            var queryResult = new Mock<IQueryResult<AnalyticsIndex>>();

            queryResult
                .SetupGet(m => m.MetaData)
                .Returns(new QueryMetaData
            {
                Status = QueryStatus.Success
            });
            queryResult.SetupGet(m => m.RetryReason).Returns(RetryReason.NoRetry);
            queryResult.SetupGet(m => m.Rows).Returns(indexes.ToAsyncEnumerable());
            Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
            _mockQueryClient.Setup(x => x.QueryAsync<AnalyticsIndex>(
                    It.Is<string>(s => s.Equals("SELECT d.* FROM Metadata.`Index` d WHERE d.DataverseName <> \"Metadata\"")),
                    It.IsAny<QueryOptions>()))
                .ReturnsAsync(queryResult.Object);
            var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
            var result = await manager.GetAllIndexesAsync(new GetAllAnalyticsIndexesOptions()).ConfigureAwait(false);
            Assert.Equal(2, result.Count());
            var first = result.FirstOrDefault();
            Assert.NotNull(first);
            Assert.Equal("test_index", first.Name);
            _mockQueryClient.VerifyAll();
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

            var queryResult = new Mock<IQueryResult<AnalyticsDataset>>();
            queryResult
                .SetupGet(m => m.MetaData)
                .Returns(new QueryMetaData
                {
                    Status = QueryStatus.Success
                });
            queryResult.SetupGet(m => m.RetryReason).Returns(RetryReason.NoRetry);
            queryResult.SetupGet(m => m.Rows).Returns(datasets.ToAsyncEnumerable());
            Mock<IQueryClient> _mockQueryClient = new Mock<IQueryClient>();
            _mockQueryClient.Setup(x => x.QueryAsync<AnalyticsDataset>(
                    It.Is<string>(s => s.Equals("SELECT d.* FROM Metadata.`Dataset` d WHERE d.DataverseName <> \"Metadata\"")),
                    It.IsAny<QueryOptions>()))
                .ReturnsAsync(queryResult.Object);
            var manager = new AnalyticsIndexManager(_mockLogger.Object, _mockQueryClient.Object, _mockRedactor.Object, _mockProvider.Object, _httpClient);
            var result = await manager.GetAllDatasetsAsync(new GetAllAnalyticsDatasetsOptions()).ConfigureAwait(false);
            Assert.Equal(2, result.Count());
            var first = result.FirstOrDefault();
            Assert.NotNull(first);
            Assert.Equal("test_dataset", first.Name);
            _mockQueryClient.VerifyAll();
        }

        private QueryErrorContext ErrorContextFactory<T>(QueryResultBase<T> failedQueryResult,
            HttpStatusCode statusCode) =>
            new QueryErrorContext
            {
                ClientContextId = Guid.Empty.ToString(),
                Parameters = "{}",
                Statement = "",
                Message = "Error Message",
                Errors = failedQueryResult.Errors,
                HttpStatus = statusCode,
                QueryStatus = failedQueryResult.MetaData?.Status ?? QueryStatus.Fatal
            };
    }
}
