using System;
using System.Collections.Generic;
using Couchbase.Analytics;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Analytics
{
    [TestFixture]
    public class AnalyticsRequestTests
    {
        private readonly string _defaultstatement = "SELECT 1;";

        [Test]
        public void Request_auto_generates_context_and_request_Ids()
        {
            var request = new AnalyticsRequest(_defaultstatement);

            var formValues = request.GetFormValues();
            var requestId = formValues["client_context_id"].ToString();
            Assert.IsNotEmpty(requestId);

            var parts = requestId.Split(new [] {"::"}, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(2, parts.Length);
            Assert.True(Guid.TryParse(parts[0], out _));
            Assert.True(Guid.TryParse(parts[1], out _));
        }

        [Test]
        public void Can_set_client_context_id()
        {
            var request = new AnalyticsRequest(_defaultstatement);
            request.ClientContextId("testing");

            var formValues = request.GetFormValues();
            var requestId = formValues["client_context_id"].ToString();
            Assert.IsNotEmpty(requestId);

            var parts = requestId.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(2, parts.Length);
            Assert.AreEqual("testing", parts[0]);
            Assert.True(Guid.TryParse(parts[1], out _));
        }

        [Test]
        public void Request_ID_changes_on_each_request()
        {
            var request = new AnalyticsRequest(_defaultstatement);

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

            Assert.AreEqual(clientContext1, clientContext2);
            Assert.AreNotEqual(requestId1, requestId2);
        }

        [Test]
        public void Pretty_default_is_false()
        {
            var request = new AnalyticsRequest(_defaultstatement);

            var formValues = request.GetFormValues();
            Assert.AreEqual(false, formValues["pretty"]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.AreEqual(false, json.pretty.Value);
        }

        [Test]
        public void Can_set_pretty_parameter()
        {
            var request = new AnalyticsRequest(_defaultstatement);
            request.Pretty(true);

            var formValues = request.GetFormValues();
            Assert.AreEqual(true, formValues["pretty"]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.AreEqual(true, json.pretty.Value);
        }

        [Test]
        public void Metrics_default_is_false()
        {
            var request = new AnalyticsRequest(_defaultstatement);

            var formValues = request.GetFormValues();
            Assert.AreEqual(false, formValues["metrics"]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.AreEqual(false, json.pretty.Value);
        }

        [Test]
        public void Can_set_metrics_parameter()
        {
            var request = new AnalyticsRequest(_defaultstatement);
            request.IncludeMetrics(true);

            var formValues = request.GetFormValues();
            Assert.AreEqual(true, formValues["metrics"]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.AreEqual(true, json.metrics.Value);
        }

        [Test]
        public void Can_set_statement()
        {
            // set statement using constructor
            var request = new AnalyticsRequest(_defaultstatement);

            var formValues = request.GetFormValues();
            Assert.AreEqual(_defaultstatement, formValues["statement"]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.AreEqual(_defaultstatement, json.statement.Value);

            // set statement using method
            const string statement = "SELECT 1 FROM `datset`;";
            request.Statement(statement);

            formValues = request.GetFormValues();
            Assert.AreEqual(statement, formValues["statement"]);

            json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.AreEqual(statement, json.statement.Value);
        }

        [TestCase("  SELECT 1;  ")]
        [TestCase("SELECT 1;  ")]
        [TestCase("  SELECT 1;")]
        [TestCase("  SELECT 1  ")]
        [TestCase("SELECT 1  ")]
        [TestCase("  SELECT 1")]
        public void Statement_is_cleaned(string statement)
        {
            const string expected = "SELECT 1;";
            var request = new AnalyticsRequest(statement);

            var formValues = request.GetFormValues();
            Assert.AreEqual(expected, formValues["statement"]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.AreEqual(expected, json.statement.Value);
        }

        [TestCase("user", false, "local:user")]
        [TestCase("local:user", false, "local:user")]
        [TestCase("user", true, "admin:user")]
        [TestCase("admin:user", true, "admin:user")]
        public void Can_add_credentials(string username, bool isAdmin, string expectedUser)
        {
            const string password = "password";
            var request = new AnalyticsRequest(_defaultstatement);

            var formValues = request.GetFormValues();
            Assert.IsFalse(formValues.ContainsKey("creds"));

            request.AddCredentials(username, password, isAdmin);
            formValues = request.GetFormValues();

            var creds = (List<dynamic>) formValues["creds"];
            Assert.AreEqual(1, creds.Count);

            var expected = $"{{ user = {expectedUser}, pass = {password} }}";
            foreach (var cred in creds)
            {
                Assert.AreEqual(expected, cred.ToString());
            }

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.AreEqual(1, json.creds.Count);
            Assert.AreEqual($"{{\"user\":\"{expectedUser}\",\"pass\":\"password\"}}", json.creds[0].ToString(Formatting.None));
        }

        [Test]
        public void Default_timeout_is_75_seconds()
        {
            var request = new AnalyticsRequest(_defaultstatement);
            request.ConfigureLifespan(75); // sets default timeout to 75 seconds

            var formValues = request.GetFormValues();
            Assert.AreEqual("75000ms", formValues["timeout"]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.AreEqual("75000ms", json.timeout.Value);
        }

        [Test]
        public void Can_set_timeout()
        {
            var request = new AnalyticsRequest(_defaultstatement);
            request.Timeout(TimeSpan.FromSeconds(15));
            request.ConfigureLifespan(75);

            var formValues = request.GetFormValues();
            Assert.AreEqual("15000ms", formValues["timeout"]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.AreEqual("15000ms", json.timeout.Value);
        }

        [Test]
        public void Can_add_named_parameter()
        {
            var request = new AnalyticsRequest(_defaultstatement);
            request.AddNamedParamter("my_string", "value");
            request.AddNamedParamter("my_int", 10);
            request.AddNamedParamter("my_bool", true);

            var formValues = request.GetFormValues();
            Assert.AreEqual("value", formValues["my_string"]);
            Assert.AreEqual(10, formValues["my_int"]);
            Assert.AreEqual(true, formValues["my_bool"]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.AreEqual("value", json.my_string.Value);
            Assert.AreEqual(10, json.my_int.Value);
            Assert.AreEqual(true, json.my_bool.Value);
        }

        [Test]
        public void Can_add_positional_parameter()
        {
            var request = new AnalyticsRequest(_defaultstatement);
            request.AddPositionalParameter("value");
            request.AddPositionalParameter(10);
            request.AddPositionalParameter(true);

            var formValues = request.GetFormValues();
            var args = formValues["args"] as object[];
            Assert.NotNull(args);
            Assert.AreEqual("value", args[0]);
            Assert.AreEqual(10, args[1]);
            Assert.AreEqual(true, args[2]);

            var json = JsonConvert.DeserializeObject<dynamic>(
                request.GetFormValuesAsJson()
            );
            Assert.AreEqual("value", json.args[0].Value);
            Assert.AreEqual(10, json.args[1].Value);
            Assert.AreEqual(true, json.args[2].Value);
        }

        [TestCase(ExecutionMode.Immediate, null)]
        //[TestCase(ExecutionMode.Async, "async")]
        public void Can_set_execution_mode(ExecutionMode mode, string expected)
        {
            var request = new AnalyticsRequest(_defaultstatement);

            var formValues = request.GetFormValues();
            Assert.IsFalse(formValues.ContainsKey("mode"));

            request.ExecutionMode(mode);
            formValues = request.GetFormValues();

            if (mode == ExecutionMode.Immediate)
            {
                Assert.IsFalse(formValues.ContainsKey("mode"));
            }
            else
            {
                Assert.AreEqual(expected, formValues["mode"]);
            }
        }

        [Test]
        public void Can_set_priority()
        {
            var request = new AnalyticsRequest(_defaultstatement);
            Assert.AreEqual(0, request.PriorityValue);

            request.Priority(true);
            Assert.AreEqual(-1, request.PriorityValue);

            request.Priority(false);
            Assert.AreEqual(0, request.PriorityValue);

            request.Priority(5);
            Assert.AreEqual(5, request.PriorityValue);
        }
    }
}

