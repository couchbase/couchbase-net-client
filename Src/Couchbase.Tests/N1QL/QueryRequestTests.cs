using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.N1QL;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Collections;
using System.Collections.ObjectModel;
using System.Runtime.Remoting.Proxies;
using NUnit.Framework.Constraints;
using System.Xml;

namespace Couchbase.Tests.N1QL
{
    [TestFixture]
    public class QueryRequestTests
    {

        private readonly string _server = ConfigurationManager.AppSettings["serverIp"];

        [Test]
        public void Test_Statement()
        {
            var query = new QueryRequest().
                BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                Statement("SELECT * FROM default");

            var json = query.GetFormValuesAsJson();
            var expected = "{\"statement\":\"SELECT * FROM default\",\"timeout\":\"75000ms\"}";
            Console.WriteLine(json);
            Assert.AreEqual(expected, json);
        }

        [Test]
        public void Test_Statement_ClientContextId()
        {
            var query = new QueryRequest().
                BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                Statement("SELECT * FROM default").
                ClientContextId("somecontextlessthanorequalto64chars");

            var json = query.GetFormValuesAsJson();
            var expected = "{\"statement\":\"SELECT * FROM default\",\"timeout\":\"75000ms\",\"client_context_id\":\"somecontextlessthanorequalto64chars\"}";
            Console.WriteLine(json);
            Assert.AreEqual(expected, json);
        }

        [Test]
        public void Test_Statement_ClientContextId_Pretty()
        {
            var query = new QueryRequest().
                BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                Statement("SELECT * FROM default").
                ClientContextId("somecontextlessthanorequalto64chars").
                Pretty(true);

            var json = query.GetFormValuesAsJson();
            var expected = "{\"statement\":\"SELECT * FROM default\",\"timeout\":\"75000ms\",\"pretty\":true,\"client_context_id\":\"somecontextlessthanorequalto64chars\"}";
            Console.WriteLine(json);
            Assert.AreEqual(expected, json);
        }

        [Test]
        public void Test_Positional_Parameters()
        {
            var query = new QueryRequest().
                BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                Statement("SELECT * FROM default WHERE type=$1").
                AddPositionalParameter("dog");

            var json = query.GetFormValuesAsJson();
            var expected = "{\"statement\":\"SELECT * FROM default WHERE type=$1\",\"timeout\":\"75000ms\",\"args\":[\"dog\"]}";
            Console.WriteLine(json);
            Assert.AreEqual(expected, json);
        }

        [Test]
        public void Test_Positional_Parameters_Two_Arguments()
        {
            var query = new QueryRequest().
                BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                Statement("SELECT * FROM default WHERE type=$1 OR type=$2").
                AddPositionalParameter("dog").
                AddPositionalParameter("cat");

            var json = query.GetFormValuesAsJson();
            var expected = "{\"statement\":\"SELECT * FROM default " +
                "WHERE type=$1 OR type=$2\",\"timeout\":\"75000ms\",\"args\":[\"dog\",\"cat\"]}";
            Console.WriteLine(json);
            Assert.AreEqual(expected, json);
        }

        [Test]
        public void Test_Named_Parameters_Two_Arguments()
        {
            var query = new QueryRequest().
                BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                Statement("SELECT * FROM default WHERE type=$canine OR type=$feline").
                AddNamedParameter("canine", "dog").
                AddNamedParameter("feline", "cat");

            var json = query.GetFormValuesAsJson();
            var expected = "{\"statement\":\"SELECT * FROM default WHERE type=$canine OR type=$feline\"," +
                "\"timeout\":\"75000ms\",\"$canine\":\"dog\",\"$feline\":\"cat\"}";
            Console.WriteLine(json);
            Assert.AreEqual(expected, json);
        }

        [Test]
        public void When_isAdmin_Is_True_Credentials_Contains_admin()
        {
            var query = new QueryRequest().
                BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                Statement("SELECT * FROM authenticated").
                AddCredentials("authenticated", "secret", true);

            var json = query.GetFormValuesAsJson();
            var expected = "{\"statement\":\"SELECT * FROM authenticated\",\"timeout\":\"75000ms\",\"creds\":[{\"user\":\"admin:authenticated\",\"pass\":\"secret\"}]}";
            Console.WriteLine(json);
            Assert.AreEqual(expected, json);
        }

