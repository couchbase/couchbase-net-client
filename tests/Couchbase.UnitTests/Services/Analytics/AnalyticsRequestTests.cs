using System;
using System.Collections.Generic;
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

            var formValues = request.GetFormValues();
            var requestId = formValues["client_context_id"].ToString();
            Assert.NotEmpty(requestId);

            var parts = requestId.Split(new [] {"::"}, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, parts.Length);
            Assert.True(Guid.TryParse(parts[0], out _));
            Assert.True(Guid.TryParse(parts[1], out _));
        }

        [Fact]
        public void Can_set_client_context_id()
        {
            var request = new AnalyticsRequest(Statement);
            request.ClientContextId("testing");

            var formValues = request.GetFormValues();
            var requestId = formValues["client_context_id"].ToString();
            Assert.NotEmpty(requestId);

            var parts = requestId.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, parts.Length);
            Assert.Equal("testing", parts[0]);
            Assert.True(Guid.TryParse(parts[1], out _));
        }

        [Fact]
        public void Request_ID_changes_on_each_request()
        {
            var request = new AnalyticsRequest(Statement);

            var formValues = request.GetFormValues();
            var clientContextId = formValues["client_context_id"].ToString();

            var parts = clientContextId.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.True(Guid.TryParse(parts[0], out var clientContext1));
            Assert.True(Guid.TryParse(parts[1], out var requestId1));

            formValues = request.GetFormValues(); // re-trigger as if going to re-submited the query
            clientContextId = formValues["client_context_id"].ToString();

            parts = clientContextId.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.True(Guid.TryParse(parts[0], out var clientContext2));
            Assert.True(Guid.TryParse(parts[1], out var requestId2));

            Assert.Equal(clientContext1, clientContext2);
            Assert.NotEqual(requestId1, requestId2);
        }

        [Fact]
        public void Pretty_default_is_false()
        {
            var request = new AnalyticsRequest(Statement);

            var formValues = request.GetFormValues();
            Assert.Equal(false, formValues["pretty"]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.Equal(false, json.pretty.Value);
        }

        [Fact]
        public void Can_set_pretty_parameter()
        {
            var request = new AnalyticsRequest(Statement);
            request.Pretty(true);

            var formValues = request.GetFormValues();
            Assert.Equal(true, formValues["pretty"]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.Equal(true, json.pretty.Value);
        }

        [Fact]
        public void Metrics_default_is_false()
        {
            var request = new AnalyticsRequest(Statement);

            var formValues = request.GetFormValues();
            Assert.Equal(false, formValues["metrics"]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.Equal(false, json.pretty.Value);
        }

        [Fact]
        public void Can_set_metrics_parameter()
        {
            var request = new AnalyticsRequest(Statement);
            request.IncludeMetrics(true);

            var formValues = request.GetFormValues();
            Assert.Equal(true, formValues["metrics"]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.Equal(true, json.metrics.Value);
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
            request.Statement(statement);

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

        [Theory]
        [InlineData("user", false, "local:user")]
        [InlineData("local:user", false, "local:user")]
        [InlineData("user", true, "admin:user")]
        [InlineData("admin:user", true, "admin:user")]
        public void Can_add_credentials(string username, bool isAdmin, string expectedUser)
        {
            const string password = "password";
            var request = new AnalyticsRequest(Statement);

            var formValues = request.GetFormValues();
            Assert.False(formValues.ContainsKey("creds"));

            request.AddCredentials(username, password, isAdmin);
            formValues = request.GetFormValues();

            var creds = (List<dynamic>) formValues["creds"];
            Assert.Equal(1, creds.Count);

            var expected = $"{{ user = {expectedUser}, pass = {password} }}";
            foreach (var cred in creds)
            {
                Assert.Equal(expected, cred.ToString());
            }

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.Equal(1, json.creds.Count);
            Assert.Equal($"{{\"user\":\"{expectedUser}\",\"pass\":\"password\"}}", json.creds[0].ToString(Formatting.None));
        }

        [Fact]
        public void Default_timeout_is_75_seconds()
        {
            var request = new AnalyticsRequest(Statement);
            request.ConfigureLifespan(75); // sets default timeout to 75 seconds

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
            var request = new AnalyticsRequest(Statement);
            request.Timeout(TimeSpan.FromSeconds(15));
            request.ConfigureLifespan(75);

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

        [Fact]
        public void Deferred_default_is_false()
        {
            var request = new AnalyticsRequest(Statement);
            Assert.False(request.IsDeferred);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Can_set_deferred(bool deferred)
        {
            var request = new AnalyticsRequest(Statement)
                .Deferred(deferred);

            Assert.Equal(deferred, request.IsDeferred);

            var formValues = request.GetFormValues();
            if (deferred)
            {
                Assert.Equal("async", formValues["mode"]);
            }
            else
            {
                Assert.False(formValues.ContainsKey("mode"));
            }
        }
    }
}
