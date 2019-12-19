using System;
using System.Threading.Tasks;
using Couchbase.DataStructures;
using Couchbase.IntegrationTests.Fixtures;
using Xunit;

namespace Couchbase.IntegrationTests.DataStructures
{
    public class PersistentListTests: IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        public PersistentListTests(ClusterFixture fixture)
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

        private async Task<IPersistentList<Foo>> GetPersistentList(string id)
        {
            var collection = await _fixture.GetDefaultCollection();
            return new PersistentList<Foo>(collection, id);
        }

        [Fact]
        public void Test_GetEnumerator()
        {
            var collection = GetPersistentList("Test_GetEnumerator()").GetAwaiter().GetResult();
            collection.ClearAsync().GetAwaiter().GetResult();
            collection.Add(new Foo{Name = "Tom", Age = 50});
            collection.Add(new Foo{Name = "Dick", Age = 27});
            collection.Add(new Foo{Name = "Harry", Age = 66});

            var count = 0;
            using (var enumerator = collection.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    count++;
                }
            }
            Assert.Equal(3, count);
        }

        [Fact]
        public void Test_CopyTo_Array()
        {
            var collection = GetPersistentList("Test_CopyTo_Array()").GetAwaiter().GetResult();
            collection.ClearAsync().GetAwaiter().GetResult();
            collection.Add(new Foo{Name = "Tom", Age = 50});
            collection.Add(new Foo{Name = "Dick", Age = 27});
            collection.Add(new Foo{Name = "Harry", Age = 66});

            var count = collection.CountAsync.GetAwaiter().GetResult();
            var list = new Foo[count];

            collection.CopyToAsync(list, 0);
            var actual = collection.CountAsync.GetAwaiter().GetResult();
            Assert.Equal(list.Length, actual);
        }

        [Fact]
        public void Test_Add()
        {
            var collection = GetPersistentList("Test_CopyTo_Array()").GetAwaiter().GetResult();
            collection.ClearAsync().GetAwaiter().GetResult();
            collection.Add(new Foo{Name = "Tom", Age = 50});

            var exists = collection.ContainsAsync(new Foo {Name = "Tom", Age = 50}).GetAwaiter().GetResult();
            Assert.True(exists);
        }


        [Fact(Skip = "Not Implemented")]
        public void Test_Clear()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "Not Implemented")]
        public void Test_Contains()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "Not Implemented")]
        public void Test_CopyTo()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "Not Implemented")]
        public void Test_Remove()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "Not Implemented")]
        public void Test_IndexOf()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "Not Implemented")]
        public void Test_Insert()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "Not Implemented")]
        public void Test_RemoveAt()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "Not Implemented")]
        public void Test_Indexer()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "Not Implemented")]
        public void Test_CopyToAsync()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "Not Implemented")]
        public void Test_AddAsync()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "Not Implemented")]
        public void Test_ClearAsync()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "Not Implemented")]
        public void Test_ContainsAsync()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "Not Implemented")]
        public Task Test_CopyToAsync_Array()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "Not Implemented")]
        public Task Test_RemoveAsync()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "Not Implemented")]
        public Task Test_CountAsync()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "Not Implemented")]
        public Task Test_IndexOfAsync()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "Not Implemented")]
        public Task Test_InsertAsync()
        {
            throw new NotImplementedException();
        }

        [Fact(Skip = "Not Implemented")]
        public Task Test_RemoveAtAsync()
        {
            throw new NotImplementedException();
        }
    }
}