        [Test]
        public void When_isAdmin_Is_False_Credentials_Contains_local()
        {
            var query = new QueryRequest().
                BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                Statement("SELECT * FROM authenticated").
                AddCredentials("authenticated", "secret", false);

            var json = query.GetFormValuesAsJson();
            var expected = "{\"statement\":\"SELECT * FROM authenticated\",\"timeout\":\"75000ms\"," +
                           "\"creds\":[{\"user\":\"local:authenticated\",\"pass\":\"secret\"}]}";
            Console.WriteLine(json);
            Assert.AreEqual(expected, json);
        }

        [Test]
        public void When_Username_Is_Empty_AddCredentials_Throws_AOOE()
        {
           var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new QueryRequest().
                BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                Statement("SELECT * FROM authenticated").
                AddCredentials("", "secret", false));

           Assert.That(ex.Message, Is.EqualTo("cannot be null, empty or whitespace.\r\nParameter name: username"));
        }

        [Test]
        public void When_Username_Is_Whitespace_AddCredentials_Throws_AOOE()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new QueryRequest().
                 BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                 Statement("SELECT * FROM authenticated").
                 AddCredentials(" ", "secret", false));

            Assert.That(ex.Message, Is.EqualTo("cannot be null, empty or whitespace.\r\nParameter name: username"));
        }

        [Test]
        public void When_Username_Is_Null_AddCredentials_Throws_AOOE()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new QueryRequest().
                 BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                 Statement("SELECT * FROM authenticated").
                 AddCredentials(null, "secret", false));

            Assert.That(ex.Message, Is.EqualTo("cannot be null, empty or whitespace.\r\nParameter name: username"));
        }

        private IQueryRequest CreateFullQueryRequest()
        {
            return new QueryRequest()
                .BaseUri(new Uri(string.Format("http://{0}:8093/query", _server)))
                .Metrics(true)
                .Statement("SELECT * from Who WHERE $1")
                .Pretty(true)
                .ReadOnly(false)
                .ScanConsistency(ScanConsistency.RequestPlus)
                .ScanVector("100") //TODO JSON object for sparse or array for full
                .ScanWait(new TimeSpan(0, 0, 0, 0, 100))
                .Signature(true)
                .Timeout(new TimeSpan(0, 0, 0, 0, 10000))
                .Compression(Compression.RLE)
                .AddCredentials("authenticated", "secret", false)
                .AddPositionalParameter("boo");
        }

        [Test]
        public void When_ScanConsistency_AtPlus_Provided_NotSupportedException_Is_Thrown()
        {
            var query = new QueryRequest();
            Assert.Throws<NotSupportedException>(()=>query.ScanConsistency(ScanConsistency.AtPlus));
        }

        [Test]
        public void When_ScanConsistency_StatementPlus_Provided_NotSupportedException_Is_Thrown()
        {
            var query = new QueryRequest();
            Assert.Throws<NotSupportedException>(() => query.ScanConsistency(ScanConsistency.StatementPlus));
        }

        [Test]
        public void Test_Prepared_Doesnt_Use_Plan_Text_As_Statement()
        {
            var request = CreateFullQueryRequest();
            request.Prepared(new QueryPlan()
            {
                Operator = "operator",
                EncodedPlan = "encoded",
                Name = "name",
                Text = "modifiedStatement"
            }, "originalStatement");
            var values = request.GetFormValues();

            Assert.AreEqual("originalStatement", request.GetOriginalStatement());
            Assert.AreEqual("name", values["prepared"]);
            Assert.AreEqual("encoded", values["encoded_plan"]);
            Assert.False(request.GetFormValuesAsJson().Contains("modifiedStatement"));
        }

        [Test]
        public void Test_Using_Prepared_Is_Detected()
        {
            var request = CreateFullQueryRequest();
            request.Prepared(new QueryPlan
            {
                Operator = "{ \"test\": \"yes\" }",
                EncodedPlan = "{ \"encoded\": 1 }",
                Name = "name",
                Text = "PREPARE original"
            }, "original");

            var values = request.GetFormValues();
            Assert.AreEqual("{ \"encoded\": 1 }", values["encoded_plan"]);
            Assert.AreEqual("name", values["prepared"]);
            try
            {
                var statement = values["statement"];
                Assert.Fail("statement should not be present, was " + statement);
            }
            catch (KeyNotFoundException e)
            {
                //expected
            }
        }

        [Test]
        public void Test_GetFormValues()
        {
            var request = CreateFullQueryRequest();

            var values = request.GetFormValues();
            Assert.AreEqual(true, values["metrics"]);
            Assert.AreEqual("SELECT * from Who WHERE $1", values["statement"]);
            Assert.AreEqual(true, values["pretty"]);
            Assert.AreEqual(false, values["readonly"]);
            Assert.AreEqual("request_plus", values["scan_consistency"]);
            Assert.AreEqual("100", values["scan_vector"]);
            Assert.AreEqual("100", values["scan_wait"]);
            Assert.AreEqual(true, values["signature"]);
            Assert.AreEqual("RLE", values["compression"]);
            Assert.AreEqual("10000ms", values["timeout"]);

            //more complex ones
            var expectedCred = "{ user = local:authenticated, pass = secret }";
            Assert.IsInstanceOf<ICollection>(values["creds"]);
            var creds = (ICollection<dynamic>) values["creds"];
            Assert.AreEqual(1, creds.Count);
            Assert.AreEqual(expectedCred, creds.First().ToString());
//            Assert.AreEqual(new { user = "admin", pass = "toto"}.ToString(), creds.First().ToString());

            var expectedArgs = new List<object>();
            expectedArgs.Add("boo");
            Assert.IsInstanceOf<ICollection>(values["args"]);
            CollectionAssert.AreEqual(expectedArgs, (ICollection) values["args"]);
        }

        [Test]
        public void GetFormValuesAsJson_Should_Correspond_To_GetFormValues()
        {
            var request = CreateFullQueryRequest();

            var parameterPairs = request.GetFormValues();
            var json = request.GetFormValuesAsJson();
            var fromJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

            Assert.AreEqual(parameterPairs.Count, fromJson.Count);
            foreach (var pair in parameterPairs)
            {
                var checkCollection = pair.Value as System.Collections.ICollection;
                var real = fromJson[pair.Key];

                if (checkCollection != null)
                {
                    //it's simpler for now to test collections using a JSON serialization
                    var expectedJson = JsonConvert.SerializeObject(pair.Value);
                    var realJson = JsonConvert.SerializeObject(real);
                    Assert.AreEqual(expectedJson, realJson);
                }
                else
                {
                    var expected = pair.Value.ToString();
                    Assert.AreEqual(expected, real.ToString());
                }
            }
        }

        [Test]
        public void When_Timeout_Set_Query_Contains_Milliseconds_With_Unit()
        {
            var query = new QueryRequest()
                .BaseUri(new Uri(string.Format("http://{0}:8093/query", _server)))
                .Statement("SELECT * FROM `beer-sample`")
                .Timeout(new TimeSpan(0, 0, 0, 0, 5));

            var json = query.GetFormValuesAsJson();
            var expected = "{\"statement\":\"SELECT * FROM `beer-sample`\",\"timeout\":\"5ms\"}";
            Console.WriteLine(json);

            Assert.AreEqual(expected, json);
        }

        [Test]
        public void When_Timeout_Is_Not_Set_Default_To_75000ms()
        {
            var query = new QueryRequest()
              .BaseUri(new Uri(string.Format("http://{0}:8093/query", _server)))
              .Statement("SELECT * FROM `beer-sample`");

            var json = query.GetFormValuesAsJson();
            var expected = "{\"statement\":\"SELECT * FROM `beer-sample`\",\"timeout\":\"75000ms\"}";
            Console.WriteLine(json);

            Assert.AreEqual(expected, json);
        }

        [Test]
        public void When_Timeout_Is_Not_Set_QueryString_Values_Use_Timeout_Default()
        {
            var query = new QueryRequest()
                .BaseUri(new Uri(string.Format("http://{0}:8093/query", _server)))
                .Statement("SELECT * from Who");

            var queryStringValues = query.GetFormValues();
            Assert.AreEqual(queryStringValues["timeout"], "75000ms");
        }
    }
}
