#nullable  enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.KeyValue;
using Couchbase.KeyValue.RangeScan;
using Couchbase.Management.Query;
using Moq;

namespace Couchbase.Transactions.Tests.UnitTests.Mocks
{
    internal class MockCollection : ICouchbaseCollection
    {
        public Dictionary<string, TransactionGetResult> AllDocs { get; }
        public MockCollection(IEnumerable<TransactionGetResult> mockDocs)
        {
            AllDocs = mockDocs.ToDictionary(o => o.Id);
        }

        public Task<IGetResult> GetAsync(string id, GetOptions? options = null)
        {
            if (AllDocs.TryGetValue(id, out var doc))
            {
                return Task.FromResult((IGetResult)doc);
            }

            throw new DocumentNotFoundException();
        }

        public Task<IExistsResult> ExistsAsync(string id, ExistsOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IMutationResult> UpsertAsync<T>(string id, T content, UpsertOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IMutationResult> InsertAsync<T>(string id, T content, InsertOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IMutationResult> ReplaceAsync<T>(string id, T content, ReplaceOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task RemoveAsync(string id, RemoveOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task UnlockAsync<T>(string id, ulong cas, UnlockOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task TouchAsync(string id, TimeSpan expiry, TouchOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IGetResult> GetAndTouchAsync(string id, TimeSpan expiry, GetAndTouchOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IGetResult> GetAndLockAsync(string id, TimeSpan expiry, GetAndLockOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IGetReplicaResult> GetAnyReplicaAsync(string id, GetAnyReplicaOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Task<IGetReplicaResult>> GetAllReplicasAsync(string id, GetAllReplicasOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task<ILookupInResult> LookupInAsync(string id, IEnumerable<LookupInSpec> specs, LookupInOptions? options = null)
        {
            return Task.FromResult(new Mock<ILookupInResult>().Object);
        }

        public Task<IMutateInResult> MutateInAsync(string id, IEnumerable<MutateInSpec> specs, MutateInOptions? options = null)
        {
            return Task.FromResult(new Mock<IMutateInResult>().Object);
        }

        public static ICluster CreateMockCluster(IEnumerable<TransactionGetResult> mockDocs) =>
            CreateMockCluster(new MockCollection(mockDocs));

        public static ICluster CreateMockCluster(ICouchbaseCollection mockCollection)
        {
            var mockBucket = new Mock<IBucket>(MockBehavior.Strict);
            mockBucket.Setup(b => b.DefaultCollectionAsync())
                .Returns(new ValueTask<ICouchbaseCollection>(mockCollection));
            var mockCluster = new Mock<ICluster>(MockBehavior.Strict);
            mockCluster.Setup(c => c.BucketAsync(It.IsAny<string>()))
                .ReturnsAsync(mockBucket.Object);
            mockCluster.Setup(c => c.Dispose());
            mockCluster.Setup(c => c.DisposeAsync());
            return mockCluster.Object;
        }

        public Task UnlockAsync(string id, ulong cas, UnlockOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<IScanResult> ScanAsync(IScanType scanType, ScanOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public ICollectionQueryIndexManager QueryIndexes { get; } = null!;

        public uint? Cid { get; }
        public string Name { get; } = "default";
        public IScope Scope { get; set; } = new Mock<IScope>().Object;
        public IBinaryCollection Binary { get; } = new Mock<IBinaryCollection>().Object;

        public bool IsDefaultCollection => Name == "_default";
    }

    internal static class MockCollectionExtensions
    {
        internal static string GetKey(this ICouchbaseCollection c, string docId) =>
    $"{c.Scope.Bucket.Name}::{c.Scope.Name}::{c.Name}$${docId}";
    }

    internal class MockCollectionWithNames : ICouchbaseCollection
    {
        public MockCollectionWithNames(string collectionName, string scopeName, string bucketName)
        {
            Name = collectionName;
            var mockScope = new Mock<IScope>(MockBehavior.Strict);
            mockScope.SetupGet(s => s.Name).Returns(scopeName);
            var mockBucket = new Mock<IBucket>(MockBehavior.Strict);
            mockBucket.SetupGet(b => b.Name).Returns(bucketName);
            var defaultCollectionOnBucket = new Mock<ICouchbaseCollection>();
            mockBucket.Setup(b => b.DefaultCollectionAsync()).Returns(new ValueTask<ICouchbaseCollection>(defaultCollectionOnBucket.Object));
            mockBucket.Setup(b => b.DefaultCollection()).Returns(defaultCollectionOnBucket.Object);
            mockScope.SetupGet(s => s.Bucket).Returns(mockBucket.Object);
            Scope = mockScope.Object;
        }

        public uint? Cid => throw new NotImplementedException();

        public string Name { get; }

        public IScope Scope { get; }

        public IBinaryCollection Binary => throw new NotImplementedException();

        public bool IsDefaultCollection => Name == "_default";

        public Task<IExistsResult> ExistsAsync(string id, ExistsOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Task<IGetReplicaResult>> GetAllReplicasAsync(string id, GetAllReplicasOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IGetResult> GetAndLockAsync(string id, TimeSpan expiry, GetAndLockOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IGetResult> GetAndTouchAsync(string id, TimeSpan expiry, GetAndTouchOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IGetReplicaResult> GetAnyReplicaAsync(string id, GetAnyReplicaOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IGetResult> GetAsync(string id, GetOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IMutationResult> InsertAsync<T>(string id, T content, InsertOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task<ILookupInResult> LookupInAsync(string id, IEnumerable<LookupInSpec> specs, LookupInOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IMutateInResult> MutateInAsync(string id, IEnumerable<MutateInSpec> specs, MutateInOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task RemoveAsync(string id, RemoveOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IMutationResult> ReplaceAsync<T>(string id, T content, ReplaceOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<IScanResult> ScanAsync(IScanType scanType, ScanOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public ICollectionQueryIndexManager QueryIndexes { get; } = null!;

        public Task TouchAsync(string id, TimeSpan expiry, TouchOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task UnlockAsync<T>(string id, ulong cas, UnlockOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task UnlockAsync(string id, ulong cas, UnlockOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IMutationResult> UpsertAsync<T>(string id, T content, UpsertOptions? options = null)
        {
            throw new NotImplementedException();
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
