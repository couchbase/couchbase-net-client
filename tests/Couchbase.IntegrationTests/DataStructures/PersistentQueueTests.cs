using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
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

        private async Task<IPersistentQueue<Foo>> GetPersistentList(string id)
        {
            var collection = await _fixture.GetDefaultCollection();
            return new PersistentQueue<Foo>(collection, id, new Mock<ILogger>().Object);
        }

        [Fact]
        public void Test_DequeueAsync()
        {
            var queue = GetPersistentList("Test_DequeueAsync()").GetAwaiter().GetResult();
            queue.ClearAsync().GetAwaiter().GetResult();
            queue.EnqueueAsync(new Foo{Name = "Tom", Age = 50}).GetAwaiter().GetResult();
            queue.EnqueueAsync(new Foo{Name = "Dick", Age = 27}).GetAwaiter().GetResult();
            queue.EnqueueAsync(new Foo{Name = "Harry", Age = 66}).GetAwaiter().GetResult();

            var item = queue.DequeueAsync().GetAwaiter().GetResult();
            Assert.Equal("Tom", item.Name);

            var count = queue.Count;
            Assert.Equal(2, count);
        }

        
        [Fact]
        public void Test_PeakAsync()
        {
            var queue = GetPersistentList("Test_PeakAsync()").GetAwaiter().GetResult();
            queue.ClearAsync().GetAwaiter().GetResult();
            queue.EnqueueAsync(new Foo{Name = "Tom", Age = 50}).GetAwaiter().GetResult();
            queue.EnqueueAsync(new Foo{Name = "Dick", Age = 27}).GetAwaiter().GetResult();
            queue.EnqueueAsync(new Foo{Name = "Harry", Age = 66}).GetAwaiter().GetResult();

            var item = queue.PeekAsync().GetAwaiter().GetResult();
            Assert.Equal("Tom", item.Name);

            var count = queue.Count;
            Assert.Equal(3, count);
        }
    }
}
