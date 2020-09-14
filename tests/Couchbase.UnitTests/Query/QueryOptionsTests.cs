using System;
using System.Collections.Generic;
using Couchbase.Query;
using Couchbase.Query.Couchbase.N1QL;
using Xunit;

namespace Couchbase.UnitTests.Query
{
    public class QueryOptionsTests
    {
        [Fact]
        public void Test_ClientContextId_Is_Guid()
        {
            var options = new QueryOptions("SELECT * FROM `Default`").ClientContextId(Guid.NewGuid().ToString());

            //will throw a FormatException if string is not a guid
            Guid.Parse(options.CurrentContextId);
        }

        [Fact]
        public void Test_Query_With_PositionParameters()
        {
            var options = new QueryOptions("SELECT * FROM `$1` WHERE name=$2").
                Parameter("default").
                Parameter("bill");

            var values = options.GetFormValues();
            var args = (List<object>)values["args"];

            Assert.Equal("default", args[0]);
            Assert.Equal("bill", args[1]);
        }

        [Fact]
        public void Test_Query_With_NamedParameters()
        {
            var options = new QueryOptions("SELECT * FROM `$bucket` WHERE name=$name").
                Parameter("bucket","default").
                Parameter("name","bill");

            var values = options.GetFormValues();
            Assert.Equal("default", values["$bucket"]);
            Assert.Equal("bill", values["$name"]);
        }

        [Fact]
        public void Test_QueryContext_Is_NotNull()
        {
            var options = new QueryOptions("SELECT * FROM WHAT") {QueryContext = "namespace:bucket:scope:collection"};
            var args = options.GetFormValues();

            Assert.Equal("namespace:bucket:scope:collection", args["query_context"]);
        }

        [Fact]
        public void Test_FlexIndex_When_True_Sends_Parameter()
        {
            var options =  new QueryOptions("SELECT * FROM WHAT").FlexIndex(true);

            var values = options.GetFormValues();
            Assert.Equal(true, actual: values["use_fts"]);
        }

        [Fact]
        public void Test_FlexIndex_When_False_Sends_Nothing()
        {
            var options = new QueryOptions("SELECT * FROM WHAT").FlexIndex(false);

            var values = options.GetFormValues();
            Assert.False(values.Keys.Contains("use_fts"));
        }

        [Fact]
        public void Test_FlexIndex_When_Default_Sends_Nothing()
        {
            var options = new QueryOptions("SELECT * FROM WHAT").FlexIndex(false);

            var values = options.GetFormValues();
            Assert.False(values.Keys.Contains("use_fts"));
        }
    }
}
