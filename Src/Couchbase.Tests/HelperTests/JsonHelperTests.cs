using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Couchbase.Helpers;

namespace Couchbase.Tests.HelperTests
{
    [TestFixture(Category="HelperTests")]
    public class JsonHelperTests
    {
        [Test]
        public void When_Deserializing_To_Generic_Type_Properties_Are_Set()
        {
            var json = "{ 'name' : 'Silvio', 'Age' : 50, 'Sex' : 'M' }";
            var obj = JsonHelper.Deserialize<Person>(json);

            Assert.That(obj, Is.InstanceOf(typeof(Person)));
            Assert.That(obj.Name, Is.StringMatching("Silvio"));
            Assert.That(obj.Age, Is.EqualTo(50));
            Assert.That(obj.Sex, Is.EqualTo('M'));
        }

        private class Person
        {
            public string Name { get; set; }

            public int Age { get; set; }

            public char Sex { get; set; }
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