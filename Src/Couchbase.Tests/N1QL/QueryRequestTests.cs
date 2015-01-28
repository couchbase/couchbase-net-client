using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.N1QL;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.Tests.N1QL
{
    [TestFixture]
    public class QueryRequestTests
    {
        [SetUp]
        public void SetUp()
        {
            new QueryRequest().ClearCache();
        }
        [Test]
        public void Test_Statement()
        {
            var query = new QueryRequest().
                BaseUri(new Uri("http://192.168.30.101:8093/query")).
                Statement("SELECT * FROM default");

            var uri = query.GetRequestUri();
            Assert.IsTrue(uri.ToString().Contains(":8093/query?statement=SELECT * FROM default"));
            Console.WriteLine(uri);
        }

        [Test]
        public void Test_Statement_ClientContextId()
        {
            var query = new QueryRequest().
                BaseUri(new Uri("http://192.168.30.101:8093/query")).
                Statement("SELECT * FROM default").
                ClientContextId("somecontextlessthanorequalto64chars");

            var uri = query.GetRequestUri();
            Console.WriteLine(uri);
            Assert.IsTrue(uri.ToString().Contains(":8093/query?statement=SELECT * FROM default&client_context_id=somecontextlessthanorequalto64chars"));
        }

        [Test]
        public void Test_Statement_ClientContextId_Pretty()
        {
            var query = new QueryRequest().
                BaseUri(new Uri("http://192.168.30.101:8093/query")).
                Statement("SELECT * FROM default").
                ClientContextId("somecontextlessthanorequalto64chars").
                Pretty(true);

            var uri = query.GetRequestUri();
            Console.WriteLine(uri);
            Assert.IsTrue(uri.ToString().Contains(":8093/query?statement=SELECT * FROM default&pretty=true&client_context_id=somecontextlessthanorequalto64chars"));
        }

        [Test]
        public void Test_Positional_Parameters()
        {
            var query = new QueryRequest().
                BaseUri(new Uri("http://192.168.30.101:8093/query")).
                Statement("SELECT * FROM default WHERE type=$1").
                AddPositionalParameter("dog");

            var uri = query.GetRequestUri();
            Console.WriteLine(uri);
            Assert.IsTrue(uri.ToString().Contains(":8093/query?statement=SELECT * FROM default WHERE type=$1&args=[\"dog\"]"));
        }

        [Test]
        public void Test_Positional_Parameters_Two_Arguments()
        {
            var query = new QueryRequest().
                BaseUri(new Uri("http://192.168.30.101:8093/query")).
                Statement("SELECT * FROM default WHERE type=$1 OR type=$2").
                AddPositionalParameter("dog").
                AddPositionalParameter("cat");

            var uri = query.GetRequestUri();
            Console.WriteLine(uri);
            Assert.IsTrue(uri.ToString().Contains(":8093/query?statement=SELECT * FROM default WHERE type=$1 OR type=$2&args=[\"dog\"%2C\"cat\"]"));
        }

        [Test]
        public void Test_Named_Parameters_Two_Arguments()
        {
            var query = new QueryRequest().
                BaseUri(new Uri("http://192.168.30.101:8093/query")).
                Statement("SELECT * FROM default WHERE type=$canine OR type=$feline").
                AddNamedParameter("canine", "dog").
                AddNamedParameter("feline", "cat");

            var uri = query.GetRequestUri();
            Console.WriteLine(uri);
            Assert.IsTrue(uri.ToString().Contains(":8093/query?statement=SELECT * FROM default WHERE type=$canine OR type=$feline&$canine=\"dog\"&$feline=\"cat\""));
        }

        [Test]
        public void When_isAdmin_Is_True_Credentials_Contains_admin()
        {
            var query = new QueryRequest().
                BaseUri(new Uri("http://192.168.30.101:8093/query")).
                Statement("SELECT * FROM authenticated").
                AddCredentials("authenticated", "secret", true);

            var uri = query.GetRequestUri();
            Console.WriteLine(uri);
            Assert.IsTrue(uri.ToString().Contains(":8093/query?statement=SELECT * FROM authenticated&creds=[{\"user\":\"admin:authenticated\"%2C\"pass\":\"secret\"}]"));
        }

        [Test]
        public void When_isAdmin_Is_False_Credentials_Contains_local()
        {
            var query = new QueryRequest().
                BaseUri(new Uri("http://192.168.30.101:8093/query")).
                Statement("SELECT * FROM authenticated").
                AddCredentials("authenticated", "secret", false);

            var uri = query.GetRequestUri();
            Console.WriteLine(uri);
            Assert.IsTrue(uri.ToString().Contains(":8093/query?statement=SELECT * FROM authenticated&creds=[{\"user\":\"local:authenticated\"%2C\"pass\":\"secret\"}]"));
        }

        [Test]
        public void When_Username_Is_Empty_AddCredentials_Throws_AOOE()
        {
           var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new QueryRequest().
                BaseUri(new Uri("http://192.168.30.101:8093/query")).
                Statement("SELECT * FROM authenticated").
                AddCredentials("", "secret", false));

           Assert.That(ex.Message, Is.EqualTo("cannot be null, empty or whitespace.\r\nParameter name: username"));
        }

        [Test]
        public void When_Username_Is_Whitespace_AddCredentials_Throws_AOOE()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new QueryRequest().
                 BaseUri(new Uri("http://192.168.30.101:8093/query")).
                 Statement("SELECT * FROM authenticated").
                 AddCredentials(" ", "secret", false));

            Assert.That(ex.Message, Is.EqualTo("cannot be null, empty or whitespace.\r\nParameter name: username"));
        }

        [Test]
        public void When_Username_Is_Null_AddCredentials_Throws_AOOE()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new QueryRequest().
                 BaseUri(new Uri("http://192.168.30.101:8093/query")).
                 Statement("SELECT * FROM authenticated").
                 AddCredentials(null, "secret", false));

            Assert.That(ex.Message, Is.EqualTo("cannot be null, empty or whitespace.\r\nParameter name: username"));
        }

        [Test]
        public void Test_GetFormValues()
        {
            var request = new QueryRequest()
                .Metrics(true)
                .HttpMethod(Method.Post)
                .Statement("SELECT * from Who WHERE $1")
                .Pretty(true)
                .ReadOnly(false)
                .ScanConsistency(ScanConsistency.RequestPlus)
                .ScanVector("100")
                .ScanWait(new TimeSpan(0, 0, 0, 0, 100))
                .Signature(true)
                .Timeout(new TimeSpan(0, 0, 0,0, 10000))
                .Compression(Compression.RLE)
                .AddCredentials("authenticated", "secret", false)
                .AddPositionalParameter("boo");

            var values = request.GetFormValues();
            Assert.AreEqual("true", values["metrics"]);
            Assert.AreEqual("SELECT * from Who WHERE $1", values["statement"]);
            Assert.AreEqual("true", values["pretty"]);
            Assert.AreEqual("false", values["readonly"]);
            Assert.AreEqual(Uri.EscapeDataString(JsonConvert.SerializeObject("request_plus")), values["scan_consistency"]);
            Assert.AreEqual("100", values["scan_vector"]);
            Assert.AreEqual("100", values["scan_wait"]);
            Assert.AreEqual("true", values["signature"]);
            Assert.AreEqual(Uri.EscapeDataString(JsonConvert.SerializeObject("RLE")), values["compression"]);
            Assert.AreEqual(Uri.EscapeDataString("[{\"user\":\"local:authenticated\",\"pass\":\"secret\"}]"), values["creds"]);
            Assert.AreEqual("[\"boo\"]", values["args"]);
            Assert.AreEqual("10000ms", values["timeout"]);
        }

        [Test]
        public void When_Timeout_Set_Query_Contains_Milliseconds_With_Unit()
        {
            var query = new QueryRequest()
                .BaseUri(new Uri("http://192.168.30.101:8093/query"))
                .Statement("SELECT * FROM `beer-sample`")
                .Timeout(new TimeSpan(0, 0, 0, 0, 5));

            var uri = query.GetRequestUri();
            Console.WriteLine(uri);

            Assert.IsTrue(uri.ToString().Contains(":8093/query?statement=SELECT * FROM `beer-sample`&timeout=5ms"));
        }

        [Test]
        public void When_Prepared_True_And_No_Cached_Prepared_Statement_Exists_Return_Append_Prepare()
        {
            var query = new QueryRequest()
                .BaseUri(new Uri("http://192.168.30.101:8093/query"))
                .Statement("SELECT * FROM `beer-sample`")
                .Prepared(true);

            var uri = query.GetRequestUri();
            Console.WriteLine(uri);

            Assert.IsTrue(uri.ToString().Contains(":8093/query?statement=PREPARE SELECT * FROM `beer-sample`"));
        }

        [Test]
        public void When_Prepared_True_And_No_Cached_Prepared_Statement_Exists_IPreparable_HasPrepared_Is_False()
        {
            var query = new QueryRequest()
                .BaseUri(new Uri("http://192.168.30.101:8093/query"))
                .Statement("SELECT * FROM `beer-sample`")
                .Prepared(true);

            //need to run this once
            var uri = query.GetRequestUri();

            var preparable = query as IPreparable;
            Assert.IsNotNull(preparable);
            Assert.IsFalse(preparable.HasPrepared);
        }

        [Test]
        public void When_Prepared_True_And_Cached_Prepared_Statement_Exists_IPreparable_HasPrepared_Is_True()
        {
            var query = new QueryRequest()
                .BaseUri(new Uri("http://192.168.30.101:8093/query"))
                .Statement("SELECT * FROM `beer-sample`")
                .Prepared(true);

            //need to run this once
            var uri = query.GetRequestUri();

            var response = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(@"Data\\N1QL\\prepared-statement.json"));
            var preparedStatement = response.results[0].ToString().Replace("\r\n", "");
            var preparable = query as IPreparable;

            Assert.IsNotNull(preparable);
            preparable.CachePreparedStatement(preparedStatement);
            Assert.IsTrue(preparable.HasPrepared);
        }
    }
}
