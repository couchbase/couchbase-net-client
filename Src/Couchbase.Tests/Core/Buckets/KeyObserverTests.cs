using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers.CarrierPublication;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Core.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Services;
using Moq;
using NUnit.Framework;

namespace Couchbase.Tests.Core.Buckets
{
    [TestFixture]
    public class KeyObserverTests
    {
        [Test]
        public void When_Observing_Key_During_Remove_Durability_Constraint_Is_Reached()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                }
            };
            configuration.Initialize();

            var provider = new CarrierPublicationProvider(
                configuration,
                (pool) => new PooledIOService(pool),
                (config, endpoint) => new ConnectionPool<Connection>(config, endpoint),
                SaslFactory.GetFactory(),
                new DefaultConverter(),
#pragma warning disable 618
                new DefaultTranscoder(new DefaultConverter(), new DefaultSerializer()));
#pragma warning restore 618

            var configInfo = provider.GetConfig("default");

            var clusterController = new Mock<IClusterController>();
            clusterController.Setup(x => x.Transcoder).Returns(new DefaultTranscoder());

            var pending = new ConcurrentDictionary<uint, IOperation>();

            var observer = new KeyObserver(pending, configInfo, clusterController.Object, 10, 500);
            var constraintReached = observer.ObserveRemove("Test_Timeout_Remove", 0, ReplicateTo.Zero, PersistTo.One);
            Assert.IsTrue(constraintReached);
        }

        [Test]
        public async Task When_Observing_Key_During_RemoveAsync_Durability_Constraint_Is_Reached()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                }
            };
            configuration.Initialize();

            var provider = new CarrierPublicationProvider(
                configuration,
                (pool) => new PooledIOService(pool),
                (config, endpoint) => new ConnectionPool<Connection>(config, endpoint),
                SaslFactory.GetFactory(),
                new DefaultConverter(),
                new DefaultTranscoder(new DefaultConverter()));

            var configInfo = provider.GetConfig("default");

            var clusterController = new Mock<IClusterController>();
            clusterController.Setup(x => x.Transcoder).Returns(new DefaultTranscoder());

            var pending = new ConcurrentDictionary<uint, IOperation>();

            var observer = new KeyObserver(pending, configInfo, clusterController.Object, 10, 500);
            using (var cts = new CancellationTokenSource(configuration.ObserveTimeout))
            {
                var constraintReached =
                    await observer.ObserveRemoveAsync("Test_Timeout_Remove_Async", 0, ReplicateTo.Zero, PersistTo.One, cts);
                Assert.IsTrue(constraintReached);
            }
        }

        [Test]
        public void When_Observing_Key_During_Add_Durability_Constraint_Is_Reached()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                }
            };
            configuration.Initialize();

            var provider = new CarrierPublicationProvider(
                configuration,
                (pool) => new PooledIOService(pool),
                (config, endpoint) => new ConnectionPool<Connection>(config, endpoint),
                SaslFactory.GetFactory(),
                new DefaultConverter(),
                new DefaultTranscoder(new DefaultConverter()));

            var configInfo = provider.GetConfig("default");

            ulong cas = 0;
                using (var cluster = new Cluster(configuration))
                {
                    using (var bucket = cluster.OpenBucket())
                    {
                        bucket.Remove("Test_Timeout_Add");
                        bucket.Insert("Test_Timeout_Add", "");
                        cas = bucket.Upsert("Test_Timeout_Add", "").Cas;
                    }
                }
            var clusterController = new Mock<IClusterController>();
            clusterController.Setup(x => x.Transcoder).Returns(new DefaultTranscoder());

            var pending = new ConcurrentDictionary<uint, IOperation>();

            var observer = new KeyObserver(pending, configInfo, clusterController.Object, 10, 500);
            var constraintReached = observer.ObserveAdd("Test_Timeout_Add", cas, ReplicateTo.Zero, PersistTo.Zero);
            Assert.IsTrue(constraintReached);
        }

        [Test]
        public async Task When_Observing_Key_During_AddAsync_Durability_Constraint_Is_Reached()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                }
            };
            configuration.Initialize();

            var provider = new CarrierPublicationProvider(
                configuration,
                (pool) => new PooledIOService(pool),
                (config, endpoint) => new ConnectionPool<Connection>(config, endpoint),
                SaslFactory.GetFactory(),
                new DefaultConverter(),
                new DefaultTranscoder(new DefaultConverter()));

            var configInfo = provider.GetConfig("default");

            var key = "Test_Timeout_Add_Async";
            ulong cas = 0;
            using (var cluster = new Cluster(configuration))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    bucket.Remove(key);
                    bucket.Insert(key, "");
                    cas = bucket.Upsert(key, "").Cas;
                }
            }
            var clusterController = new Mock<IClusterController>();
            clusterController.Setup(x => x.Transcoder).Returns(new DefaultTranscoder());

            var pending = new ConcurrentDictionary<uint, IOperation>();

            var observer = new KeyObserver(pending, configInfo, clusterController.Object, 10, 500);
            using (var cts = new CancellationTokenSource(configuration.ObserveTimeout))
            {
                var constraintReached = await observer.ObserveAddAsync(key, cas, ReplicateTo.Zero, PersistTo.Zero, cts);
                Assert.IsTrue(constraintReached);
            }
        }

        [Test]
        public void When_Mutation_Happens_Observe_Fails()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                }
            };
            configuration.Initialize();

            var provider = new CarrierPublicationProvider(
                configuration,
                (pool) => new PooledIOService(pool),
                (config, endpoint) => new ConnectionPool<Connection>(config, endpoint),
                SaslFactory.GetFactory(),
                new DefaultConverter(),
                new DefaultTranscoder(new DefaultConverter()));

            var configInfo = provider.GetConfig("default");

            ulong cas = 0;
            using (var cluster = new Cluster(configuration))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    bucket.Remove("When_Mutation_Happens_Observe_Fails");
                    cas = bucket.Insert("When_Mutation_Happens_Observe_Fails", "").Cas;
                    bucket.Upsert("When_Mutation_Happens_Observe_Fails", "");
                }
            }
            var clusterController = new Mock<IClusterController>();
            clusterController.Setup(x => x.Transcoder).Returns(new DefaultTranscoder());

            var pending = new ConcurrentDictionary<uint, IOperation>();

            var observer = new KeyObserver(pending, configInfo, clusterController.Object, 10, 500);
            var constraintReached = observer.ObserveAdd("When_Mutation_Happens_Observe_Fails", cas, ReplicateTo.One, PersistTo.One);
            Assert.IsFalse(constraintReached);
        }

        [Test]
        public async Task When_Mutation_Happens_Observe_Fails_Async()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                }
            };
            configuration.Initialize();

            var provider = new CarrierPublicationProvider(
                configuration,
                (pool) => new PooledIOService(pool),
                (config, endpoint) => new ConnectionPool<Connection>(config, endpoint),
                SaslFactory.GetFactory(),
                new DefaultConverter(),
                new DefaultTranscoder(new DefaultConverter()));

            var configInfo = provider.GetConfig("default");

            var key = "When_Mutation_Happens_Observe_Fails_async";
            ulong cas = 0;
            using (var cluster = new Cluster(configuration))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    bucket.Remove(key);
                    cas = bucket.Insert(key, "").Cas;
                    bucket.Upsert(key, "");
                }
            }
            var clusterController = new Mock<IClusterController>();
            clusterController.Setup(x => x.Transcoder).Returns(new DefaultTranscoder());

            var pending = new ConcurrentDictionary<uint, IOperation>();

            var observer = new KeyObserver(pending, configInfo, clusterController.Object, 10, 500);
            using (var cts = new CancellationTokenSource(configuration.ObserveTimeout))
            {
                var constraintReached = await observer.ObserveAddAsync(key, cas, ReplicateTo.One, PersistTo.One, cts);
                Assert.IsFalse(constraintReached);
            }
        }

        [Test]
        public void Test_Timeout_Add_PersistTo_Master()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                }
            };
            configuration.Initialize();

            var provider = new CarrierPublicationProvider(
                configuration,
                (pool) => new PooledIOService(pool),
                (config, endpoint) => new ConnectionPool<Connection>(config, endpoint),
                SaslFactory.GetFactory(),
                new DefaultConverter(),
                new DefaultTranscoder(new DefaultConverter()));

            var configInfo = provider.GetConfig("default");

            IOperationResult result;
            using (var cluster = new Cluster(configuration))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    bucket.Remove("Test_Timeout_Add_PersistTo_Master");
                    result=bucket.Insert("Test_Timeout_Add_PersistTo_Master", "");
                }
            }

            var clusterController = new Mock<IClusterController>();
            clusterController.Setup(x => x.Transcoder).Returns(new DefaultTranscoder());

            var pending = new ConcurrentDictionary<uint, IOperation>();

            var observer = new KeyObserver(pending, configInfo, clusterController.Object, 10, 500);
            var constraintReached = observer.ObserveAdd("Test_Timeout_Add_PersistTo_Master", result.Cas, ReplicateTo.Zero, PersistTo.Zero);
            Assert.IsTrue(constraintReached);
        }


        [Test]
        public async Task Test_Timeout_Add_PersistTo_Master_Async()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                }
            };
            configuration.Initialize();

            var provider = new CarrierPublicationProvider(
                configuration,
                (pool) => new PooledIOService(pool),
                (config, endpoint) => new ConnectionPool<Connection>(config, endpoint),
                SaslFactory.GetFactory(),
                new DefaultConverter(),
                new DefaultTranscoder(new DefaultConverter()));

            var configInfo = provider.GetConfig("default");

            var key = "Test_Timeout_Add_PersistTo_Master_Async";
            IOperationResult result;
            using (var cluster = new Cluster(configuration))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    bucket.Remove(key);
                    result = bucket.Insert(key, "");
                }
            }

            var clusterController = new Mock<IClusterController>();
            clusterController.Setup(x => x.Transcoder).Returns(new DefaultTranscoder());

            var pending = new ConcurrentDictionary<uint, IOperation>();

            var observer = new KeyObserver(pending, configInfo, clusterController.Object, 10, 500);
            using (var cts = new CancellationTokenSource(configuration.ObserveTimeout))
            {
                var constraintReached =
                    await observer.ObserveAddAsync(key, result.Cas, ReplicateTo.Zero, PersistTo.Zero, cts);
                Assert.IsTrue(constraintReached);
            }
        }

        [Test]
        public void When_ReplicateTo_Is_Greater_Than_PersistTo_Length_Of_Replicas_Is_ReplicateTo()
        {
            var vBucket = new VBucket(null, 0, 0, new[] {0, 2, 1}, 0, new VBucketServerMap {ServerList = new string[]{}}, "default");
            var expected = new[] {0, 2};
            var clusterController = new Mock<IClusterController>();
            clusterController.Setup(x => x.Transcoder).Returns(new DefaultTranscoder());


            var pending = new ConcurrentDictionary<uint, IOperation>();

            var observer = new KeyObserver(pending, null, clusterController.Object, 10, 500);
            var actual = observer.GetReplicas(vBucket, ReplicateTo.Two, PersistTo.One);
            Assert.AreEqual(expected, actual);
            Assert.AreEqual(actual.Count, (int)ReplicateTo.Two);
        }

        [Test]
        public void When_PersistTo_Is_Greater_Than_ReplicateTo_Length_Of_Replicas_Is_PersistTo()
        {
            var vBucket = new VBucket(null, 0, 0, new[] { 0, 2, 1 }, 0, new VBucketServerMap { ServerList = new string[] { } }, "default");
            var expected = new[] { 0, 2 };

            var clusterController = new Mock<IClusterController>();
            clusterController.Setup(x => x.Transcoder).Returns(new DefaultTranscoder());
            var pending = new ConcurrentDictionary<uint, IOperation>();
            var observer = new KeyObserver(pending, null, clusterController.Object, 10, 500);

            var actual = observer.GetReplicas(vBucket, ReplicateTo.One, PersistTo.Two);

            Assert.AreEqual(expected, actual);
            Assert.AreEqual(actual.Count, (int)PersistTo.Two);
        }

        [Test]
        public void When_No_Replicas_Are_Found_GetReplicas_Returns_Empty_List()
        {
            var vBucket = new VBucket(null, 0, 0, new[] { -1, -1, -1 }, 0, new VBucketServerMap { ServerList = new string[] { } }, "default");
            var expected = new int[] {};

            var clusterController = new Mock<IClusterController>();
            clusterController.Setup(x => x.Transcoder).Returns(new DefaultTranscoder());
            var pending = new ConcurrentDictionary<uint, IOperation>();
            var observer = new KeyObserver(pending, null, clusterController.Object, 10, 500);

            var actual = observer.GetReplicas(vBucket, ReplicateTo.One, PersistTo.Two);

            Assert.AreEqual(expected, actual);
            Assert.AreEqual(expected.Count(), actual.Count());
        }
    }
}
