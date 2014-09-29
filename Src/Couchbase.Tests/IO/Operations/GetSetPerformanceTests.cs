using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
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
            var transcoder = new DefaultTranscoder(converter);
            var vbucket = GetVBucket();
            int n = 1000; //set to a higher # if needed

            using (new OperationTimer())
            {
                var key = string.Format("key{0}", 111);
                var set = new Set<int?>(key, 111, vbucket, converter);
                var get = new Get<int?>(key, vbucket, converter, transcoder);

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
            var transcoder = new DefaultTranscoder(converter);
            var vbucket = GetVBucket();
            var n = 1000;//set to a higher # if needed

            using (new OperationTimer())
            {
                Parallel.For(0, n, options, i =>
                {
                    var key = string.Format("key{0}", i);
                    var set = new Set<int?>(key, i, vbucket, converter);
                    var result = IOStrategy.Execute(set);
                    Assert.IsTrue(result.Success);
  
                    var get = new Get<int?>(key, vbucket, converter, transcoder);
                    var result1 = IOStrategy.Execute(get);
                    Assert.IsTrue(result1.Success); 
                    Assert.AreEqual(i, result1.Value);
                });
            }
        }

        [Test]
        public void Get()
        {
            using (var cluster = new Cluster())
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var key = string.Format("key{0}", 1);
                    var result = bucket.Get<int>(key);
                    Assert.IsTrue(result.Success);
                }
            }
        }

        [Test]
        public void Set()
        {
            using (var cluster = new Cluster())
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

            using (var cluster = new Cluster())
            {
                using (var bucket = cluster.OpenBucket())
                {
                    using (new OperationTimer())
                    {
                        var temp = bucket;
                        Parallel.For(0, n, options, i =>
                        {
                            var key = string.Format("key{0}", i);
                            var value = (int?) i;
                            var result = temp.Upsert(key, value);
                            Assert.IsTrue(result.Success);

                            var result1 = temp.Get<int?>(key);
                            Assert.IsTrue(result1.Success);
                            Assert.AreEqual(i, result1.Value);
                        });
                    }
                }
            }
        }
    }
}
