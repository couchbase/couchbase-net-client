using Couchbase.Services.Query;
using Xunit;

namespace Couchbase.UnitTests.Services.Query
{
    public class QueryParameterTests
    {
        [Fact]
        public void Test_NameParameters()
        {
            var parameters = new QueryParameter();

            parameters.Add("myname", "wee");
            parameters.Add("bar", "zod");

            Assert.Equal(2, parameters.NamedParameters.Count);
            Assert.Equal("wee", parameters.NamedParameters["myname"]);
            Assert.Equal("zod", parameters.NamedParameters["bar"]);
        }

        [Fact]
        public void Test_NameParameters2()
        {
            var parameters = new QueryParameter().Add("myname", "wee").Add("bar", "zod");

            Assert.Equal(2, parameters.NamedParameters.Count);
            Assert.Equal("wee", parameters.NamedParameters["myname"]);
            Assert.Equal("zod", parameters.NamedParameters["bar"]);
        }

        [Fact]
        public void Test_PositionalParameters()
        {
            var parameters = new QueryParameter();

            parameters.Add("wee");
            parameters.Add("zod");

            Assert.Equal(2, parameters.PostionalParameters.Count);
            Assert.Equal("wee", parameters.PostionalParameters[0]);
            Assert.Equal("zod", parameters.PostionalParameters[1]);
        }
    }
}