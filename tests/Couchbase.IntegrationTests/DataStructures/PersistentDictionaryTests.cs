using System;
using System.Threading.Tasks;
using Couchbase.Core.Logging;
using Couchbase.DataStructures;
using Couchbase.IntegrationTests.Fixtures;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.IntegrationTests.DataStructures
{
    public class PersistentDictionaryTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        public PersistentDictionaryTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        public class Foo : IEquatable<Foo>
        {
            public string Name { get; set; }

            public int Age { get; set; }

            public bool Equals(Foo other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Name == other.Name && Age == other.Age;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Foo) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ Age;
                }
            }

            public static bool operator ==(Foo left, Foo right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(Foo left, Foo right)
            {
                return !Equals(left, right);
            }
        }

        private async Task<IPersistentDictionary<Foo>> GetPersistentDictionary(string id)
        {
            var collection = await _fixture.GetDefaultCollection().ConfigureAwait(false);
            return new PersistentDictionary<Foo>(collection, id, new Mock<ILogger>().Object, new Mock<IRedactor>().Object);

        }

        [Fact(Skip = "Not working.  PersistentDictionary needs to be re-worked to not use sync-over-async.")]
        public void Test_Add()
        {
            var dict = GetPersistentDictionary("PersistentDictionaryTests.Test_Add").GetAwaiter().GetResult();
            dict.ClearAsync().GetAwaiter().GetResult();
            dict.Add("foo", new Foo {Name = "Tom", Age = 50});
            dict.Add("Dick", new Foo{Name = "Dick", Age = 27});
            dict.Add("Harry", new Foo{Name = "Harry", Age = 66});

            var exists = dict.ContainsKeyAsync("Dick").GetAwaiter().GetResult();
            //Assert.True(exists);
        }
    }
}
