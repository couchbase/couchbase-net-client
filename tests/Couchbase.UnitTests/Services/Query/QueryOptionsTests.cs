using System;
using Couchbase.Query;
using Couchbase.Query.Couchbase.N1QL;
using Xunit;

namespace Couchbase.UnitTests.Services.Query
{
    public class QueryOptionsTests
    {
        [Fact]
        public void Test_Encoding()
        {
            var options = new QueryOptions("SELECT * FROM `Default`").Encoding(Encoding.Utf8);
            var result = options.GetFormValues();

            Assert.Equal("UTF-8", result["encoding"]);
        }

        [Fact]
        public void Test_ClientContextId_Is_Guid()
        {
            var options = new QueryOptions("SELECT * FROM `Default`").ClientContextId(Guid.NewGuid().ToString());

            //will throw a FormatException if string is not a guid
            Guid.Parse(options.CurrentContextId);
        }
    }
}
