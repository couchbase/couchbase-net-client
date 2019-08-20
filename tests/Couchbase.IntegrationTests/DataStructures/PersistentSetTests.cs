using System;
using System.Threading.Tasks;
using Couchbase.DataStructures;
using Couchbase.IntegrationTests.Fixtures;
using Xunit;

namespace Couchbase.IntegrationTests.DataStructures
{
    public class PersistentSetTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public PersistentSetTests(ClusterFixture fixture)
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
        }

        private async Task<IPersistentSet<Foo>> GetPersistentSet(string id)
        {
            var collection = await _fixture.GetDefaultCollection();
            return new PersistentSet<Foo>(collection, id);
        }

        [Fact]
        public void Test_Add()
        {
            var set = GetPersistentSet("PersistentSetTests.Test_Add()").GetAwaiter().GetResult();
            set.ClearAsync().GetAwaiter().GetResult();
            set.AddAsync(new Foo{Name = "Tom", Age = 50}).GetAwaiter().GetResult();
            set.AddAsync(new Foo{Name = "Tom", Age = 50}).GetAwaiter().GetResult();
        }
    }
}
