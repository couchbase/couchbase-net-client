using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class ObserveTests : OperationTestBase
    {
        [Test]
        public void Test_Observe()
        {
            const string key = "Test_Observe";

            var operation = new Observe(key, GetVBucket(), new AutoByteConverter());
            var result = IOStrategy.Execute(operation);
            Console.WriteLine(result.Message);
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Test_Observe2()
        {
            const string key = "Test_Observe2";
            var remove = new Delete(key, GetVBucket(), Converter, Serializer);

            var set = new Set<int>(key, 10, GetVBucket(), Converter);
            var result = IOStrategy.Execute(set);
            Assert.IsTrue(result.Success);

            var get = new Get<dynamic>(key, GetVBucket(), Converter, Serializer);
            var result1 = IOStrategy.Execute(get);
            Assert.IsTrue(result1.Success);
            Assert.AreEqual(result.Cas, result1.Cas);

            var operation = new Observe(key, GetVBucket(), new AutoByteConverter());
            var result2 = IOStrategy.Execute(operation);
            Assert.AreEqual(result1.Cas, result2.Value.Cas);

            Assert.AreEqual(KeyState.FoundPersisted, result2.Value.KeyState);
            Assert.IsTrue(result2.Success);
        }

        [Test]
        public void Test_Observe3()
        {
            var config = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri("http://192.168.56.101:8091/pools")
                },
                PoolConfiguration = new PoolConfiguration
                {
                    MaxSize = 2,
                    MinSize = 1
                },
                UseSsl = false
            };

            using (var cluster = new CouchbaseCluster(config))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    //bucket.Observe("keythatdoesntexist", 0, ReplicateTo.One, PersistTo.One);
                    for (int i = 0; i < 10; i++)
                    {
                        var document = new Document<dynamic>
                        {
                            Id = "jeb123",
                            Value = new {Name = "jeb"}
                        };
                        bucket.Remove(document);
                        Assert.IsFalse(bucket.Get<dynamic>(document.Id).Success);

                        var result = bucket.Insert(document);
                        bucket.Insert(document);
                        bucket.Observe(document.Id, result.Document.Cas, true, ReplicateTo.Two, PersistTo.One);
                    }
                }
            }
        }
    }
}
