using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Couchbase.Core.Logging;
using Couchbase.DataStructures;
using Couchbase.IntegrationTests.Fixtures;
using Microsoft.Extensions.Logging;
using Moq;
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

        private async Task<IPersistentList<Foo>> GetPersistentList([CallerMemberName] string id = "")
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(true);
            return new PersistentList<Foo>(collection, $"{nameof(PersistentListTests)}-{id}", new Mock<ILogger>().Object, new Mock<IRedactor>().Object);
        }

        #region Synchronous

        [Fact]
        public async Task Test_GetEnumerator()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo{Name = "Tom", Age = 50});
            await collection.AddAsync(new Foo{Name = "Dick", Age = 27});
            await collection.AddAsync(new Foo{Name = "Harry", Age = 66});

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
        public async Task Test_Add()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();

            collection.Add(new Foo{Name = "Tom", Age = 50});

            var exists = await collection.ContainsAsync(new Foo {Name = "Tom", Age = 50});
            Assert.True(exists);
        }

        [Fact]
        public async Task Test_Clear()
        {
            var collection = await GetPersistentList();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });

            collection.Clear();

            var count = await collection.CountAsync;
            Assert.Equal(0, count);
        }

        [Fact]
        public async Task Test_Contains_True()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });

            var result = collection.Contains(new Foo { Name = "Tom", Age = 50 });

            Assert.True(result);
        }

        [Fact]
        public async Task Test_Contains_False()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });

            var result = collection.Contains(new Foo { Name = "Bob", Age = 51 });

            Assert.False(result);
        }

        [Fact]
        public async Task Test_CopyTo()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });
            await collection.AddAsync(new Foo { Name = "Dick", Age = 27 });
            await collection.AddAsync(new Foo { Name = "Harry", Age = 66 });

            var count = await collection.CountAsync;
            var list = new Foo[count];

            collection.CopyTo(list, 0);

            Assert.Equal(new Foo { Name = "Dick", Age = 27 }, list[1]);
        }

        [Fact]
        public async Task Test_Remove_IsPresent()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });
            Assert.Equal(1, await collection.CountAsync);

            var result = collection.Remove(new Foo { Name = "Tom", Age = 50 });

            Assert.Equal(0, await collection.CountAsync);
            Assert.True(result);
        }

        [Fact]
        public async Task Test_Remove_IsNotPresent()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });

            var result = collection.Remove(new Foo { Name = "Bob", Age = 51 });

            Assert.Equal(1, await collection.CountAsync);
            Assert.False(result);
        }

        [Fact]
        public async Task Test_IndexOf_Present()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });
            await collection.AddAsync(new Foo { Name = "Bob", Age = 51 });

            var result = collection.IndexOf(new Foo { Name = "Bob", Age = 51 });

            Assert.Equal(1, result);
        }

        [Fact]
        public async Task Test_IndexOf_NotPresent()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });

            var result = collection.IndexOf(new Foo { Name = "Bob", Age = 51 });

            Assert.Equal(-1, result);
        }

        [Fact]
        public async Task Test_Insert()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });
            await collection.AddAsync(new Foo { Name = "John", Age = 52 });

            collection.Insert(1, new Foo { Name = "Bob", Age = 51 });

            Assert.Equal(1, await collection.IndexOfAsync(new Foo { Name = "Bob", Age = 51 }));
            Assert.Equal(3, await collection.CountAsync);
        }

        [Fact]
        public async Task Test_RemoveAt()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });
            await collection.AddAsync(new Foo { Name = "Bob", Age = 51 });
            await collection.AddAsync(new Foo { Name = "John", Age = 52 });

            collection.RemoveAt(1);

            Assert.Equal(1, await collection.IndexOfAsync(new Foo { Name = "John", Age = 52 }));
            Assert.Equal(2, await collection.CountAsync);
        }

        [Fact]
        public async Task Test_IndexerGet_InRange()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });
            await collection.AddAsync(new Foo { Name = "Bob", Age = 51 });
            await collection.AddAsync(new Foo { Name = "John", Age = 52 });

            var result = collection[1];

            Assert.Equal(new Foo { Name = "Bob", Age = 51 }, result);
        }

        [Fact]
        public async Task Test_IndexerGet_OutOfRange()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });
            await collection.AddAsync(new Foo { Name = "Bob", Age = 51 });
            await collection.AddAsync(new Foo { Name = "John", Age = 52 });

            Assert.Throws<ArgumentOutOfRangeException>(() => collection[3]);
        }

        [Fact]
        public async Task Test_IndexerSet_InRange()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });
            await collection.AddAsync(new Foo { Name = "John", Age = 52 });

            collection[1] = new Foo { Name = "Bob", Age = 51 };

            Assert.Equal(new Foo { Name = "Bob", Age = 51 }, collection[1]);
            Assert.Equal(2, await collection.CountAsync);
        }

        [Fact]
        public async Task Test_IndexerSet_OutOfRange()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });
            await collection.AddAsync(new Foo { Name = "Bob", Age = 51 });
            await collection.AddAsync(new Foo { Name = "John", Age = 52 });

            Assert.Throws<ArgumentOutOfRangeException>(() => collection[3] = new Foo());
        }

        #endregion

        #region Asynchronous

        [Fact]
        public async Task Test_AddAsync()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();

            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });

            var exists = await collection.ContainsAsync(new Foo { Name = "Tom", Age = 50 });
            Assert.True(exists);
        }

        [Fact]
        public async Task Test_ClearAsync()
        {
            var collection = await GetPersistentList();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });

            await collection.ClearAsync();

            var count = await collection.CountAsync;
            Assert.Equal(0, count);
        }

        [Fact]
        public async Task Test_ContainsAsync_True()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });

            var result = await collection.ContainsAsync(new Foo { Name = "Tom", Age = 50 });

            Assert.True(result);
        }

        [Fact]
        public async Task Test_ContainsAsync_False()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });

            var result = await collection.ContainsAsync(new Foo { Name = "Bob", Age = 51 });

            Assert.False(result);
        }

        [Fact]
        public async Task Test_CopyToAsync()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });
            await collection.AddAsync(new Foo { Name = "Dick", Age = 27 });
            await collection.AddAsync(new Foo { Name = "Harry", Age = 66 });

            var count = await collection.CountAsync;
            var list = new Foo[count];

            await collection.CopyToAsync(list, 0);

            Assert.Equal(new Foo { Name = "Dick", Age = 27 }, list[1]);
        }

        [Fact]
        public async Task Test_RemoveAsync_IsPresent()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });
            Assert.Equal(1, await collection.CountAsync);

            var result = await collection.RemoveAsync(new Foo { Name = "Tom", Age = 50 });

            Assert.Equal(0, await collection.CountAsync);
            Assert.True(result);
        }

        [Fact]
        public async Task Test_RemoveAsync_IsNotPresent()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });

            var result = await collection.RemoveAsync(new Foo { Name = "Bob", Age = 51 });

            Assert.Equal(1, await collection.CountAsync);
            Assert.False(result);
        }

        [Fact]
        public async Task Test_IndexOfAsync_Present()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });
            await collection.AddAsync(new Foo { Name = "Bob", Age = 51 });

            var result = await collection.IndexOfAsync(new Foo { Name = "Bob", Age = 51 });

            Assert.Equal(1, result);
        }

        [Fact]
        public async Task Test_IndexOfAsync_NotPresent()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });

            var result = await collection.IndexOfAsync(new Foo { Name = "Bob", Age = 51 });

            Assert.Equal(-1, result);
        }

        [Fact]
        public async Task Test_InsertAsync()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });
            await collection.AddAsync(new Foo { Name = "John", Age = 52 });

            await collection.InsertAsync(1, new Foo { Name = "Bob", Age = 51 });

            Assert.Equal(1, await collection.IndexOfAsync(new Foo { Name = "Bob", Age = 51 }));
            Assert.Equal(3, await collection.CountAsync);
        }

        [Fact]
        public async Task Test_RemoveAtAsync()
        {
            var collection = await GetPersistentList();
            await collection.ClearAsync();
            await collection.AddAsync(new Foo { Name = "Tom", Age = 50 });
            await collection.AddAsync(new Foo { Name = "Bob", Age = 51 });
            await collection.AddAsync(new Foo { Name = "John", Age = 52 });

            await collection.RemoveAtAsync(1);

            Assert.Equal(1, await collection.IndexOfAsync(new Foo { Name = "John", Age = 52 }));
            Assert.Equal(2, await collection.CountAsync);
        }

        #endregion
    }
}
