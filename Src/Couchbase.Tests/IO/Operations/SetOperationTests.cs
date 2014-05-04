using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class SetOperationTests
    {
        private Cluster _cluster;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            Cluster.Initialize();
            _cluster = Cluster.Get();
        }

        [Test]
        public void Test_Insert_String()
        {
            using (var bucket = _cluster.OpenBucket("default"))
            {
                var response = bucket.Insert(TestKeys.KeyWithStringValue.Key, TestKeys.KeyWithStringValue.Value);
                Assert.IsTrue(response.Success);
            }
        }

        [Test]
        public void Test_Insert_Int32()
        {
            using (var bucket = _cluster.OpenBucket("default"))
            {
                var response = bucket.Insert(TestKeys.KeyWithInt32Value.Key, TestKeys.KeyWithInt32Value.Value);
                Assert.IsTrue(response.Success);
            }
        }

        [Test]
        public void Test_Insert_dynamic_Type()
        {
            using (var bucket = _cluster.OpenBucket("default"))
            {
                dynamic obj = new
                {
                    StringProperty="somestring",
                    IntProperty = 23
                };
                
                var response = bucket.Insert("dynamickey", obj);
                Assert.IsTrue(response.Success);
            }
        }

        [Test]
        public void Test_Insert_POCO()
        {
            using (var bucket = _cluster.OpenBucket("default"))
            {
                var foo = new Foo
                {
                    Age = 24,
                    Bar = "None4"
                };
                var response = bucket.Insert("pocokey3", foo);
                Assert.IsTrue(response.Success);
                Console.WriteLine(response.Message);
                Console.WriteLine(response.Status);
            }
        }

        public class Foo
        {
            public string Bar { get; set; }

            public int Age { get; set; }
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            _cluster.Dispose();
        }
    }
}
