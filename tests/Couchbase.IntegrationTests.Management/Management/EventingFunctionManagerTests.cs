using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Couchbase.Management.Eventing;
using Xunit;
using Xunit.Abstractions;
using Couchbase.Test.Common.Utils;
using Xunit.Sdk;
using Couchbase.Test.Common;
using Couchbase.Test.Common.Fixtures;

namespace Couchbase.IntegrationTests.Management.Management
{
    [Collection(NonParallelDefinition.Name)]
    public class EventingFunctionManagerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        private readonly string _bucketName;
        private const string ScopeName = "eventing";

        private static ICouchbaseCollection _sourceCollection;
        private static ICouchbaseCollection _metaCollection;
        private static IEventingFunctionManager _eventingFunctionManager;

        private volatile bool _collectionsCreated;

        public EventingFunctionManagerTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            _outputHelper = outputHelper;
            _eventingFunctionManager = _fixture.Cluster.EventingFunctions;
            _bucketName = _fixture.GetDefaultBucket().Result.Name;
        }

        private async Task SetupTests()
        {
            if (!_collectionsCreated)
            {
                var bucket = _fixture.GetDefaultBucket().Result;
                await bucket.WaitUntilReadyAsync(TimeSpan.FromSeconds(10));

                try
                {
                    var collectionManager = bucket.Collections;
                    await collectionManager.CreateScopeAsync(ScopeName);
                    await collectionManager.CreateCollectionAsync(ScopeName, "source", new CreateCollectionSettings());
                    await collectionManager.CreateCollectionAsync(ScopeName, "meta", new CreateCollectionSettings());
                }
                catch (ScopeExistsException)
                {
                    //Already created
                }

                var scope = await bucket.ScopeAsync(ScopeName);
                _sourceCollection = await scope.CollectionAsync("source");
                _metaCollection = await scope.CollectionAsync("meta");

                _collectionsCreated = true;
            }
        }

        [Fact]
        public async Task Test_Upsert_Get_Drop_Functions()
        {
            await SetupTests();

            var funcName = "randomFunctionName";
            var function = new EventingFunction
            {
                Name = funcName,
                Code = "function OnUpdate(doc, meta) {}",
                SourceKeySpace = new EventingFunctionKeyspace(_bucketName, ScopeName, _sourceCollection.Name),
                MetaDataKeySpace = new EventingFunctionKeyspace(_bucketName, ScopeName, _metaCollection.Name),
            };

            await _eventingFunctionManager.UpsertFunctionAsync(function);

            var read = await _eventingFunctionManager.GetFunctionAsync(funcName);
            Assert.Equal("function OnUpdate(doc, meta) {}", read.Code);
            var results = await _eventingFunctionManager.GetAllFunctionsAsync();
            Assert.Contains(funcName, results.Select(func => func.Name));

            await _eventingFunctionManager.DropFunctionAsync(funcName);
            results = await _eventingFunctionManager.GetAllFunctionsAsync();
            Assert.DoesNotContain(funcName, results.Select(func => func.Name));
        }

        [Fact]
        public async Task Test_UpsertFunctionAsync_EventingCompilationFailureException()
        {
            await SetupTests();

            await Assert.ThrowsAsync<EventingFunctionCompilationFailureException>(async () =>
                await _eventingFunctionManager.UpsertFunctionAsync(new EventingFunction
                {
                    Name = "invalidFuncName",
                    Code = "invalidFunc",
                    SourceKeySpace = new EventingFunctionKeyspace(_bucketName, ScopeName, _sourceCollection.Name),
                    MetaDataKeySpace = new EventingFunctionKeyspace(_bucketName, ScopeName, _metaCollection.Name),
                }));
        }

        [Fact]
        public async Task Test_UpsertFunctionAsync_CollectionNotFoundException()
        {
            await SetupTests();

            await Assert.ThrowsAsync<Couchbase.Management.Collections.CollectionNotFoundException>(async () =>
                await _eventingFunctionManager.UpsertFunctionAsync(new EventingFunction
                {
                    Name = "collNotFound",
                    Code = "function OnUpdate(doc, meta) {}",
                    SourceKeySpace = new EventingFunctionKeyspace(_bucketName, ScopeName, "made-up"),
                    MetaDataKeySpace = new EventingFunctionKeyspace(_bucketName, ScopeName, _metaCollection.Name),
                }));
        }

        [Fact]
        public async Task Test_UpsertFunctionAsync_BucketNotFoundException()
        {
            await SetupTests();

            await Assert.ThrowsAsync<BucketNotFoundException>(async () =>
                await _eventingFunctionManager.UpsertFunctionAsync(new EventingFunction
                {
                    Name = "bucketNotFound",
                    Code = "function OnUpdate(doc, meta) {}",
                    SourceKeySpace = new EventingFunctionKeyspace("fake-bucket", ScopeName, _sourceCollection.Name),
                    MetaDataKeySpace = new EventingFunctionKeyspace("faker-bucket", ScopeName, _metaCollection.Name),
                }));
        }

        [Fact]
        public async Task Test_UpsertFunctionAsync_IdenticalKeyspaceException()
        {
            await SetupTests();

            await Assert.ThrowsAsync<EventingFunctionIdenticalKeyspaceException>(async () =>
                await _eventingFunctionManager.UpsertFunctionAsync(new EventingFunction
                {
                    Name = "keyspaceIdentical",
                    Code = "function OnUpdate(doc, meta) {}",
                    SourceKeySpace = new EventingFunctionKeyspace(_bucketName, ScopeName, _sourceCollection.Name),
                    MetaDataKeySpace = new EventingFunctionKeyspace(_bucketName, ScopeName, _sourceCollection.Name),
                }));
        }

        [Fact]
        public async Task Test_Fail_With_Unknown_Function_Name()
        {
            await SetupTests();

            var randomFunctionName = "doesNotExist";

            await Assert.ThrowsAsync<EventingFunctionNotFoundException>(async () =>
                await _eventingFunctionManager.GetFunctionAsync(randomFunctionName));
            await Assert.ThrowsAsync<EventingFunctionNotFoundException>(async () =>
                await _eventingFunctionManager.DeployFunctionAsync(randomFunctionName));
            await Assert.ThrowsAsync<EventingFunctionNotFoundException>(async () =>
                await _eventingFunctionManager.PauseFunctionAsync(randomFunctionName));

            await Assert.ThrowsAsync<EventingFunctionNotFoundException>(async () =>
                await _eventingFunctionManager.DropFunctionAsync(randomFunctionName));
            await Assert.ThrowsAsync<EventingFunctionNotFoundException>(async () =>
                await _eventingFunctionManager.UndeployFunctionAsync(randomFunctionName));
            await Assert.ThrowsAsync<EventingFunctionNotFoundException>(async () =>
                await _eventingFunctionManager.ResumeFunctionAsync(randomFunctionName));
        }

        [Fact]
        public async Task Test_Deploys_And_Undeploys_Function()
        {
            await SetupTests();

            var funcName = "deployUndeployTestFunc";
            var function = new EventingFunction
            {
                Name = funcName,
                Code = "function OnUpdate(doc, meta) {}",
                SourceKeySpace = new EventingFunctionKeyspace(_bucketName, ScopeName, _sourceCollection.Name),
                MetaDataKeySpace = new EventingFunctionKeyspace(_bucketName, ScopeName, _metaCollection.Name),
            };

            await _eventingFunctionManager.UpsertFunctionAsync(function);

            var read = await _eventingFunctionManager.GetFunctionAsync(funcName);
            Assert.Equal(EventingFunctionDeploymentStatus.Undeployed, read.Settings.DeploymentStatus);

            await Assert.ThrowsAsync<EventingFunctionNotDeployedException>(async () =>
                await _eventingFunctionManager.UndeployFunctionAsync(funcName));
            await _eventingFunctionManager.DeployFunctionAsync(funcName);

            //Wait until status is deployed
            await Retry.DoUntilAsync(() => IsState(funcName, EventingFunctionStatus.Deployed));

            read = await _eventingFunctionManager.GetFunctionAsync(funcName);
            Assert.Equal(EventingFunctionDeploymentStatus.Deployed, read.Settings.DeploymentStatus);

            await _eventingFunctionManager.UndeployFunctionAsync(funcName);

            //Wait until status is undeployed
            await Retry.DoUntilAsync(() => IsState(funcName, EventingFunctionStatus.Undeployed));

            read = await _eventingFunctionManager.GetFunctionAsync(funcName);
            Assert.Equal(EventingFunctionDeploymentStatus.Undeployed, read.Settings.DeploymentStatus);

            await _eventingFunctionManager.DropFunctionAsync(funcName);
        }

        [Fact]
        public async Task Test_Pauses_And_Resumes_Function()
        {
            await SetupTests();

            var funcName = "deployPauseResumeTestFunc";
            var function = new EventingFunction
            {
                Name = funcName,
                Code = "function OnUpdate(doc, meta) {}",
                SourceKeySpace = new EventingFunctionKeyspace(_bucketName, ScopeName, _sourceCollection.Name),
                MetaDataKeySpace = new EventingFunctionKeyspace(_bucketName, ScopeName, _metaCollection.Name),
            };

            await _eventingFunctionManager.UpsertFunctionAsync(function);

            var read = await _eventingFunctionManager.GetFunctionAsync(funcName);

            Assert.Equal(EventingFunctionProcessingStatus.Paused, read.Settings.ProcessingStatus);

            await Assert.ThrowsAsync<EventingFunctionNotBootstrappedException>(async () =>
                await _eventingFunctionManager.PauseFunctionAsync(funcName));
            await Assert.ThrowsAsync<EventingFunctionNotDeployedException>(async () =>
                await _eventingFunctionManager.ResumeFunctionAsync(funcName));

            await _eventingFunctionManager.DeployFunctionAsync(funcName);

            //Wait until deployed
            await Retry.DoUntilAsync(() => IsState(funcName, EventingFunctionStatus.Deployed));

            read = await _eventingFunctionManager.GetFunctionAsync(funcName);
            Assert.Equal(EventingFunctionProcessingStatus.Running, read.Settings.ProcessingStatus);

            await _eventingFunctionManager.PauseFunctionAsync(funcName);

            await Retry.DoUntilAsync(() => IsState(funcName, EventingFunctionStatus.Paused));

            read = await _eventingFunctionManager.GetFunctionAsync(funcName);
            Assert.Equal(EventingFunctionProcessingStatus.Paused, read.Settings.ProcessingStatus);

            await _eventingFunctionManager.UndeployFunctionAsync(funcName);

            //Wait until undeployed
            await Retry.DoUntilAsync(() => IsState(funcName, EventingFunctionStatus.Undeployed));
            await _eventingFunctionManager.DropFunctionAsync(funcName);
        }

        private static bool IsState(String funcName, EventingFunctionStatus status)
        {
            var results = _eventingFunctionManager.FunctionsStatus().Result;
            return results.Functions.Exists(state => state.Name == funcName && state.Status == status);
        }
    }
}
