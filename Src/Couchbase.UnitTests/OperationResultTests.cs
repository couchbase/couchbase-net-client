using NUnit.Framework;

namespace Couchbase.UnitTests
{
    [TestFixture]
    public class OperationResultTests
    {
        [Test]
        public void OperationResult_ToString_TokenIsNull()
        {
            var result = new OperationResult
            {
                Cas = 10202020202
            };

            var expected = "{\"id\":null,\"cas\":10202020202,\"token\":null}";
            Assert.AreEqual(expected, result.ToString().Replace(" ", ""));
        }

        [Test]
        public void OperationResult_ToString_IdNotNull()
        {
            var result = new OperationResult
            {
                Cas = 10202020202,
                Id = "foo"
            };

            var expected = "{\"id\":\"foo\",\"cas\":10202020202,\"token\":null}";
            Assert.AreEqual(expected, result.ToString());
        }

        [Test]
        public void OperationResult_ToString_ContentNotNull()
        {
            var result = new OperationResult<dynamic>
            {
                Cas = 10202020202,
                Id = "foo",
                Value = new { Name="ted", Age=10}
            };

            var expected = "{\"id\":\"foo\",\"cas\":10202020202,\"token\":null,\"content\":\"{\\\"Name\\\":\\\"ted\\\",\\\"Age\\\":10}\"}";
            Assert.AreEqual(expected, result.ToString());
        }


        [Test]
        public void DocumentResult_ToString_ContentNotNull()
        {
            var result = new DocumentResult<dynamic>(new OperationResult<dynamic>
            {
                Cas = 10202020202,
                Id = "foo",
                Value = new { Name = "ted", Age = 10 }
            });

            var expected = "{\"id\":\"foo\",\"cas\":10202020202,\"token\":null,\"content\":\"{\\\"name\\\":\\\"ted\\\",\\\"age\\\":10}\"}";
            Assert.IsNotNull(expected, result.ToString());
        }

        [Test]
        public void Document_ToString_ContentNotNull()
        {
            var result = new DocumentResult<dynamic>(new OperationResult<dynamic>
            {
                Cas = 10202020202,
                Id = "foo",
                Value = new { Name = "ted", Age = 10 }
            });

            var expected = "{\"id\":\"foo\",\"cas\":10202020202,\"token\":null,\"content\":\"{\\\"Name\\\":\\\"ted\\\",\\\"Age\\\":10}\"}";
            Assert.AreEqual(expected, result.Document.ToString());
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
