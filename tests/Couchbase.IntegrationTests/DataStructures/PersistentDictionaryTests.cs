using System;
using System.Collections.Generic;
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

        private async Task<IPersistentDictionary<Foo>> GetPersistentDictionary([CallerMemberName] string id = "")
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            return new PersistentDictionary<Foo>(collection, id, new Mock<ILogger>().Object, new Mock<IRedactor>().Object);
        }

        [Fact]
        public async Task Test_AddAsync()
        {
            var dict = await GetPersistentDictionary();
            await dict.ClearAsync();
            await dict.AddAsync("foo", new Foo {Name = "Tom", Age = 50});
            await dict.AddAsync("Dick", new Foo{Name = "Dick", Age = 27});
            await dict.AddAsync("Harry", new Foo{Name = "Harry", Age = 66});

            var exists = await dict.ContainsKeyAsync("Dick");
            Assert.True(exists);
        }

        [Fact]
        public async Task Test_SetAsync()
        {
            var dict = await GetPersistentDictionary();
            await dict.ClearAsync();
            await dict.SetAsync("foo", new Foo { Name = "Tom", Age = 50 });
            await dict.SetAsync("foo", new Foo { Name = "Dick", Age = 27 });

            var value = await dict.GetAsync("foo");

            Assert.Equal("Dick", value.Name);
        }

        [Fact]
        public async Task Test_TryGetValue_Exists()
        {
            var dict = await GetPersistentDictionary();
            await dict.ClearAsync();
            await dict.AddAsync("foo", new Foo { Name = "Tom", Age = 50 });

            var result = dict.TryGetValue("foo", out var value);

            Assert.True(result);
            Assert.Equal("Tom", value.Name);
        }

        [Fact]
        public async Task Test_TryGetValue_Missing()
        {
            var dict = await GetPersistentDictionary();
            await dict.ClearAsync();
            await dict.AddAsync("foo", new Foo { Name = "Tom", Age = 50 });

            var result = dict.TryGetValue("bar", out _);

            Assert.False(result);
        }
    }
}
