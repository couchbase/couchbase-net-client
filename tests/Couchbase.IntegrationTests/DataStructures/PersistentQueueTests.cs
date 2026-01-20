using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Logging;
using Couchbase.DataStructures;
using Couchbase.IntegrationTests.Fixtures;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.IntegrationTests.DataStructures
{
    public class PersistentQueueTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        public PersistentQueueTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        public class Foo
        {
            public string Name { get; set; }

            public int Age { get; set; }
        }

        private async Task<IPersistentQueue<Foo>> GetPersistentQueue([CallerMemberName] string id = "")
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            return new PersistentQueue<Foo>(collection, $"{nameof(PersistentQueueTests)}-{id}", new QueueOptions(), new Mock<ILogger>().Object, new Mock<IRedactor>().Object);
        }

        [Fact]
        public async Task Test_DequeueAsync()
        {
            var queue = await GetPersistentQueue();
            await queue.ClearAsync();
            await queue.EnqueueAsync(new Foo{Name = "Tom", Age = 50});
            await queue.EnqueueAsync(new Foo{Name = "Dick", Age = 27});
            await queue.EnqueueAsync(new Foo{Name = "Harry", Age = 66});

            var item = await queue.DequeueAsync();
            Assert.Equal("Tom", item?.Name);

            var count = await queue.CountAsync;
            Assert.Equal(2, count);
        }


        [Fact]
        public async Task Test_PeekAsync()
        {
            var queue = await GetPersistentQueue();
            await queue.ClearAsync();
            await queue.EnqueueAsync(new Foo{Name = "Tom", Age = 50});
            await queue.EnqueueAsync(new Foo{Name = "Dick", Age = 27});
            await queue.EnqueueAsync(new Foo{Name = "Harry", Age = 66});

            var item = await queue.PeekAsync();
            Assert.Equal("Tom", item?.Name);

            var count = await queue.CountAsync;
            Assert.Equal(3, count);
        }
    }
}
