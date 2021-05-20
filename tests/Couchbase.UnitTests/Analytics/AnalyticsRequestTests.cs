using System;
using Couchbase.Analytics;
using Newtonsoft.Json;
using Xunit;

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

            var json = JsonConvert.DeserializeObject<dynamic>(
                options.GetFormValuesAsJson(Statement)
            );
            Assert.Equal(Statement, json.statement.Value);

            // set statement using method
            const string statement = "SELECT 1 FROM `datset`;";

            formValues = options.GetFormValues(statement);
            Assert.Equal(statement, formValues["statement"]);

            json = JsonConvert.DeserializeObject<dynamic>(
                options.GetFormValuesAsJson(statement)
            );
            Assert.Equal(statement, json.statement.Value);
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

            var json = JsonConvert.DeserializeObject<dynamic>(
                options.GetFormValuesAsJson(statement)
            );
            Assert.Equal(expected, json.statement.Value);
        }

        [Fact]
        public void Default_timeout_is_75_seconds()
        {
            // sets default timeout to 75 seconds
            var options = new AnalyticsOptions().Timeout(TimeSpan.FromSeconds(75));

            var formValues = options.GetFormValues(Statement);
            Assert.Equal("75000ms", formValues["timeout"]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                options.GetFormValuesAsJson(Statement)
            );
            Assert.Equal("75000ms", json.timeout.Value);
        }

        [Fact]
        public void Can_set_timeout()
        {
            var options = new AnalyticsOptions().Timeout(TimeSpan.FromSeconds(15));

            var formValues = options.GetFormValues(Statement);
            Assert.Equal("15000ms", formValues["timeout"]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                options.GetFormValuesAsJson(Statement)
            );
            Assert.Equal("15000ms", json.timeout.Value);
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

            var json = JsonConvert.DeserializeObject<dynamic>(
                options.GetFormValuesAsJson(Statement)
            );
            Assert.Equal("value", json.my_string.Value);
            Assert.Equal(10, json.my_int.Value);
            Assert.Equal(true, json.my_bool.Value);
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

            var json = JsonConvert.DeserializeObject<dynamic>(
                options.GetFormValuesAsJson(Statement)
            );
            Assert.Equal("value", json.args[0].Value);
            Assert.Equal(10, json.args[1].Value);
            Assert.Equal(true, json.args[2].Value);
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

            //options.Priority(5); //remove?
            //Assert.Equal(5, options.PriorityValue);
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
    }
}
