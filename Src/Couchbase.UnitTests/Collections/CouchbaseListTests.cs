using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Collections;
using Couchbase.Core;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.Collections
{
    [TestFixture]
    public class CouchbaseListTests
    {
        private const string BucketKey = "TestCollection";

        public class Poco
        {
            public string Key { get; set; }

            public string Name { get; set; }
        }

        public static class MockHelper
        {
            public static Mock<IBucket> CreateBucket(params Poco[] items)
            {                
                var result = new Mock<IOperationResult<List<Poco>>>();
                result.SetupGet(x => x.Success).Returns(true);
                result.SetupGet(x => x.Value).Returns(items.ToList());

                var bucket = new Mock<IBucket>();
                bucket.Setup(x => x.Exists(BucketKey)).Returns(true);
                bucket.Setup(x => x.Get<List<Poco>>(BucketKey)).Returns(result.Object);

                return bucket;
            }
        }

        [Test]
        public void Test_Add()
        {
            //arrange
            var builder = new Mock<IMutateInBuilder<List<Poco>>>();
            builder.Setup(x => x.ArrayAppend(It.IsAny<Poco>(), It.IsAny<bool>())).Returns(builder.Object);
            builder.Setup(x => x.Execute()).Returns(new DocumentFragment<List<Poco>>(builder.Object) {Success = true});

            var bucket = MockHelper.CreateBucket();
            bucket.Setup(x => x.MutateIn<List<Poco>>(BucketKey)).Returns(builder.Object);

            var collection = new CouchbaseList<Poco>(bucket.Object, BucketKey);

            var poco = new Poco() { Key = "Poco", Name = "Poco #1" };

            //act/assert
            Assert.DoesNotThrow(()=> collection.Add(poco));

            builder.Verify(x => x.ArrayAppend(poco, true), Times.Once());
            builder.Verify(x => x.Execute(), Times.Once());
        }

        [Test]
        public void Test_Insert()
        {
            //arrange
            var builder = new Mock<IMutateInBuilder<List<Poco>>>();
            builder.Setup(x => x.ArrayInsert("[0]", It.IsAny<Poco>(), It.IsAny<bool>())).Returns(builder.Object);
            builder.Setup(x => x.Execute()).Returns(new DocumentFragment<List<Poco>>(builder.Object) { Success = true });

            var bucket = MockHelper.CreateBucket(new Poco() { Key = "Poco-1", Name = "Poco #1" });
            bucket.Setup(x => x.MutateIn<List<Poco>>(BucketKey)).Returns(builder.Object);

            var collection = new CouchbaseList<Poco>(bucket.Object, BucketKey);

            var poco = new Poco() { Key = "Poco-2", Name = "Poco #2" };

            //act/assert
            Assert.DoesNotThrow(() => collection.Insert(0, poco));

            builder.Verify(x => x.ArrayInsert("[0]", poco, true), Times.Once());
            builder.Verify(x => x.Execute(), Times.Once());
        }

        [Test]
        public void Test_RemoveAt()
        {
            //arrange
            var builder = new Mock<IMutateInBuilder<List<Poco>>>();
            builder.Setup(x => x.Remove("[0]")).Returns(builder.Object);
            builder.Setup(x => x.Execute()).Returns(new DocumentFragment<List<Poco>>(builder.Object) { Success = true });

            var bucket = MockHelper.CreateBucket(new Poco());
            bucket.Setup(x => x.MutateIn<List<Poco>>(BucketKey)).Returns(builder.Object);

            var collection = new CouchbaseList<Poco>(bucket.Object, BucketKey);

            //act/assert
            Assert.DoesNotThrow(() => collection.RemoveAt(0));

            builder.Verify(x => x.Remove("[0]"), Times.Once());
            builder.Verify(x => x.Execute(), Times.Once());
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Test_Contains(bool expected) 
        {
            //arrange
            var item = new Poco();
            var items = new List<Poco>();

            if(expected) 
            {
                items.Add(item);
            }

            var bucket = MockHelper.CreateBucket(items.ToArray());

            var collection = new CouchbaseList<Poco>(bucket.Object, BucketKey);

            //act
            var actual = collection.Contains(item);

            //assert
            Assert.AreEqual(expected, actual);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Test_IndexOf(bool contains)
        {
            //arrange
            var item = new Poco();
            var items = new List<Poco>();

            if(contains)
            {
                items.Add(item);
            }

            var bucket = MockHelper.CreateBucket(items.ToArray());

            var collection = new CouchbaseList<Poco>(bucket.Object, BucketKey);

            //act
            var expected = contains ? 0 : -1;
            var actual = collection.IndexOf(item);

            //assert
            Assert.AreEqual(expected, actual);
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
