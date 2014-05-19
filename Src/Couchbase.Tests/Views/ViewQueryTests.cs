using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Views;
using NUnit.Framework;

namespace Couchbase.Tests.Views
{
    [TestFixture]
    public class ViewQueryTests
    {
        [Test]
        public void When_Development_True_DesignDoc_Has_dev_Prefix()
        {
            var expected = new Uri("http://localhost:8092/default/_design/dev_cities/_view/by_name?");
            var baseUri = "http://localhost:8092";
            var query = new ViewQuery(baseUri, true).
                Bucket("default").
                DesignDoc("cities").
                View("by_name");

            Assert.AreEqual(expected, query.RawUri());
        }

        [Test]
        public void Test_Build_Basic_Query()
        {
            var expected = new Uri("http://localhost:8092/default/_design/cities/_view/by_name?");
            var baseUri = "http://localhost:8092";
            var query = new ViewQuery(baseUri, false).
                Bucket("default").
                DesignDoc("cities").
                View("by_name");

            Assert.AreEqual(expected, query.RawUri());
        }

        [Test]
        public void Test_Build_Basic_Query_Using_From()
        {
            var expected = new Uri("http://localhost:8092/default/_design/cities/_view/by_name?");
            var baseUri = "http://localhost:8092";
            var query = new ViewQuery(baseUri, false).
                From("default", "cities").
                View("by_name");

            Assert.AreEqual(expected, query.RawUri());
        }

        [Test]
        public void Test_Build_Basic_Query_Using_From_Limit_10()
        {
            var expected = new Uri("http://localhost:8092/default/_design/cities/_view/by_name?limit=10");
            var baseUri = "http://localhost:8092";
            var query = new ViewQuery(baseUri, false).
                From("default", "cities").
                View("by_name").
                Limit(10);

            Assert.AreEqual(expected, query.RawUri());
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