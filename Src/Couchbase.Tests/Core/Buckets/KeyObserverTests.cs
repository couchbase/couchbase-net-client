using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers.CarrierPublication;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Strategies;
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
                (pool) => new DefaultIOStrategy(pool),
                (config, endpoint) => new ConnectionPool<Connection>(config, endpoint),
                SaslFactory.GetFactory3(),
                new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()));

            var configInfo = provider.GetConfig("default");

            var observer = new KeyObserver(configInfo, 10, 500);
            var constraintReached = observer.ObserveRemove("Test_Timeout_Remove", 0, ReplicateTo.Zero, PersistTo.One);
            Assert.IsTrue(constraintReached);
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
                (pool) => new DefaultIOStrategy(pool),
                (config, endpoint) => new ConnectionPool<Connection>(config, endpoint),
                SaslFactory.GetFactory3(),
                new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()));

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
            var observer = new KeyObserver(configInfo, 10, 500);
            var constraintReached = observer.ObserveAdd("Test_Timeout_Add", cas, ReplicateTo.One, PersistTo.One);
            Assert.IsTrue(constraintReached);
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
                (pool) => new DefaultIOStrategy(pool),
                (config, endpoint) => new ConnectionPool<Connection>(config, endpoint),
                SaslFactory.GetFactory3(),
                new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()));

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
            var observer = new KeyObserver(configInfo, 10, 500);
            var constraintReached = observer.ObserveAdd("When_Mutation_Happens_Observe_Fails", cas, ReplicateTo.One, PersistTo.One);
            Assert.IsFalse(constraintReached);
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
                (pool) => new DefaultIOStrategy(pool),
                (config, endpoint) => new ConnectionPool<Connection>(config, endpoint),
                SaslFactory.GetFactory3(),
                new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()));

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

            var observer = new KeyObserver(configInfo, 10, 500);
            var constraintReached = observer.ObserveAdd("Test_Timeout_Add_PersistTo_Master", result.Cas, ReplicateTo.Zero, PersistTo.Zero);
            Assert.IsTrue(constraintReached);
        }

        [Test]
        public void When_ReplicateTo_Is_Greater_Than_PersistTo_Length_Of_Replicas_Is_ReplicateTo()
        {
            var vBucket = new VBucket(null, 0, 0, new[] {0, 2, 1});
            var expected = new[] {0, 2};
            var observer = new KeyObserver(null, 10, 500);
            var actual = observer.GetReplicas(vBucket, ReplicateTo.Two, PersistTo.One);
            Assert.AreEqual(expected, actual);
            Assert.AreEqual(actual.Count, (int)ReplicateTo.Two);
        }

        [Test]
        public void When_PersistTo_Is_Greater_Than_ReplicateTo_Length_Of_Replicas_Is_PersistTo()
        {
            var vBucket = new VBucket(null, 0, 0, new[] { 0, 2, 1 });
            var expected = new[] { 0, 2 };
            var observer = new KeyObserver(null, 10, 500);
            var actual = observer.GetReplicas(vBucket, ReplicateTo.One, PersistTo.Two);
            Assert.AreEqual(expected, actual);
            Assert.AreEqual(actual.Count, (int)PersistTo.Two);
        }

        [Test]
        public void When_No_Replicas_Are_Found_GetReplicas_Returns_Empty_List()
        {
            var vBucket = new VBucket(null, 0, 0, new[] { -1, -1, -1 });
            var expected = new int[] {};
            var observer = new KeyObserver(null, 10, 500);
            var actual = observer.GetReplicas(vBucket, ReplicateTo.One, PersistTo.Two);
            Assert.AreEqual(expected, actual);
            Assert.AreEqual(expected.Count(), actual.Count());
        }
    }
}
