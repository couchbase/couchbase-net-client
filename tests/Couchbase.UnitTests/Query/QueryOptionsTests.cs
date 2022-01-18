using System;
using System.Collections.Generic;
using System.Threading;
using Couchbase.Core;
using Couchbase.KeyValue;
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

        [Fact]
        public void Test_CloneIdUsedAlready()
        {
            var cts = new CancellationTokenSource();
            var mutationState = new MutationState();
            mutationState.Add(new MutationResult(0, TimeSpan.FromSeconds(10), new MutationToken("default", 1, 1, 1)));
            var options = new QueryOptions().
                AdHoc(true).AutoExecute(true).
                CancellationToken(cts.Token).
                ClientContextId("clientid").
                ConsistentWith(mutationState).
                FlexIndex(true).
                MaxServerParallelism(1).
                Metrics(true).
                Parameter(1).
                Parameter("name", "value").
                PipelineBatch(1).
                PipelineCap(1).
                Profile(QueryProfile.Off).
                Raw("foo", "bar").
                Readonly(true).
                ScanCap(1).
                ScanWait(TimeSpan.FromSeconds(10)).
                Timeout(TimeSpan.FromMilliseconds(1)).
                Statement("SELECT 1;").
                ScanCap(1);

            var newOptions = options.CloneIfUsedAlready();
            var newValues = newOptions.GetFormValues();
            var oldValues = options.GetFormValues();

            Assert.Equal(newValues.Count, oldValues.Count);
            Assert.Equal(newValues["max_parallelism"], oldValues["max_parallelism"]);
            Assert.Equal(newValues["statement"], oldValues["statement"]);
            Assert.Equal(newValues["timeout"], oldValues["timeout"]);
            Assert.Equal(newValues["readonly"], oldValues["readonly"]);
            Assert.Equal(newValues["metrics"], oldValues["metrics"]);
            Assert.Equal(newValues["$name"], oldValues["$name"]);
            Assert.Equal(newValues["args"], oldValues["args"]);
            Assert.Equal(newValues["scan_consistency"], oldValues["scan_consistency"]);
            Assert.Equal(newValues["scan_vectors"], oldValues["scan_vectors"]);
            Assert.Equal(newValues["scan_wait"], oldValues["scan_wait"]);
            Assert.Equal(newValues["scan_cap"], oldValues["scan_cap"]);
            Assert.Equal(newValues["pipeline_batch"], oldValues["pipeline_batch"]);
            Assert.Equal(newValues["pipeline_cap"], oldValues["pipeline_cap"]);
            Assert.Equal(newValues["foo"], oldValues["foo"]);
            Assert.Equal(newValues["auto_execute"], oldValues["auto_execute"]);
            Assert.Equal(newValues["client_context_id"], oldValues["client_context_id"]);
            Assert.Equal(newValues["use_fts"], oldValues["use_fts"]);
        }
    }
}
