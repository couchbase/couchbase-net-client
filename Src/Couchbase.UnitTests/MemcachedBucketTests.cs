using System;
using System.Collections.Generic;
using System.Threading;
using Couchbase.Analytics;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.N1QL;
using Couchbase.Search;
using Couchbase.Views;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests
{
    [TestFixture]
    public class MemcachedBucketTests
    {
        private MemcachedBucket _bucket;

        [OneTimeSetUp]
        public void Setup()
        {
            var mockController = new Mock<IClusterController>();
            mockController.Setup(x => x.Configuration).Returns(new ClientConfiguration());

            _bucket = new MemcachedBucket(mockController.Object, string.Empty, new DefaultConverter(), new DefaultTranscoder(), null);
        }

        #region NotSupported KV Operations

        [Test]
        public void Observe_With_Durability_throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.Observe("", 0, false, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Observe("", 0, false, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
        }

        [Test]
        public void ObserveAsync_With_Durability_throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.ObserveAsync("", 0, false, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ObserveAsync("", 0, false, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
        }

        [Test]
        public void Insert_With_Durability_throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.Insert("", new { }, ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Insert("", new { }, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Insert("", new { }, ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Insert("", new { }, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Insert("", new { }, TimeSpan.Zero, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Insert("", new { }, TimeSpan.Zero, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.Insert("", new { }, 0, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Insert("", new { }, 0, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.Insert(new Document<dynamic>(), ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Insert(new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Insert(new Document<dynamic>(), ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Insert(new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
        }

        [Test]
        public void InsertAsync_With_Durability_throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.InsertAsync("", new { }, ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.InsertAsync("", new { }, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.InsertAsync("", new { }, ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.InsertAsync("", new { }, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.InsertAsync("", new { }, TimeSpan.Zero, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.InsertAsync("", new { }, TimeSpan.Zero, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.InsertAsync("", new { }, 0, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.InsertAsync("", new { }, 0, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.InsertAsync(new Document<dynamic>(), ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.InsertAsync(new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.InsertAsync(new Document<dynamic>(), ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.InsertAsync(new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.InsertAsync(new List<IDocument<dynamic>>(), ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.InsertAsync(new List<IDocument<dynamic>>(), ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.InsertAsync(new List<IDocument<dynamic>>(), ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.InsertAsync(new List<IDocument<dynamic>>(), ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
        }

        [Test]
        public void Remove_With_Durability_throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.Remove("", ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Remove("", ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Remove("", ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Remove("", ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.Remove("", 0, ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Remove("", 0, ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Remove("", 0, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Remove("", 0, ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Remove("", 0, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.Remove(new Document<dynamic>(), ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Remove(new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Remove(new Document<dynamic>(), ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Remove(new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
        }

        [Test]
        public void RemoveAsync_With_Durability_throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.RemoveAsync("", ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.RemoveAsync("", ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.RemoveAsync("", ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.RemoveAsync("", ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.RemoveAsync("", 0, ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.RemoveAsync("", 0, ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.RemoveAsync("", 0, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.RemoveAsync("", 0, ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.RemoveAsync("", 0, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.RemoveAsync(new Document<dynamic>(), ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.RemoveAsync(new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.RemoveAsync(new Document<dynamic>(), ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.RemoveAsync(new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.RemoveAsync(new List<IDocument<dynamic>>(), ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.RemoveAsync(new List<IDocument<dynamic>>(), ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.RemoveAsync(new List<IDocument<dynamic>>(), ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.RemoveAsync(new List<IDocument<dynamic>>(), ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
        }

        [Test]
        public void Replace_With_Durability_throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.Replace("", new { }, ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Replace("", new { }, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Replace("", new { }, ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Replace("", new { }, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.Replace("", new { }, 0, ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Replace("", new { }, 0, ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Replace("", new { }, 0, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Replace("", new { }, 0, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Replace("", new { }, 0, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Replace("", new { }, 0, 0, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Replace("", new { }, 0, 0, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Replace("", new { }, 0, TimeSpan.Zero, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Replace("", new { }, 0, TimeSpan.Zero, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.Replace("", new Document<dynamic>(), ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Replace("", new Document<dynamic>(), 0, ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Replace("", new Document<dynamic>(), 0, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Replace("", new Document<dynamic>(), 0, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Replace("", new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Replace("", new Document<dynamic>(), ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Replace("", new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.Replace(new Document<dynamic>(), ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Replace(new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Replace(new Document<dynamic>(), ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Replace(new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
        }

        [Test]
        public void ReplaceAsync_With_Durability_throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync("", new { }, ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync("", new { }, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync("", new { }, ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync("", new { }, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync("", new { }, 0, ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync("", new { }, 0, ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync("", new { }, 0, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync("", new { }, 0, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync("", new { }, 0, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync("", new { }, 0, 0, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync("", new { }, 0, 0, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync("", new { }, 0, TimeSpan.Zero, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync("", new { }, 0, TimeSpan.Zero, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync("", new Document<dynamic>(), ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync("", new Document<dynamic>(), 0, ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync("", new Document<dynamic>(), 0, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync("", new Document<dynamic>(), 0, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync("", new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync("", new Document<dynamic>(), ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync("", new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync(new Document<dynamic>(), ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync(new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync(new Document<dynamic>(), ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync(new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync(new List<IDocument<dynamic>>(), ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync(new List<IDocument<dynamic>>(), ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync(new List<IDocument<dynamic>>(), ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.ReplaceAsync(new List<IDocument<dynamic>>(), ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
        }

        [Test]
        public void Upsert_With_Durability_throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.Upsert("", new { }, ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Upsert("", new { }, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Upsert("", new { }, ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Upsert("", new { }, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.Upsert("", new { }, 0, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Upsert("", new { }, 0, 0, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Upsert("", new { }, 0, TimeSpan.Zero, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Upsert("", new { }, 0, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Upsert("", new { }, 0, 0, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Upsert("", new { }, 0, TimeSpan.Zero, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Upsert("", new { }, TimeSpan.Zero, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Upsert("", new { }, TimeSpan.Zero, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.Upsert("", new Document<dynamic>(), ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Upsert("", new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Upsert("", new Document<dynamic>(), ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Upsert("", new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.Upsert(new Document<dynamic>(), ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Upsert(new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Upsert(new Document<dynamic>(), ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.Upsert(new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
        }

        [Test]
        public void UpsertAsync_With_Durability_throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync("", new { }, ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync("", new { }, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync("", new { }, ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync("", new { }, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync("", new { }, 0, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync("", new { }, 0, 0, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync("", new { }, 0, TimeSpan.Zero, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync("", new { }, 0, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync("", new { }, 0, 0, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync("", new { }, 0, TimeSpan.Zero, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync("", new { }, TimeSpan.Zero, ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync("", new { }, TimeSpan.Zero, ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync("", new Document<dynamic>(), ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync("", new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync("", new Document<dynamic>(), ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync("", new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync(new Document<dynamic>(), ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync(new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync(new Document<dynamic>(), ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync(new Document<dynamic>(), ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));

            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync(new List<IDocument<dynamic>>(), ReplicateTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync(new List<IDocument<dynamic>>(), ReplicateTo.Zero, PersistTo.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync(new List<IDocument<dynamic>>(), ReplicateTo.Zero, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.UpsertAsync(new List<IDocument<dynamic>>(), ReplicateTo.Zero, PersistTo.Zero, TimeSpan.Zero));
        }

        [Test]
        public void Exists_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.Exists(""));
            Assert.Throws<NotSupportedException>(() => _bucket.Exists("", TimeSpan.FromSeconds(1)));
        }

        [Test]
        public void ExistsAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.ExistsAsync(""));
            Assert.Throws<NotSupportedException>(() => _bucket.ExistsAsync("", TimeSpan.FromSeconds(1)));
        }

        [Test]
        public void GetFromReplica_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.GetFromReplica<dynamic>(""));
            Assert.Throws<NotSupportedException>(() => _bucket.GetFromReplica<dynamic>("", TimeSpan.Zero));
        }

        [Test]
        public void GetFromReplicaAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.GetFromReplicaAsync<dynamic>(""));
            Assert.Throws<NotSupportedException>(() => _bucket.GetFromReplicaAsync<dynamic>("", TimeSpan.Zero));
        }

        [Test]
        public void GetDocumentFromReplica_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.GetDocumentFromReplica<dynamic>(""));
            Assert.Throws<NotSupportedException>(() => _bucket.GetDocumentFromReplica<dynamic>("", TimeSpan.Zero));
        }

        [Test]
        public void GetDocumentFromReplicaAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.GetDocumentFromReplicaAsync<dynamic>(""));
            Assert.Throws<NotSupportedException>(() => _bucket.GetDocumentFromReplicaAsync<dynamic>("", TimeSpan.Zero));
        }

        [Test]
        public void GetWithLock_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.GetWithLock<dynamic>("", 0));
            Assert.Throws<NotSupportedException>(() => _bucket.GetWithLock<dynamic>("", TimeSpan.Zero));
        }

        [Test]
        public void GetWithLockAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.GetWithLockAsync<dynamic>("", 0));
            Assert.Throws<NotSupportedException>(() => _bucket.GetWithLockAsync<dynamic>("", TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.GetWithLockAsync<dynamic>("", TimeSpan.Zero));
        }

        [Test]
        public void GetAndLock_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.GetAndLock<dynamic>("", 0));
            Assert.Throws<NotSupportedException>(() => _bucket.GetAndLock<dynamic>("", TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.GetAndLock<dynamic>("", 0, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.GetAndLock<dynamic>("", TimeSpan.Zero, TimeSpan.Zero));
        }

        [Test]
        public void GetAndLockAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.GetAndLockAsync<dynamic>("", 0));
            Assert.Throws<NotSupportedException>(() => _bucket.GetAndLockAsync<dynamic>("", TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.GetAndLockAsync<dynamic>("", 0, TimeSpan.Zero));
            Assert.Throws<NotSupportedException>(() => _bucket.GetAndLockAsync<dynamic>("", TimeSpan.Zero, TimeSpan.Zero));
        }

        [Test]
        public void Unlock_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.Unlock("", 0));
            Assert.Throws<NotSupportedException>(() => _bucket.Unlock("", 0, TimeSpan.Zero));
        }

        [Test]
        public void UnlockAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.UnlockAsync("", 0));
            Assert.Throws<NotSupportedException>(() => _bucket.UnlockAsync("", 0, TimeSpan.Zero));
        }

        #endregion

        #region NotSupport Query Operations

        [Test]
        public void Query_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.Query<dynamic>(string.Empty));
            Assert.Throws<NotSupportedException>(() => _bucket.Query<dynamic>(new QueryRequest()));
        }

        [Test]
        public void QueryAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.QueryAsync<dynamic>(string.Empty));
            Assert.Throws<NotSupportedException>(() => _bucket.QueryAsync<dynamic>(new QueryRequest()));
            Assert.Throws<NotSupportedException>(() => _bucket.QueryAsync<dynamic>(new QueryRequest(), CancellationToken.None));
        }

        #endregion

        #region NotSupported View Operations

        [Test]
        public void Query_With_ViewQuery_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.Query<dynamic>(new ViewQuery()));
        }

        [Test]
        public void QueryAsync_With_ViewQuery_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.QueryAsync<dynamic>(new ViewQuery()));
        }

        [Test]
        public void CreateQuery_With_ViewQuery_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.CreateQuery(false));
            Assert.Throws<NotSupportedException>(() => _bucket.CreateQuery("", ""));
            Assert.Throws<NotSupportedException>(() => _bucket.CreateQuery("", "", false));
        }

        #endregion

        #region NotSupported FTS operations

        [Test]
        public void Query_With_SearchQuery_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.Query(new SearchQuery()));
        }

        [Test]
        public void QueryAync_With_SearchQuery_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.QueryAsync(new SearchQuery()));
        }


        #endregion

        #region NotSupported Analytics operations

        [Test]
        public void Query_With_AnalyticsQuery_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.Query<dynamic>(new AnalyticsRequest("")));
        }

        [Test]
        public void QueryAsync_With_AnalyticsQuery_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.QueryAsync<dynamic>(new AnalyticsRequest("")));
            Assert.Throws<NotSupportedException>(() => _bucket.QueryAsync<dynamic>(new AnalyticsRequest(""), CancellationToken.None));
        }

        #endregion

        #region NotSupported Subdoc API

        [Test]
        public void MutateIn_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.MutateIn<dynamic>(""));
            Assert.Throws<NotSupportedException>(() => _bucket.MutateIn<dynamic>("", TimeSpan.Zero));
        }

        [Test]
        public void LookupIn_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.LookupIn<dynamic>(""));
            Assert.Throws<NotSupportedException>(() => _bucket.LookupIn<dynamic>("", TimeSpan.Zero));
        }

        #endregion

        #region NotSupported Data Structures

        #region Map

        [Test]
        public void MapGet_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.MapGet<dynamic>("", ""));
            Assert.Throws<NotSupportedException>(() => _bucket.MapGet<dynamic>("", "", TimeSpan.Zero));
        }

        [Test]
        public void MapGetAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.MapGetAsync<dynamic>("", ""));
            Assert.Throws<NotSupportedException>(() => _bucket.MapGetAsync<dynamic>("", "", TimeSpan.Zero));
        }

        [Test]
        public void MapRemove_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.MapRemove("", ""));
            Assert.Throws<NotSupportedException>(() => _bucket.MapRemove("", "", TimeSpan.Zero));
        }

        [Test]
        public void MapRemoveAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.MapRemoveAsync("", ""));
            Assert.Throws<NotSupportedException>(() => _bucket.MapRemoveAsync("", "", TimeSpan.Zero));
        }

        [Test]
        public void MapSize_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.MapSize(""));
            Assert.Throws<NotSupportedException>(() => _bucket.MapSize("", TimeSpan.Zero));
        }

        [Test]
        public void MapSizeAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.MapSizeAsync(""));
            Assert.Throws<NotSupportedException>(() => _bucket.MapSizeAsync("", TimeSpan.Zero));
        }

        [Test]
        public void MapAdd_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.MapAdd("", "", "", false));
            Assert.Throws<NotSupportedException>(() => _bucket.MapAdd("", "", "", false, TimeSpan.Zero));
        }

        [Test]
        public void MapAddAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.MapAddAsync("", "", "", false));
            Assert.Throws<NotSupportedException>(() => _bucket.MapAddAsync("", "", "", false, TimeSpan.Zero));
        }


        #endregion

        #region List

        [Test]
        public void ListGet_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.ListGet<dynamic>("", 0));
            Assert.Throws<NotSupportedException>(() => _bucket.ListGet<dynamic>("", 0, TimeSpan.Zero));
        }

        [Test]
        public void ListGetAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.ListGetAsync<dynamic>("", 0));
            Assert.Throws<NotSupportedException>(() => _bucket.ListGetAsync<dynamic>("", 0, TimeSpan.Zero));
        }

        [Test]
        public void ListAppend_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.ListAppend("", null, false));
            Assert.Throws<NotSupportedException>(() => _bucket.ListAppend("", null, false, TimeSpan.Zero));
        }

        [Test]
        public void ListAppendAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.ListAppendAsync("", null, false));
            Assert.Throws<NotSupportedException>(() => _bucket.ListAppendAsync("", null, false, TimeSpan.Zero));
        }

        [Test]
        public void ListPrepend_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.ListPrepend("", null, false));
            Assert.Throws<NotSupportedException>(() => _bucket.ListPrepend("", null, false, TimeSpan.Zero));
        }

        [Test]
        public void ListPrependAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.ListPrependAsync("", null, false));
            Assert.Throws<NotSupportedException>(() => _bucket.ListPrependAsync("", null, false, TimeSpan.Zero));
        }

        [Test]
        public void ListRemove_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.ListRemove("", 0));
            Assert.Throws<NotSupportedException>(() => _bucket.ListRemove("", 0, TimeSpan.Zero));
        }

        [Test]
        public void ListRemoveAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.ListRemoveAsync("", 0));
            Assert.Throws<NotSupportedException>(() => _bucket.ListRemoveAsync("", 0, TimeSpan.Zero));
        }

        [Test]
        public void ListSet_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.ListSet("", 0, ""));
            Assert.Throws<NotSupportedException>(() => _bucket.ListSet("", 0, "", TimeSpan.Zero));
        }

        [Test]
        public void ListSetAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.ListSetAsync("", 0, ""));
            Assert.Throws<NotSupportedException>(() => _bucket.ListSetAsync("", 0, "", TimeSpan.Zero));
        }

        [Test]
        public void ListSize_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.ListSize(""));
            Assert.Throws<NotSupportedException>(() => _bucket.ListSize("", TimeSpan.Zero));
        }

        [Test]
        public void ListSizeAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.ListSizeAsync(""));
            Assert.Throws<NotSupportedException>(() => _bucket.ListSizeAsync("", TimeSpan.Zero));
        }

        #endregion

        #region Set

        [Test]
        public void SetAdd_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.SetAdd("", "", false));
            Assert.Throws<NotSupportedException>(() => _bucket.SetAdd("", "", false, TimeSpan.Zero));
        }

        [Test]
        public void SetAddAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.SetAddAsync("", "", false));
            Assert.Throws<NotSupportedException>(() => _bucket.SetAddAsync("", "", false, TimeSpan.Zero));
        }

        [Test]
        public void SetContains_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.SetContains("", ""));
            Assert.Throws<NotSupportedException>(() => _bucket.SetContains("", "", TimeSpan.Zero));
        }

        [Test]
        public void SetContainsAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.SetContainsAsync("", ""));
            Assert.Throws<NotSupportedException>(() => _bucket.SetContainsAsync("", "", TimeSpan.Zero));
        }

        [Test]
        public void SetSize_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.SetSize(""));
            Assert.Throws<NotSupportedException>(() => _bucket.SetSize("", TimeSpan.Zero));
        }

        [Test]
        public void SetSizeAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.SetSizeAsync(""));
            Assert.Throws<NotSupportedException>(() => _bucket.SetSizeAsync("", TimeSpan.Zero));
        }

        [Test]
        public void SetRemove_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.SetRemove("", 0));
            Assert.Throws<NotSupportedException>(() => _bucket.SetRemove("", 0, TimeSpan.Zero));
        }

        [Test]
        public void SetRemoveAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.SetRemoveAsync("", 0));
            Assert.Throws<NotSupportedException>(() => _bucket.SetRemoveAsync("", 0, TimeSpan.Zero));
        }

        #endregion

        #region Queue

        [Test]
        public void QueuePush_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.QueuePush("", 0, false));
            Assert.Throws<NotSupportedException>(() => _bucket.QueuePush("", 0, false, TimeSpan.Zero));
        }

        [Test]
        public void QueuePushAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.QueuePushAsync("", 0, false));
            Assert.Throws<NotSupportedException>(() => _bucket.QueuePushAsync("", 0, false, TimeSpan.Zero));
        }

        [Test]
        public void QueuePop_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.QueuePop<dynamic>(""));
            Assert.Throws<NotSupportedException>(() => _bucket.QueuePop<dynamic>("", TimeSpan.Zero));
        }

        [Test]
        public void QueuePopAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.QueuePopAsync<dynamic>(""));
            Assert.Throws<NotSupportedException>(() => _bucket.QueuePopAsync<dynamic>("", TimeSpan.Zero));
        }

        [Test]
        public void QueueSize_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.QueueSize(""));
            Assert.Throws<NotSupportedException>(() => _bucket.QueueSize("", TimeSpan.Zero));
        }

        [Test]
        public void QueueSizeAsync_Throws_NotSupportedException()
        {
            Assert.Throws<NotSupportedException>(() => _bucket.QueueSizeAsync(""));
            Assert.Throws<NotSupportedException>(() => _bucket.QueueSizeAsync("", TimeSpan.Zero));
        }

        #endregion

        #endregion
    }
}
