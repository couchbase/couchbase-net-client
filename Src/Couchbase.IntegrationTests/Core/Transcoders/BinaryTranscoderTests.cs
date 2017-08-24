#if NET45
using System;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IntegrationTests.Utils;
using NUnit.Framework;

namespace Couchbase.IntegrationTests.Core.Transcoders
{
    [TestFixture]
    public class BinaryTranscoderTests
    {
        private ICluster _cluster;

        [OneTimeSetUp]
        public void Setup()
        {
            var config = Utils.TestConfiguration.GetCurrentConfiguration();
            config.Transcoder = () => new BinaryTranscoder();
            _cluster = new Cluster(config);
            _cluster.SetupEnhancedAuth();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            _cluster.Dispose();
        }

        [Serializable]
        public class Person : IEquatable<Person>
        {
            public string Name { get; set; }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Person) obj);
            }

            public bool Equals(Person other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return string.Equals(Name, other.Name);
            }

            public override int GetHashCode()
            {
                return (Name != null ? Name.GetHashCode() : 0);
            }
        }

        [Test]
        public void Can_Upsert_And_Get_Using_BinaryTranscoder()
        {
            const string key = "person:mike";
            var person = new Person {Name = "mike"};

            using (var bucket = _cluster.OpenBucket("default"))
            {
                try
                {
                    var upsertResult = bucket.Upsert(key, person);
                    Assert.IsTrue(upsertResult.Success);

                    var getResult = bucket.Get<dynamic>(key);
                    Assert.IsTrue(getResult.Success);
                    Assert.AreEqual(person, getResult.Value);
                }
                finally
                {
                    bucket.Remove(key);
                }
            }
        }
    }
}
#endif
