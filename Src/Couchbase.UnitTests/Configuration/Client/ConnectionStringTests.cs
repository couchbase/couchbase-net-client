using System;
using System.Collections;
using System.Collections.Generic;
using Couchbase.Configuration.Client;
using NUnit.Framework;

namespace Couchbase.UnitTests.Configuration.Client
{
    [TestFixture]
    public class ConnectionStringTests
    {
        public static IEnumerable SuccessCases
        {
            get
            {
                yield return new TestCaseData(
                    "10.0.0.1:8091",
                    ConnectionScheme.Http.ToString(),
                    new[] {"10.0.0.1"},
                    (ushort?) 8091);

                yield return new TestCaseData(
                    "http://10.0.0.1",
                    ConnectionScheme.Http.ToString(),
                    new[] {"10.0.0.1"},
                    null);

                yield return new TestCaseData(
                    "couchbase://10.0.0.1",
                    ConnectionScheme.Couchbase.ToString(),
                    new[] {"10.0.0.1"},
                    null);

                yield return new TestCaseData(
                    "couchbases://10.0.0.1,10.0.0.2,10.0.0.3:11207",
                    ConnectionScheme.Couchbases.ToString(),
                    new[] {"10.0.0.1", "10.0.0.2", "10.0.0.3"},
                    (ushort?) 11207);

                yield return new TestCaseData(
                    "couchbase://10.0.0.1;10.0.0.2:11210;10.0.0.3",
                    ConnectionScheme.Couchbase.ToString(),
                    new[] {"10.0.0.1", "10.0.0.2", "10.0.0.3"},
                    (ushort?) 11210);

                yield return new TestCaseData(
                    "couchbase://test.local:11210?key=value",
                    ConnectionScheme.Couchbase.ToString(),
                    new[] {"test.local"},
                    (ushort?) 11210);

                yield return new TestCaseData(
                    "couchbase://10.0.0.1",
                    ConnectionScheme.Couchbase.ToString(),
                    new[] {"10.0.0.1"},
                    null);

                yield return new TestCaseData(
                    "http://fqdn",
                    ConnectionScheme.Http.ToString(),
                    new[] {"fqdn"},
                    null);

                yield return new TestCaseData(
                    "http://fqdn?key=value",
                    ConnectionScheme.Http.ToString(),
                    new[] {"fqdn"},
                    null);

                yield return new TestCaseData(
                    "couchbases://fqdn",
                    ConnectionScheme.Couchbases.ToString(),
                    new[] {"fqdn"},
                    null);
            }
        }

        [Test, TestCaseSource(nameof(SuccessCases))]
        public void Parse_Success(string connectionString, string scheme, string[] hosts, ushort? port)
        {
            // Act

            var result = ConnectionString.Parse(connectionString);

            // Assert

            Assert.AreEqual(scheme, result.Scheme.ToString());
            Assert.AreEqual(hosts.Length, result.Hosts.Count);
            foreach (var host in hosts)
            {
                Assert.IsTrue(result.Hosts.Contains(host));
            }

            Assert.AreEqual(port, result.Port);
        }

        public static IEnumerable<string> ErrorCases
        {
            get
            {
                yield return "http://host1,http://host2";
                yield return "https://host2:8091,host3:8091";
            }
        }

        [Test, TestCaseSource(nameof(ErrorCases))]
        public void Parse_Error(string connectionString)
        {
            // Act/Assert

            Assert.Throws<Exception>(() => ConnectionString.Parse(connectionString));
        }
    }
}
