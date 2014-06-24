using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Serializers;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using NUnit.Framework;
using Wintellect;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public sealed class GetSetPerformanceTests : OperationTestBase
    {
        [Test]
        public void Test_Timed_Execution()
        {
            var converter = new AutoByteConverter();
            var serializer = new TypeSerializer2(converter);
            var vbucket = GetVBucket();
            int n = 1000; //set to a higher # if needed

            using (new OperationTimer())
            {
                var key = string.Format("key{0}", 111);
                var set = new SetOperation<int>(key, 111, vbucket, converter);
                var get = new GetOperation<int>(key, vbucket, converter, serializer);

                for (var i = 0; i < n; i++)
                {
                    var result = IOStrategy.Execute(set);
                    Assert.IsTrue(result.Success);

                    var result1 = IOStrategy.Execute(get);
                    Assert.IsTrue(result1.Success);
                    Assert.AreEqual(111, result1.Value);
                }
            }
        }

       [Test]
        public void Test_Timed_Execution_Parallel()
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = 4 };
            var converter = new AutoByteConverter();
            var serializer = new TypeSerializer2(converter);
            var vbucket = GetVBucket();
            var n = 1000;//set to a higher # if needed

            using (new OperationTimer())
            {
                Parallel.For(0, n, options, i =>
                {
                    var key = string.Format("key{0}", i);
                    var set = new SetOperation<int>(key, i, vbucket, converter);
                    var result = IOStrategy.Execute(set);
                    Assert.IsTrue(result.Success);
  
                    var get = new GetOperation<int>(key, vbucket, converter, serializer);
                    var result1 = IOStrategy.Execute(get);
                    Assert.IsTrue(result1.Success); 
                    Assert.AreEqual(i, result1.Value);
                });
            }
        }

        [Test]
        public void Get()
        {
            using (var cluster = new CouchbaseCluster())
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var key = string.Format("key{0}", 1);
                    var result = bucket.Get<int>(key);
                    Console.WriteLine(result.Value);
                }
            }
        }

        [Test]
        public void Set()
        {
            using (var cluster = new CouchbaseCluster())
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var key = string.Format("key{0}", 1);
                    var result = bucket.Upsert(key, 12);
                    Console.WriteLine(result.Value);
                }
            }
        }

        [Test]
        public void Test_Timed_Execution_Parallel_Client()
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = 4 };
            var n = 1000;//set to a higher # if needed

            using (var cluster = new CouchbaseCluster())
            {
                using (var bucket = cluster.OpenBucket())
                {
                    using (new OperationTimer())
                    {
                        var temp = bucket;
                        Parallel.For(0, n, options, i =>
                        {
                            var key = string.Format("key{0}", i);
                            var result = temp.Upsert(key, i);
                            Assert.IsTrue(result.Success);

                            var result1 = temp.Get<int>(key);
                            Assert.IsTrue(result1.Success);
                            Assert.AreEqual(i, result1.Value);
                        });
                    }
                }
            }
        }
    }
}
