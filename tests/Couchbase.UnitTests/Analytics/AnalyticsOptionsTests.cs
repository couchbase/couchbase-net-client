using System;
using Couchbase.Analytics;
using System.Text.Json;
using Xunit;
using System.Collections.Generic;

namespace Couchbase.UnitTests.Analytics
{
    public class AnalyticsRequestTests
    {
        private const string Statement = "SELECT 1;";

        [Fact]
        public void Request_auto_generates_context_and_request_Ids()
        {
            var options = new AnalyticsOptions();
            options.ClientContextId(Guid.NewGuid().ToString());

            var formValues = options.GetFormValues(Statement);
            var requestId = formValues["client_context_id"].ToString();

            Assert.NotEmpty(requestId);
        }

        [Fact]
        public void Can_set_client_context_id()
        {
            var options = new AnalyticsOptions();
            options.ClientContextId("testing");
            options.ClientContextId(Guid.NewGuid().ToString());

            var formValues = options.GetFormValues(Statement);
            var contextId = formValues["client_context_id"].ToString();
            Assert.NotEmpty(contextId);

            Assert.True(Guid.TryParse(contextId, out Guid result));
        }

        [Fact]
        public void Request_ID_changes_on_each_request()
        {
            var options = new AnalyticsOptions();
            var formValues = options.GetFormValues(Statement);
            var clientContextId1 = formValues["client_context_id"].ToString();

            formValues = options.GetFormValues(Statement); // re-trigger as if going to re-submited the query
            var clientContextId2 = formValues["client_context_id"].ToString();

            Assert.NotEqual(clientContextId1, clientContextId2);
        }

        [Fact]
        public void Can_set_statement()
        {
            // set statement using constructor
            var options = new AnalyticsOptions();

            var formValues = options.GetFormValues(Statement);
            Assert.Equal(Statement, formValues["statement"]);

            var jsonDocument = JsonDocument.Parse(options.GetFormValuesAsJson(Statement));

            Assert.Equal(Statement, jsonDocument.RootElement.GetProperty("statement").GetString());

            // set statement using method
            const string statement = "SELECT 1 FROM `datset`;";

            formValues = options.GetFormValues(statement);
            Assert.Equal(statement, formValues["statement"]);

            var jsonDocument1 = JsonDocument.Parse(options.GetFormValuesAsJson(statement));
            Assert.Equal(statement, jsonDocument1.RootElement.GetProperty("statement").GetString());
        }

        [Theory]
        [InlineData("  SELECT 1;  ")]
        [InlineData("SELECT 1;  ")]
        [InlineData("  SELECT 1;")]
        [InlineData("  SELECT 1  ")]
        [InlineData("SELECT 1  ")]
        [InlineData("  SELECT 1")]
        public void Statement_is_cleaned(string statement)
        {
            const string expected = "SELECT 1;";
            var options = new AnalyticsOptions();

            var formValues = options.GetFormValues(statement);
            Assert.Equal(expected, formValues["statement"]);

            var jsonDocument1 = JsonDocument.Parse(options.GetFormValuesAsJson(statement));
            Assert.Equal(expected, jsonDocument1.RootElement.GetProperty("statement").GetString());
        }

        [Fact]
        public void Default_timeout_is_75_seconds()
        {
            // sets default timeout to 75 seconds
            var options = new AnalyticsOptions().Timeout(TimeSpan.FromSeconds(75));

            var formValues = options.GetFormValues(Statement);
            Assert.Equal("75000ms", formValues["timeout"]);

            var jsonDocument = JsonDocument.Parse(options.GetFormValuesAsJson(Statement));

            Assert.Equal("75000ms", jsonDocument.RootElement.GetProperty("timeout").GetString());
        }

        [Fact]
        public void Can_set_timeout()
        {
            var options = new AnalyticsOptions().Timeout(TimeSpan.FromSeconds(15));

            var formValues = options.GetFormValues(Statement);
            Assert.Equal("15000ms", formValues["timeout"]);

            var jsonDocument = JsonDocument.Parse(options.GetFormValuesAsJson(Statement));

            Assert.Equal("15000ms", jsonDocument.RootElement.GetProperty("timeout").GetString());
        }

