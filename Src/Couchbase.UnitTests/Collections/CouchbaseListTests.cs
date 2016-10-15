using System;
using System.Collections.Generic;
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
            //arrange
            var builder = new Mock<IMutateInBuilder<List<Poco>>>();
            builder.Setup(x => x.ArrayAppend(It.IsAny<Poco>(), It.IsAny<bool>())).Returns(builder.Object);
            builder.Setup(x => x.Execute()).Returns(new DocumentFragment<List<Poco>>(builder.Object) {Success = true});

            var bucket = new Mock<IBucket>();
            bucket.Setup(x => x.Insert(It.IsAny<Document<List<Poco>>>())).
                Returns(new DocumentResult<List<Poco>>(new OperationResult<List<Poco>>
            {
                Success = true
            }));
            bucket.Setup(x => x.MutateIn<List<Poco>>(It.IsAny<string>())).Returns(builder.Object);

            var collection = new CouchbaseList<Poco>(bucket.Object, "Thecollection");

            //act/assert
            Assert.DoesNotThrow(()=> collection.Add(new Poco { Key = "poco1", Name = "Poco-pica" }));
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
