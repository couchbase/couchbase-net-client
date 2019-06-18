using System;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.TestData;
using Xunit;

namespace Couchbase.IntegrationTests
{
    public class GetTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public GetTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Can_get_document()
        {
            var collection = await _fixture.GetDefaultCollection();
            var key = Guid.NewGuid().ToString();

            try
            {
                await collection.InsertAsync(key, new {name = "mike"});

                using (var result = await collection.GetAsync(key))
                {
                    var content = result.ContentAs<dynamic>();

                    Assert.Equal("mike", (string) content.name);
                }
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [Fact]
        public async Task Can_Get_Projection()
        {
            var collection = await _fixture.GetDefaultCollection();
            var key = Guid.NewGuid().ToString();

            try
            {
                await collection.InsertAsync(key, Person.Create());

                using (var result = await collection.GetAsync(key, options => options.WithProjection("name")))
                {
                    var content = result.ContentAs<Person>();

                    Assert.Equal("Emmy-lou Dickerson", content.name);
                    Assert.Null(content.animals);
                }
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [Fact]
        public async Task Can_Get_Projections()
        {
            var collection = await _fixture.GetDefaultCollection();
            var key = Guid.NewGuid().ToString();

            try
            {
                await collection.InsertAsync(key, Person.Create());

                using (var result = await collection.GetAsync(key, options => options.WithProjection("name", "age")))
                {
                    var content = result.ContentAs<Person>();

                    Assert.Equal("Emmy-lou Dickerson", content.name);
                    Assert.Equal(26, content.age);
                }
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }
    }
}
