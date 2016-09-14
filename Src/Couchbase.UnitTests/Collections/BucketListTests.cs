using Couchbase.Collections;
using Couchbase.Core;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.Collections
{
    [TestFixture]
    public class CouchbaseListTests
    {
        public class Poco
        {
            public string Key { get; set; }

            public string Name { get; set; }
        }

        [Test]
        public void Test_Add()
        {
            var bucket = new Mock<IBucket>();

            var collection = new CouchbaseList<Poco>(bucket.Object, "Thecollection");

            collection.Add(new Poco {Key = "poco1", Name = "Poco-pica"});
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
