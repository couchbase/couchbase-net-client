using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Couchbase.Core.Logging;
using Couchbase.DataStructures;
using Couchbase.IntegrationTests.Fixtures;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.IntegrationTests.DataStructures
{
    public class PersistentSetTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public PersistentSetTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            _outputHelper = outputHelper;
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
                return base.GetHashCode();
            }
        }

        private async Task<IPersistentSet<Foo>> GetPersistentSet([CallerMemberName] string id = "")
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            return new PersistentSet<Foo>(collection, $"{nameof(PersistentSetTests)}-{id}", new Mock<ILogger>().Object, new Mock<IRedactor>().Object);
        }

        [Fact]
        public async Task Test_AddAsync()
        {
            var set = await GetPersistentSet();
            await set.ClearAsync();
            await set.AddAsync(new Foo{Name = "Tom", Age = 50});
            await set.AddAsync(new Foo{Name = "Dick", Age = 27});

            Assert.Equal(2, await set.CountAsync);
        }

        [Fact]
        public async Task Test_Iteration()
        {
            var set = await GetPersistentSet();
            await set.ClearAsync();
            await set.AddAsync(new Foo{Name = "Tom", Age = 50});
            await set.AddAsync(new Foo{Name = "Dick", Age = 27});

            foreach (var item in set)
            {
                _outputHelper.WriteLine($"{item.Name}, {item.Age}");
            }
        }
    }
}
