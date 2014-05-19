using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.IO;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class AddOperationTests : OperationTestBase
    {
        [Test]
        public void When_Key_Doesnt_Exist_Operation_Succeeds()
        {
            const string key = "keythatdoesntexist";

            //delete the value if it exists
            var deleteOperation = new DeleteOperation(key, GetVBucket());
            var result1 = IOStrategy.Execute(deleteOperation);

            var operation = new AddOperation<dynamic>(key, new {foo = "foo"}, GetVBucket());
            var result = IOStrategy.Execute(operation);
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void When_Key_Exists_Exist_Operation_Fails()
        {
            var operation = new AddOperation<dynamic>("keythatdoesntexist", new { foo = "foo" }, GetVBucket());
            var result = IOStrategy.Execute(operation);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(ResponseStatus.KeyExists, result.Status);
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