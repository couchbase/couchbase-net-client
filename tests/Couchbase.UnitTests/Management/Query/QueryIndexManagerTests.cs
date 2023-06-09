using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Management.Query;
using Couchbase.Query;
using Couchbase.UnitTests.Helpers;
using Couchbase.UnitTests.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

#pragma warning disable CS8632

namespace Couchbase.UnitTests.Management.Query
{
    public class QueryIndexManagerTests
    {

        [Fact]
        public void When_NotConnected_QueryIndexManager_Throws_NodeUnavailableException()
        {
            var clusterContext = new ClusterContext();
            var serviceUriProviderMock = new Mock<ServiceUriProvider>(clusterContext);

            var serviceUriProvider = serviceUriProviderMock.Object;
            Assert.Throws<ServiceNotAvailableException>(() => serviceUriProvider.GetRandomQueryUri());
        }

        [Fact]
        public async Task Test_GetAllIndexesAsync()
        {
            using var response = ResourceHelper.ReadResourceAsStream(@"Documents\Query\Management\query-index-partition-response.json");

            var buffer = new byte[response.Length];
            response.Read(buffer, 0, buffer.Length);

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected().Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()).ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(buffer)
            });

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:8091")
            };
            var httpClientFactory = new MockHttpClientFactory(httpClient);

            var mockServiceUriProvider = new Mock<IServiceUriProvider>();
            mockServiceUriProvider
                .Setup(m => m.GetRandomQueryUri())
                .Returns(new Uri("http://localhost:8093"));

            var serializer = new DefaultSerializer();
            var client = new QueryClient(httpClientFactory, mockServiceUriProvider.Object, serializer,
                new Mock<ILogger<QueryClient>>().Object, NoopRequestTracer.Instance);

            var manager = new QueryIndexManager(client, new Mock<ILogger<QueryIndexManager>>().Object,
                new Redactor(new TypedRedactor(RedactionLevel.None)));

            var result =  await manager.GetAllIndexesAsync("default");

            var queryIndices = result as QueryIndex[] ?? result.ToArray();
            var rowWithPartition = queryIndices.FirstOrDefault(x => x.Partition == "HASH(`_type`)");
            Assert.NotNull(rowWithPartition);

            var rowWithCondition = queryIndices.FirstOrDefault(x => x.Condition == "(`_type` = \"User\")");
            Assert.NotNull(rowWithCondition);

            var rowWithIndexKey = queryIndices.FirstOrDefault(x=>x.IndexKey.Contains("`airportname`"));
            Assert.Equal("`airportname`", rowWithIndexKey.IndexKey.First());
        }

        [Fact]
        public async Task CreateIndexAsync_IgnoreIfExists_False_Do_Not_Throw_IndexExistsException()
        {
            var manager = CreateManager();
            await manager.CreateIndexAsync("default", "index1", new List<string> {"field"}, CreateQueryIndexOptions.Default.IgnoreIfExists(true));
        }

        [Fact]
        public async Task CreateIndexAsync_IgnoreIfExists_True_Throw_IndexExistsException()
        {
            var manager = CreateManager();
            await Assert.ThrowsAsync<IndexExistsException>(async () => await manager.CreateIndexAsync("default", "index1", new List<string> { "field" }, CreateQueryIndexOptions.Default.IgnoreIfExists(false)));
        }

        [Fact]
        public async Task CreateIndexAsync_indexName_Null_Throw_ArgumentNullException()
        {
            var manager = CreateManager();
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await manager.CreateIndexAsync("default", null, new List<string>(), CreateQueryIndexOptions.Default.IgnoreIfExists(false)));
        }

        [Fact]
        public async Task CreateIndexAsync_bucketName_Null_Throw_ArgumentNullException()
        {
            var manager = CreateManager();
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await manager.CreateIndexAsync(null, "index1", new List<string>(), CreateQueryIndexOptions.Default.IgnoreIfExists(false)));
        }

        [Fact]
        public async Task CreatePrimaryIndexAsync_IgnoreIfExists_False_Do_Not_Throw_IndexExistsException()
        {
            var manager = CreateManager();
            await manager.CreatePrimaryIndexAsync("default", CreatePrimaryQueryIndexOptions.Default.IgnoreIfExists(true));
        }

        [Fact]
        public async Task CreatePrimaryIndexAsync_BucketName_Null_Throw_ArgumentNullException()
        {
            var manager = CreateManager();
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await manager.CreatePrimaryIndexAsync(null, CreatePrimaryQueryIndexOptions.Default.IgnoreIfExists(true)));
        }

        [Fact]
        public async Task CreatePrimaryIndexAsync_IgnoreIfExists_True_Throw_IndexExistsException()
        {
            var manager = CreateManager();
            await Assert.ThrowsAsync<IndexExistsException>(async ()=>await manager.CreatePrimaryIndexAsync("default", CreatePrimaryQueryIndexOptions.Default.IgnoreIfExists(false)));
        }

        [Fact]
        public async Task CreatePrimaryIndexAsync_CollectionName_Null_Throw_ArgumentNullException()
        {
            var manager = CreateManager();
            await Assert.ThrowsAsync<ArgumentNullException>(
                async () =>
                {
                    var options = CreatePrimaryQueryIndexOptions.Default;
                    options.CollectionNameValue = "collectionName";
                    await manager.CreatePrimaryIndexAsync("default", options);
                });
        }

        [Fact]
        public async Task CreatePrimaryIndexAsync_ScopeName_Null_Throw_ArgumentNullException()
        {
            var manager = CreateManager();
            await Assert.ThrowsAsync<ArgumentNullException>(
                async () =>
                {
                    var options = CreateQueryIndexOptions.Default;
                    options.CollectionNameValue = "collectionName";
                    await manager.CreateIndexAsync("default", "index1", new[] { "field1" }, options);
                });
        }

        [Fact]
        public async Task CreateIndexAsync_CollectionName_Null_Throw_ArgumentNullException()
        {
            var manager = CreateManager();
            await Assert.ThrowsAsync<ArgumentNullException>(
                async () =>
                {
                    var options = CreateQueryIndexOptions.Default;
                    options.ScopeNameValue = "scopeName";
                    await manager.CreateIndexAsync("default", "index1", new[] { "field1" }, options);
                });
        }

        [Fact]
        public async Task CreateIndexAsync_ScopeName_Null_Throw_ArgumentNullException()
        {
            var manager = CreateManager();
            await Assert.ThrowsAsync<ArgumentNullException>(
                async () =>
                {
                    var options = CreatePrimaryQueryIndexOptions.Default;
                    options.CollectionNameValue = "collectionName";
                    await manager.CreatePrimaryIndexAsync("default", options);
                });
        }

        [Fact]
        public async Task DropPrimaryIndexAsync_IgnoreIfExists_False_Do_Not_Throw_IndexExistsException()
        {
            var manager = CreateManager();
            await manager.DropPrimaryIndexAsync("default", DropPrimaryQueryIndexOptions.Default.IgnoreIfExists(true));
        }

        [Fact]
        public async Task DropPrimaryIndexAsync_IgnoreIfExists_True_Throw_IndexExistsException()
        {
            var manager = CreateManager();
            await Assert.ThrowsAsync<IndexExistsException>(async () => await manager.DropPrimaryIndexAsync("default", DropPrimaryQueryIndexOptions.Default.IgnoreIfExists(false)));
        }

        [Fact]
        public async Task DropIndexAsync_IgnoreIfExists_False_Do_Not_Throw_IndexExistsException()
        {
            var manager = CreateManager();
            await manager.DropIndexAsync("default", "index1", DropQueryIndexOptions.Default.IgnoreIfExists(true));
        }

        [Fact]
        public async Task DropIndexAsync_IgnoreIfExists_True_Throw_IndexExistsException()
        {
            var manager = CreateManager();
            await Assert.ThrowsAsync<IndexExistsException>(async () => await manager.DropIndexAsync("default", "index1", DropQueryIndexOptions.Default.IgnoreIfExists(false)));
        }

        [Fact]
        public async Task DropIndexAsync_Throws_ArgumentNullException_BucketName_Null()
        {
            var manager = CreateManager();
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await manager.DropIndexAsync(null, "index1", DropQueryIndexOptions.Default.IgnoreIfExists(false)));
        }

        [Fact]
        public async Task DropIndexAsync_Throws_ArgumentNullException_IndexName_Null()
        {
            var manager = CreateManager();
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await manager.DropIndexAsync("default", null, DropQueryIndexOptions.Default.IgnoreIfExists(false)));
        }

        private QueryIndexManager CreateManager()
        {
            using var response = ResourceHelper.ReadResourceAsStream(@"Documents\Query\Management\query-create-primary-index-exists-5000.json");

            var buffer = new byte[response.Length];
            response.Read(buffer, 0, buffer.Length);

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected().Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()).ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new ByteArrayContent(buffer)
            });

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:8093")
            };
            var httpClientFactory = new MockHttpClientFactory(httpClient);

            var mockServiceUriProvider = new Mock<IServiceUriProvider>();
            mockServiceUriProvider
                .Setup(m => m.GetRandomQueryUri())
                .Returns(new Uri("http://localhost:8093"));

            var serializer = new DefaultSerializer();
            var client = new QueryClient(httpClientFactory, mockServiceUriProvider.Object, serializer,
                new Mock<ILogger<QueryClient>>().Object, NoopRequestTracer.Instance);

            return new QueryIndexManager(client, new Mock<ILogger<QueryIndexManager>>().Object,
                new Redactor(new TypedRedactor(RedactionLevel.None)));
        }

        //Check that query_context is not present in QueryIndexManager
        [Fact]
        private async Task Test_BuildDeferredIndexesAsync_QueryContext()
        {
            var client = new FakeQueryClient();
            var manager = new QueryIndexManager(client, new Mock<ILogger<QueryIndexManager>>().Object,
                new Redactor(new TypedRedactor(RedactionLevel.None)));

           await manager.BuildDeferredIndexesAsync("travel-sample");

           Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => { var qc = client.FormValues["query_context"]; });
        }

        [Fact]
        private async Task Test_CreateIndexAsync_QueryContext()
        {
            var client = new FakeQueryClient();
            var manager = new QueryIndexManager(client, new Mock<ILogger<QueryIndexManager>>().Object,
                new Redactor(new TypedRedactor(RedactionLevel.None)));

            await manager.CreateIndexAsync("travel-sample", "index1", new[] { "field1" });

            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => { var qc = client.FormValues["query_context"]; });
        }

        [Fact]
        private async Task Test_DropIndexAsync_QueryContext()
        {
            var client = new FakeQueryClient();
            var manager = new QueryIndexManager(client, new Mock<ILogger<QueryIndexManager>>().Object,
                new Redactor(new TypedRedactor(RedactionLevel.None)));

            await manager.DropIndexAsync("travel-sample", "index1");

            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => { var qc = client.FormValues["query_context"]; });
        }

        [Fact]
        private async Task Test_CreatePrimaryIndexAsync_QueryContext()
        {
            var client = new FakeQueryClient();
            var manager = new QueryIndexManager(client, new Mock<ILogger<QueryIndexManager>>().Object,
                new Redactor(new TypedRedactor(RedactionLevel.None)));

            await manager.CreatePrimaryIndexAsync("travel-sample");

            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => { var qc = client.FormValues["query_context"]; });
        }

        [Fact]
        private async Task Test_DropPrimaryIndexAsync_QueryContext()
        {
            var client = new FakeQueryClient();
            var manager = new QueryIndexManager(client, new Mock<ILogger<QueryIndexManager>>().Object,
                new Redactor(new TypedRedactor(RedactionLevel.None)));

            await manager.DropPrimaryIndexAsync("travel-sample");

            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => { var qc = client.FormValues["query_context"]; });
        }


        [Fact]
        private async Task Test_WatchIndexesAsync_QueryContext()
        {
            var client = new FakeQueryClient();
            var manager = new QueryIndexManager(client, new Mock<ILogger<QueryIndexManager>>().Object,
                new Redactor(new TypedRedactor(RedactionLevel.None)));

            await manager.WatchIndexesAsync("travel-sample", new []{"field1"});

            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => { var qc = client.FormValues["query_context"]; });
        }


        [Fact]
        private async Task Test_GetAllIndexesAsync_QueryContext()
        {
            var client = new FakeQueryClient();
            var manager = new QueryIndexManager(client, new Mock<ILogger<QueryIndexManager>>().Object,
                new Redactor(new TypedRedactor(RedactionLevel.None)));

            await manager.GetAllIndexesAsync("travel-sample");

            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => { var qc = client.FormValues["query_context"]; });
        }

        private class FakeQueryClient : IQueryClient
        {
            public int InvalidateQueryCache()
            {
                throw new NotImplementedException();
            }

            public DateTime? LastActivity { get; }
            public Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions options)
            {
                options.Statement("SELECT 1;");
                FormValues = options.GetFormValues();
                return Task.FromResult((IQueryResult<T>) new FakeQueryResult<T>());
            }

            public void UpdateClusterCapabilities(ClusterCapabilities clusterCapabilities)
            {
            }

            public IDictionary<string, object?> FormValues { get; private set; }
        }

        private class FakeQueryResult<T> : IQueryResult<T>
        {
            private IEnumerable<T>? _rows = Enumerable.Empty<T>();
            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken())
            {
                return _rows.ToAsyncEnumerable().GetAsyncEnumerator();
            }
            public RetryReason RetryReason { get; }
            public IAsyncEnumerable<T> Rows => this;
            public QueryMetaData MetaData { get; }
            public List<Error> Errors { get; }
        }
    }
}
