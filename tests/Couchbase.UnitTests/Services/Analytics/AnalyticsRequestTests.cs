using System;
using Couchbase.Analytics;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Services.Analytics
{
    public class AnalyticsRequestTests
    {
        private const string Statement = "SELECT 1;";

        [Fact]
        public void Request_auto_generates_context_and_request_Ids()
        {
            var request = new AnalyticsRequest(Statement);
            request.WithClientContextId(Guid.NewGuid().ToString());

            var formValues = request.GetFormValues();
            var requestId = formValues["client_context_id"].ToString();

            Assert.NotEmpty(requestId);
        }

        [Fact]
        public void Can_set_client_context_id()
        {
            var request = new AnalyticsRequest(Statement);
            request.WithClientContextId("testing");
            request.WithClientContextId(Guid.NewGuid().ToString());

            var formValues = request.GetFormValues();
            var contextId = formValues["client_context_id"].ToString();
            Assert.NotEmpty(contextId);

            Assert.True(Guid.TryParse(contextId, out Guid result));
        }

        [Fact]
        public void Request_ID_changes_on_each_request()
        {
            var request = new AnalyticsRequest(Statement);
            var formValues = request.GetFormValues();
            var clientContextId1 = formValues["client_context_id"].ToString();

            formValues = request.GetFormValues(); // re-trigger as if going to re-submited the query
            var clientContextId2 = formValues["client_context_id"].ToString();

            Assert.NotEqual(clientContextId1, clientContextId2);
        }

        [Fact]
        public void Can_set_statement()
        {
            // set statement using constructor
            var request = new AnalyticsRequest(Statement);

            var formValues = request.GetFormValues();
            Assert.Equal(Statement, formValues["statement"]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.Equal(Statement, json.statement.Value);

            // set statement using method
            const string statement = "SELECT 1 FROM `datset`;";
            request.WithStatement(statement);

            formValues = request.GetFormValues();
            Assert.Equal(statement, formValues["statement"]);

            json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
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
            var request = new AnalyticsRequest(statement);

            var formValues = request.GetFormValues();
            Assert.Equal(expected, formValues["statement"]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.Equal(expected, json.statement.Value);
        }

        [Fact]
        public void Default_timeout_is_75_seconds()
        {
            // sets default timeout to 75 seconds
            var request = new AnalyticsRequest(Statement) {Timeout = TimeSpan.FromSeconds(75)};

            var formValues = request.GetFormValues();
            Assert.Equal("75000ms", formValues["timeout"]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.Equal("75000ms", json.timeout.Value);
        }

        [Fact]
        public void Can_set_timeout()
        {
            var request = new AnalyticsRequest(Statement) {Timeout = TimeSpan.FromSeconds(15)};

            var formValues = request.GetFormValues();
            Assert.Equal("15000ms", formValues["timeout"]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.Equal("15000ms", json.timeout.Value);
        }

        [Fact]
        public void Can_add_named_parameter()
        {
            var request = new AnalyticsRequest(Statement);
            request.AddNamedParameter("my_string", "value");
            request.AddNamedParameter("my_int", 10);
            request.AddNamedParameter("my_bool", true);

            var formValues = request.GetFormValues();
            Assert.Equal("value", formValues["my_string"]);
            Assert.Equal(10, formValues["my_int"]);
            Assert.Equal(true, formValues["my_bool"]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.Equal("value", json.my_string.Value);
            Assert.Equal(10, json.my_int.Value);
            Assert.Equal(true, json.my_bool.Value);
        }

        [Fact]
        public void Can_add_positional_parameter()
        {
            var request = new AnalyticsRequest(Statement);
            request.AddPositionalParameter("value");
            request.AddPositionalParameter(10);
            request.AddPositionalParameter(true);

            var formValues = request.GetFormValues();
            var args = formValues["args"] as object[];
            Assert.NotNull(args);
            Assert.Equal("value", args[0]);
            Assert.Equal(10, args[1]);
            Assert.Equal(true, args[2]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.Equal("value", json.args[0].Value);
            Assert.Equal(10, json.args[1].Value);
            Assert.Equal(true, json.args[2].Value);
        }

        [Fact]
        public void Can_set_priority()
        {
            var request = new AnalyticsRequest(Statement);
            Assert.Equal(0, request.PriorityValue);

            request.Priority(true);
            Assert.Equal(-1, request.PriorityValue);

            request.Priority(false);
            Assert.Equal(0, request.PriorityValue);

            request.Priority(5);
            Assert.Equal(5, request.PriorityValue);
        }
    }
}
