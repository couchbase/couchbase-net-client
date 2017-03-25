using System.Collections.Generic;
using Couchbase.Core.IO.SubDocument;
using Couchbase.Core.Serialization;
using Couchbase.IO.Operations;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests
{
    [TestFixture()]
    public class DocumentFragmentTests
    {
        [Test]
        public void When_Bytes_Are_Null_Return_Default()
        {
            var typeSerializer = new Mock<ITypeSerializerProvider>();
            var fragment = new DocumentFragment<dynamic>(typeSerializer.Object)
            {
                Value = new List<OperationSpec>
                {
                    new OperationSpec
                    {
                        Bytes = null, //ack!
                        OpCode = OperationCode.Get
                    }
                }
            };

            Assert.DoesNotThrow(() => fragment.Content<dynamic>(0));
            Assert.AreEqual(default(dynamic), fragment.Content<dynamic>(0));
            Assert.IsNull(fragment.Content<Poco>(0));
        }

        [Test]
        public void When_Bytes_Are_Empty_Return_Default()
        {
            var typeSerializer = new Mock<ITypeSerializerProvider>();
            typeSerializer.Setup(x => x.Serializer).Returns(new DefaultSerializer());

            var fragment = new DocumentFragment<dynamic>(typeSerializer.Object)
            {
                Value = new List<OperationSpec>
                {
                    new OperationSpec
                    {
                        Bytes = new byte[] {}, //doh!
                        OpCode = OperationCode.Get
                    }
                }
            };

            Assert.DoesNotThrow(() => fragment.Content<dynamic>(0));
            Assert.AreEqual(default(dynamic), fragment.Content<dynamic>(0));
            Assert.IsNull(fragment.Content<Poco>(0));
        }

        [Test]
        public void When_Bytes_Are_Empty_For_Poco_Return_Default()
        {
            var typeSerializer = new Mock<ITypeSerializerProvider>();
            typeSerializer.Setup(x => x.Serializer).Returns(new DefaultSerializer());

            var fragment = new DocumentFragment<dynamic>(typeSerializer.Object)
            {
                Value = new List<OperationSpec>
                {
                    new OperationSpec
                    {
                        Bytes = new byte[] {}, //doh!
                        OpCode = OperationCode.Get
                    }
                }
            };

            Assert.DoesNotThrow(() => fragment.Content<Poco>(0));
            Assert.AreEqual(default(Poco), fragment.Content<Poco>(0));
            Assert.IsNull(fragment.Content<Poco>(0));
        }
    }

    public class Poco
    {
        public string Name { get; set; }
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
