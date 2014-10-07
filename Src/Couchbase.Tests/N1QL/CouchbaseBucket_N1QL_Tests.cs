using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Couchbase.Tests.N1QL
{
    [TestFixture]
    public class CouchbaseBucketN1QlTests
    {
        private Cluster _cluster;

        [SetUp]
        public void SetUp()
        {
            _cluster = new Cluster();
        }

        [TearDown]
        public void TearDown()
        {
            _cluster.Dispose();
        }

        [Test]
        public async void Test_QueryAsync()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                const string query = "SELECT * FROM tutorial WHERE fname = 'Ian'";

                var result = await bucket.QueryAsync<dynamic>(query);
                foreach (var row in result.Rows)
                {
                    Console.WriteLine(row);
                }
            }

        }

        [Test]
        public void Test_N1QL_Query()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                const string query = "SELECT * FROM tutorial WHERE fname = 'Ian'";

                var result = bucket.Query<dynamic>(query);
                foreach (var row in result.Rows)
                {
                    Console.WriteLine(row);
                }
            }
        }
    }
}
