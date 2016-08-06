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

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _cluster = new Cluster("couchbaseClients/couchbase");
        }

        [Test]
        public void Test_Insert_String()
        {
            using (var bucket = _cluster.OpenBucket("default"))
            {
                var response = bucket.Upsert(TestKeys.KeyWithStringValue.Key, TestKeys.KeyWithStringValue.Value);
                Assert.IsTrue(response.Success);
            }
        }

        [Test]
        public void Test_Insert_Int32()
        {
            using (var bucket = _cluster.OpenBucket("default"))
            {
                var response = bucket.Upsert(TestKeys.KeyWithInt32Value.Key, TestKeys.KeyWithInt32Value.Value);
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

                var response = bucket.Upsert("dynamickey", obj);
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
                var response = bucket.Upsert("pocokey3", foo);
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

        [OneTimeTearDown]
        public void TearDown()
        {
            _cluster.Dispose();
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion