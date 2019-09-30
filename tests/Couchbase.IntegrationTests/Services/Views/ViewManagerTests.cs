using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Views;
using Xunit;

namespace Couchbase.IntegrationTests.Services.Views
{
    public class ViewManagerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public ViewManagerTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Can_get_view()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default");
            var manager = bucket.ViewIndexes;

            var original = new DesignDocument
            {
                Name = "test",
                Views = new Dictionary<string, View>
                {
                    {
                        "by_name", new View
                        {
                            Map = "function (doc, meta) { emit(meta.id, null); }",
                            Reduce = "_count"
                        }
                    }
                }
            };

            try
            {
                // create
                await manager.CreateAsync(original);

                // upsert
                await manager.UpsertAsync(original);

                // get
                var designDoc = await manager.GetAsync(original.Name);
                Assert.Equal(original.Name, designDoc.Name);
                Assert.Single(original.Views);
                Assert.Equal(original.Views.First().Key, designDoc.Views.First().Key);
                Assert.Equal(original.Views.First().Value.Map, designDoc.Views.First().Value.Map);
                Assert.Equal(original.Views.First().Value.Reduce, designDoc.Views.First().Value.Reduce);

                // publish (& get again)
                await manager.PublishAsync(original.Name);
                designDoc = await manager.GetAsync(original.Name, options => options.WithIsProduction(true));
                Assert.Equal(original.Name, designDoc.Name);
                Assert.Single(original.Views);
                Assert.Equal(original.Views.First().Key, designDoc.Views.First().Key);
                Assert.Equal(original.Views.First().Value.Map, designDoc.Views.First().Value.Map);
                Assert.Equal(original.Views.First().Value.Reduce, designDoc.Views.First().Value.Reduce);

                // get all
                var getAllResult = await manager.GetAllAsync();
                Assert.Single(getAllResult);

                var ddoc = getAllResult.First();
                Assert.Equal(original.Name, ddoc.Name);
                Assert.Single(original.Views);
                Assert.Equal(original.Views.First().Key, ddoc.Views.First().Key);
                Assert.Equal(original.Views.First().Value.Map, ddoc.Views.First().Value.Map);
                Assert.Equal(original.Views.First().Value.Reduce, ddoc.Views.First().Value.Reduce);
            }
            finally
            {
                // drop
                await manager.DropAsync("dev_test", options => options.WithIsProduction(true));
            }
        }
    }
}
