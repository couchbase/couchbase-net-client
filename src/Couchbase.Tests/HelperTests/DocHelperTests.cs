using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Couchbase.Helpers;
using Newtonsoft.Json;

namespace Couchbase.Tests.HelperTests
{
    [TestFixture]
    public class DocHelperTests
    {
        [Test]
        public void When_Inserting_Id_Into_Doc_Json_String_Is_Valid_And_Contains_Id()
        {
            var json = "{ \"message\" : \"Test\" }";
            var jsonWithId = DocHelper.InsertId(json, "8675309");

            Assert.That(jsonWithId, Is.StringContaining("\"id\":\"8675309\""));
            Assert.That(JsonConvert.DeserializeObject(jsonWithId), Is.Not.Null);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
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