        [Fact]
        public void Can_add_named_parameter()
        {
            var options = new AnalyticsOptions();
            options.Parameter("my_string", "value");
            options.Parameter("my_int", 10);
            options.Parameter("my_bool", true);

            var formValues = options.GetFormValues(Statement);
            Assert.Equal("value", formValues["my_string"]);
            Assert.Equal(10, formValues["my_int"]);
            Assert.Equal(true, formValues["my_bool"]);

            var jsonDocument = JsonDocument.Parse(options.GetFormValuesAsJson(Statement));

            Assert.Equal("value", jsonDocument.RootElement.GetProperty("my_string").ToString());
            Assert.Equal(10, jsonDocument.RootElement.GetProperty("my_int").GetInt32());
            Assert.True(jsonDocument.RootElement.GetProperty("my_bool").GetBoolean());
        }

        [Fact]
        public void Can_add_positional_parameter()
        {
            var options = new AnalyticsOptions();
            options.Parameter("value");
            options.Parameter(10);
            options.Parameter(true);

            var formValues = options.GetFormValues(Statement);
            var args = formValues["args"] as object[];
            Assert.NotNull(args);
            Assert.Equal("value", args[0]);
            Assert.Equal(10, args[1]);
            Assert.Equal(true, args[2]);

            var jsonDocument = JsonDocument.Parse(options.GetFormValuesAsJson(Statement));
            var array = jsonDocument.RootElement.GetProperty("args");

            Assert.Equal("value", array[0].GetString());
            Assert.Equal(10, array[1].GetInt32());
            Assert.True(array[2].GetBoolean());
        }

        [Fact]
        public void Can_set_priority()
        {
            var options = new AnalyticsOptions();
            Assert.Equal(0, options.PriorityValue);

            options.Priority(true);
            Assert.Equal(-1, options.PriorityValue);

            options.Priority(false);
            Assert.Equal(0, options.PriorityValue);
        }

        [Fact]
        public void Can_fetch_JSON_from_NamedParameters()
        {
            var options = new AnalyticsOptions();
            options.Parameter("theykey", "thevalue");

            var json = options.GetParametersAsJson();

            Assert.Equal("{\"theykey\":\"thevalue\"}", json);
        }

        [Fact]
        public void Can_fetch_JSON_from_PositionalParameters()
        {
            var options = new AnalyticsOptions();
            options.Parameter("thevalue");

            var json = options.GetParametersAsJson();

            Assert.Equal("[\"thevalue\"]", json);
        }

        [Fact]
        public void When_parameters_empty_GetParametersFromJson_returns_empty_object()
        {
            var options = new AnalyticsOptions();

            var json = options.GetParametersAsJson();

            Assert.Equal("{}", json);
        }

        [Fact]
        public void Test_GetFormValuesAsJson()
        {
            var options = new AnalyticsOptions()
            {
                BucketName = "default",
                ScopeName = "scope1",
                QueryContext = "queryctx",
            }
            .Parameter("par1")
            .Parameter("par2")
            .Parameter("namedpar1", "namedpar1val")
            .Parameter("namedpar2", "namedpar2val")
            .Raw("raw1", "val1")
            .Raw("raw2", "val2")
            .Readonly(true)
            .ClientContextId("cctxid")
            .Timeout(TimeSpan.FromSeconds(10))
            .ScanConsistency(AnalyticsScanConsistency.RequestPlus)
            .Priority(true);

            var json = options.GetFormValuesAsJson("SELECT * FROM default");
            var expected = "{\"statement\":\"SELECT * FROM default;\",\"timeout\":\"10000ms\",\"client_context_id\":\"cctxid\",\"query_context\":\"queryctx\",\"namedpar1\":\"namedpar1val\",\"namedpar2\":\"namedpar2val\",\"raw1\":\"val1\",\"raw2\":\"val2\",\"args\":[\"par1\",\"par2\"]}";
            Assert.Contains(expected, json);
        }
    }
}
